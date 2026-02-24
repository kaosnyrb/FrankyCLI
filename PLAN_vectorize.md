# Vectorize Starfield Object Placements — Implementation Plan

Goal: extract dungeon placement sequences from FrankyCLI's generation pipeline, encode them as discrete token vectors, and train a GPT-style autoregressive model that can generate new dungeon topologies.

---

## Overview

Two levels of representation are useful:

| Level | Source | Sequence element | Use case |
|-------|--------|-----------------|----------|
| **Topology** | `DungeonState.placedRooms` | One token per room (PackIn EditorID) | High-level layout generation |
| **Object** | `Cell.Temporary/Persistent` | One token per PlacedObject | Low-level interior recreation |

Start with topology sequences — they are smaller, easier to validate, and directly drive the existing connector-based generation pipeline. Object sequences come later.

---

## Phase 1 — Vocabulary & Token Types

### Files to create

#### `Retrograde.Library/Vectorization/TokenCategory.cs`
```csharp
public enum TokenCategory
{
    Special    = 0,   // PAD, START, END, SEP
    TrunkRoom  = 1,
    HabRoom    = 2,
    OreRoom    = 3,
    UtilRoom   = 4,
    BossRoom   = 5,
    Connector  = 6,   // XMarkerHeading connector markers
    ContentObj = 7,   // furniture, lights, loot
    EnemySpawn = 8,   // XMarker enemy positions
    Structure  = 9,   // Static structural pieces (SciIntHallSm*, etc.)
}
```

#### `Retrograde.Library/Vectorization/DungeonToken.cs`
```csharp
public record DungeonToken
{
    // Vocabulary token (type identity)
    public int TypeId;           // Index into TokenVocab

    // Position — quantized to bins relative to sequence bounding box
    public int XBin;             // 0..BinCount-1
    public int YBin;
    public int ZBin;

    // Orientation
    public int YawSteps;         // 0..3

    // Attributes (for conditioning, not for prediction targets)
    public TokenCategory Category;
    public int DistrictId;       // 0=trunk, 1=hab, 2=ore, 3=util, 4=boss

    // Raw values preserved for reconstruction
    public float RawX, RawY, RawZ;
    public string? EditorId;     // original source EditorID (nullable if unknown)
}

public static class SpecialTokens
{
    public const int PAD   = 0;
    public const int START = 1;
    public const int END   = 2;
    public const int SEP   = 3;  // separates rooms within a sequence
    public const int UNK   = 4;
    public const int FIRST_REAL = 5;
}
```

#### `Retrograde.Library/Vectorization/TokenVocab.cs`
```csharp
public class TokenVocab
{
    // Token ID 0-4 are special (see SpecialTokens)
    private readonly Dictionary<string, int> _editorIdToId = new();
    private readonly Dictionary<int, string> _idToEditorId = new();
    private int _nextId = SpecialTokens.FIRST_REAL;

    public int Count => _nextId;

    // Call during vocab build pass; returns existing id if already present
    public int GetOrAdd(string editorId);
    public int Encode(string editorId);               // returns UNK if unknown
    public string Decode(int tokenId);                // returns null if unknown
    public TokenCategory CategoryFor(int tokenId);    // derived from EditorID conventions

    // Serialize / deserialize
    public void Save(string jsonPath);
    public static TokenVocab Load(string jsonPath);

    // Build from template mods — scans PackIns, Statics, Activators
    public static TokenVocab BuildFromMods(IEnumerable<IStarfieldModGetter> mods);
}
```

**Vocab JSON format:**
```json
{
  "version": 1,
  "special": {"PAD":0,"START":1,"END":2,"SEP":3,"UNK":4},
  "tokens": [
    {"id": 5, "editorId": "rg_sts_trk_big_001", "category": "TrunkRoom"},
    {"id": 6, "editorId": "rg_sts_trk_big_002", "category": "TrunkRoom"},
    ...
  ]
}
```

**Vocabulary build strategy:**
- Scan all `IPackInGetter` records from template mods → EditorIDs → token IDs
- Scan `IStaticGetter` records for kit pieces (SciIntHallSm*, SciIntRmSm*, XMarker*, etc.)
- Scan `IActivatorGetter` records for alert boxes (DMP_Room_*)
- Expected vocab size: ~600–800 tokens total

---

## Phase 2 — Position Quantization

#### `Retrograde.Library/Vectorization/PositionQuantizer.cs`
```csharp
public class PositionQuantizer
{
    public const int BinCount = 64;   // 6 bits per axis → 18 bits total per position

    // Station topology: rooms span roughly ±200 units in X/Y, ±50 Z
    public static readonly PositionQuantizer StationTopology =
        new(-250f, 250f, -250f, 250f, -60f, 60f);

    // Worldspace tiles: tile grid units, varies
    public static readonly PositionQuantizer WorldspaceTile =
        new(-100f, 100f, -100f, 100f, -20f, 20f);

    // Object-level (within a single cell, ±100 units each axis)
    public static readonly PositionQuantizer CellObject =
        new(-120f, 120f, -120f, 120f, -60f, 60f);

    public PositionQuantizer(float xMin, float xMax, float yMin, float yMax,
                              float zMin, float zMax);

    public (int x, int y, int z) Quantize(P3Float pos);
    public P3Float Dequantize(int xBin, int yBin, int zBin);

    // Clamp out-of-range positions to boundary bins rather than throwing
    private int QuantizeAxis(float val, float min, float max);
}
```

**Bin encoding:** linear interpolation. `bin = clamp((val - min) / (max - min) * BinCount, 0, BinCount-1)`.

---

## Phase 3 — Sequence Representation

#### `Retrograde.Library/Vectorization/DungeonSequence.cs`
```csharp
public enum SequenceType { Topology, ObjectLevel }

public class DungeonSequence
{
    public string Id;                   // e.g. "run_0042_topology"
    public SequenceType Type;
    public string DungeonStyle;         // "station", "worldspace_fort", etc.
    public string? Faction;             // "spacer", "crimson", etc.
    public List<DungeonToken> Tokens;

    // Bounding box of raw positions — used for relative normalization if needed
    public float MinX, MaxX, MinY, MaxY, MinZ, MaxZ;

    // Stats
    public int RoomCount;
    public int TokenCount => Tokens.Count;
}
```

**Flat integer encoding** for ML consumption — each token is 6 integers:
```
[type_id, x_bin, y_bin, z_bin, yaw_id, district_id]
```

Concatenated as `[START, token0[0..5], SEP, token1[0..5], SEP, ..., END]` → variable-length integer sequence.

---

## Phase 4 — Extraction Pipeline (C#)

#### `Retrograde.Library/Vectorization/TopologyExtractor.cs`
```csharp
public static class TopologyExtractor
{
    // Extract a topology sequence from a completed DungeonState.
    // Call AFTER StationDungeonGenerator.GenerateTopology() returns.
    public static DungeonSequence Extract(
        DungeonState state,
        TokenVocab vocab,
        PositionQuantizer quantizer,
        string runId,
        string? faction = null)
    {
        var tokens = new List<DungeonToken>();

        foreach (var room in state.placedRooms)
        {
            var (xb, yb, zb) = quantizer.Quantize(room.WorldPos);
            tokens.Add(new DungeonToken
            {
                TypeId    = vocab.Encode(room.Prefab.PrefabEditorId),
                XBin      = xb, YBin = yb, ZBin = zb,
                YawSteps  = room.YawSteps,
                Category  = vocab.CategoryFor(vocab.Encode(room.Prefab.PrefabEditorId)),
                DistrictId = DistrictToId(room.DistrictType),
                RawX = room.WorldPos.X, RawY = room.WorldPos.Y, RawZ = room.WorldPos.Z,
                EditorId  = room.Prefab.PrefabEditorId
            });
        }

        return new DungeonSequence
        {
            Id = runId + "_topology",
            Type = SequenceType.Topology,
            DungeonStyle = "station",
            Faction = faction,
            Tokens = tokens,
            RoomCount = state.placedRooms.Count,
        };
    }

    private static int DistrictToId(string? district) => district switch
    {
        "trunk" => 0, "hab" => 1, "ore" => 2,
        "util" => 3, "boss" => 4, _ => 0
    };
}
```

#### `Retrograde.Library/Vectorization/ObjectExtractor.cs`
```csharp
public static class ObjectExtractor
{
    // Extract object-level sequence from a Cell's placed objects.
    // Sorts by position (X then Y then Z) for deterministic ordering.
    public static DungeonSequence Extract(
        ICellGetter cell,
        TokenVocab vocab,
        PositionQuantizer quantizer,
        string sequenceId,
        string dungeonStyle)
    {
        var allPlaced = cell.Temporary.Concat(cell.Persistent)
            .OfType<IPlacedObjectGetter>()
            .OrderBy(p => p.Position.X).ThenBy(p => p.Position.Y).ThenBy(p => p.Position.Z);

        var tokens = new List<DungeonToken>();
        foreach (var obj in allPlaced)
        {
            var editorId = obj.Base.FormKey.ToString(); // fallback to FormKey string
            // Try to resolve EditorID from template mods if available
            var (xb, yb, zb) = quantizer.Quantize(obj.Position);
            var yawSteps = RotationToYawSteps(obj.Rotation.Z);
            tokens.Add(new DungeonToken
            {
                TypeId   = vocab.Encode(editorId),
                XBin = xb, YBin = yb, ZBin = zb,
                YawSteps = yawSteps,
                Category = TokenCategory.ContentObj, // refined per vocab lookup
                RawX = obj.Position.X, RawY = obj.Position.Y, RawZ = obj.Position.Z,
                EditorId = editorId,
            });
        }

        return new DungeonSequence { Id = sequenceId, Type = SequenceType.ObjectLevel,
            DungeonStyle = dungeonStyle, Tokens = tokens };
    }

    private static int RotationToYawSteps(float radians)
    {
        // Round to nearest 90° step: 0=north, 1=east, 2=south, 3=west
        int steps = (int)Math.Round(radians / (MathF.PI / 2f));
        return ((steps % 4) + 4) % 4;
    }
}
```

#### `Retrograde.Library/Vectorization/SequenceSerializer.cs`
```csharp
public static class SequenceSerializer
{
    // Append one sequence as a JSON line to the output file
    public static void AppendJsonLine(DungeonSequence seq, string outputPath);

    // Read all sequences from a JSON Lines file
    public static IEnumerable<DungeonSequence> ReadJsonLines(string path);

    // Flat integer encoding for ML: returns int[][] where each inner array is
    // [type_id, x_bin, y_bin, z_bin, yaw_id, district_id]
    public static int[][] ToFlatInts(DungeonSequence seq);

    // Summary statistics across a dataset file
    public static void PrintStats(string jsonLinesPath);
}
```

**JSON Lines record format:**
```json
{
  "id": "run_0042_topology",
  "type": "topology",
  "style": "station",
  "faction": "spacer",
  "room_count": 18,
  "tokens": [
    {"tid": 5, "x": 32, "y": 32, "z": 32, "yaw": 0, "dist": 0, "eid": "rg_sts_trk_big_001"},
    {"tid": 7, "x": 38, "y": 32, "z": 32, "yaw": 1, "dist": 0, "eid": "rg_sts_trk_big_003"},
    ...
  ]
}
```

---

## Phase 5 — CLI Entry Point

#### `gen_vectorize.cs`
```csharp
// Usage: gen_vectorize [outdir] [runs=500] [faction=spacer] [--build-vocab]
public static class gen_vectorize
{
    public static int Generate(string[] args)
    {
        string outDir   = args.Length > 0 ? args[0] : "vectorize_output";
        int runs        = args.Length > 1 ? int.Parse(args[1]) : 500;
        string faction  = args.Length > 2 ? args[2] : "spacer";
        bool buildVocab = args.Contains("--build-vocab");

        Directory.CreateDirectory(outDir);
        string vocabPath = Path.Combine(outDir, "vocab.json");
        string dataPath  = Path.Combine(outDir, "sequences.jsonl");

        // 1. Build or load vocabulary
        TokenVocab vocab;
        if (buildVocab || !File.Exists(vocabPath))
        {
            Console.WriteLine("[vectorize] Building vocabulary from template mods...");
            vocab = TokenVocab.BuildFromMods(RetrogradeContext.Current.TemplateMods);
            vocab.Save(vocabPath);
            Console.WriteLine($"[vectorize] Vocab size: {vocab.Count} tokens → {vocabPath}");
        }
        else
        {
            vocab = TokenVocab.Load(vocabPath);
            Console.WriteLine($"[vectorize] Loaded vocab ({vocab.Count} tokens)");
        }

        var quantizer = PositionQuantizer.StationTopology;
        int success = 0, fail = 0;

        // 2. Generate N dungeons and extract sequences
        for (int i = 0; i < runs; i++)
        {
            try
            {
                // Reuse the gen_harness pattern — create a fresh mod context per run
                using var ctx = RetrogradeContext.CreateEphemeral();
                var generator = new StationDungeonGenerator(ctx, faction: faction);
                var state = generator.GenerateTopology(); // stops after topology

                var seq = TopologyExtractor.Extract(
                    state, vocab, quantizer,
                    runId: $"run_{i:D5}",
                    faction: faction);

                SequenceSerializer.AppendJsonLine(seq, dataPath);
                success++;

                if (i % 50 == 0)
                    Console.WriteLine($"[vectorize] {i}/{runs} runs, {success} ok, {fail} failed");
            }
            catch (Exception ex)
            {
                fail++;
                Console.WriteLine($"[vectorize] Run {i} failed: {ex.Message}");
            }
        }

        Console.WriteLine($"[vectorize] Done. {success} sequences → {dataPath}");
        SequenceSerializer.PrintStats(dataPath);
        return 0;
    }
}
```

#### `scripts/gen_vectorize.sh`
```bash
#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
OUTDIR="${1:-vectorize_output}"
RUNS="${2:-500}"
FACTION="${3:-spacer}"
dotnet run --project FrankyCLI.csproj -- gen_vectorize "$OUTDIR" "$RUNS" "$FACTION" "${@:4}"
```

#### Modification to `Program.cs`
Add dispatch case:
```csharp
"gen_vectorize" => gen_vectorize.Generate(args[1..]),
```

#### Modification to `StationDungeonGenerator.cs`
`GenerateTopology()` already exists and returns `DungeonState`. Verify it stops before content passes. No structural changes needed — just call it from gen_vectorize.

---

## Phase 6 — Python ML Layer

Directory: `ml/`

### `ml/vocab.py`
```python
import json
from dataclasses import dataclass

SPECIAL = {"PAD": 0, "START": 1, "END": 2, "SEP": 3, "UNK": 4}

class Vocab:
    def __init__(self, path: str):
        with open(path) as f:
            data = json.load(f)
        self.id2token = {t["id"]: t["editorId"] for t in data["tokens"]}
        self.id2token.update({v: k for k, v in SPECIAL.items()})
        self.token2id = {v: k for k, v in self.id2token.items()}
        self.size = max(self.id2token) + 1

    def encode(self, editor_id: str) -> int:
        return self.token2id.get(editor_id, SPECIAL["UNK"])

    def decode(self, token_id: int) -> str:
        return self.id2token.get(token_id, "<UNK>")
```

### `ml/dataset.py`
```python
import json
import torch
from torch.utils.data import Dataset

FIELDS_PER_TOKEN = 6   # [type_id, x_bin, y_bin, z_bin, yaw_id, district_id]
BIN_COUNT = 64

class DungeonDataset(Dataset):
    """
    Each sample: flat integer sequence with START/SEP/END framing.
    Input  = sequence[:-1]
    Target = sequence[1:]  (next-token prediction)
    """
    def __init__(self, jsonl_path: str, max_seq_len: int = 512):
        self.sequences = []
        with open(jsonl_path) as f:
            for line in f:
                seq = json.loads(line)
                flat = self._encode(seq["tokens"])
                if len(flat) <= max_seq_len:
                    self.sequences.append(torch.tensor(flat, dtype=torch.long))

    def _encode(self, tokens: list[dict]) -> list[int]:
        from vocab import SPECIAL
        out = [SPECIAL["START"]]
        for t in tokens:
            out += [t["tid"], t["x"] + 5, t["y"] + 5 + BIN_COUNT,
                    t["z"] + 5 + 2*BIN_COUNT, t["yaw"] + 5 + 3*BIN_COUNT,
                    t["dist"] + 5 + 3*BIN_COUNT + 4]
            out.append(SPECIAL["SEP"])
        out.append(SPECIAL["END"])
        return out

    def __len__(self): return len(self.sequences)
    def __getitem__(self, idx): return self.sequences[idx]

def collate(batch):
    max_len = max(s.size(0) for s in batch)
    padded = torch.zeros(len(batch), max_len, dtype=torch.long)
    mask = torch.zeros(len(batch), max_len, dtype=torch.bool)
    for i, s in enumerate(batch):
        padded[i, :s.size(0)] = s
        mask[i, :s.size(0)] = True
    return padded, mask
```

**Vocabulary offset strategy:** `type_id` uses the real vocab IDs (0-N). Position bins (0-63 each) and yaw (0-3) and district (0-4) are offset into the same flat integer space to avoid collision. Total combined vocab size ≈ vocab.size + 64*3 + 4 + 5 ≈ ~1000 integers.

### `ml/model.py`
```python
import torch
import torch.nn as nn

class DungeonTransformer(nn.Module):
    """
    GPT-style autoregressive transformer.
    Input: flat integer token sequence (combined type+position vocab).
    Output: next-token logits over the same combined vocab.
    """
    def __init__(
        self,
        vocab_size: int,       # combined vocab (type + position bins + special)
        d_model: int = 256,
        n_heads: int = 8,
        n_layers: int = 6,
        max_seq_len: int = 512,
        dropout: float = 0.1,
    ):
        super().__init__()
        self.embedding = nn.Embedding(vocab_size, d_model, padding_idx=0)
        self.pos_encoding = nn.Embedding(max_seq_len, d_model)
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=d_model, nhead=n_heads, dim_feedforward=d_model*4,
            dropout=dropout, batch_first=True, norm_first=True
        )
        self.transformer = nn.TransformerEncoder(encoder_layer, num_layers=n_layers)
        self.head = nn.Linear(d_model, vocab_size)
        self.max_seq_len = max_seq_len

    def forward(self, x: torch.Tensor, mask: torch.Tensor = None):
        # x: (B, T) integer token ids
        B, T = x.shape
        pos = torch.arange(T, device=x.device).unsqueeze(0)
        h = self.embedding(x) + self.pos_encoding(pos)
        # Causal mask
        causal = nn.Transformer.generate_square_subsequent_mask(T, device=x.device)
        h = self.transformer(h, mask=causal,
                             src_key_padding_mask=~mask if mask is not None else None)
        return self.head(h)   # (B, T, vocab_size)

    @torch.no_grad()
    def generate(self, prompt: list[int], max_new: int = 256,
                 temperature: float = 1.0, top_k: int = 50) -> list[int]:
        self.eval()
        seq = torch.tensor([prompt], dtype=torch.long)
        for _ in range(max_new):
            logits = self(seq[:, -self.max_seq_len:])[:, -1, :]
            logits /= temperature
            # Top-k sampling
            topk_vals, topk_idx = torch.topk(logits, top_k)
            probs = torch.softmax(topk_vals, dim=-1)
            next_tok = topk_idx[0, torch.multinomial(probs[0], 1)]
            seq = torch.cat([seq, next_tok.view(1, 1)], dim=1)
            if next_tok.item() == 2:  # END token
                break
        return seq[0].tolist()
```

### `ml/train.py`
```python
import torch
import torch.nn.functional as F
from torch.utils.data import DataLoader, random_split
from model import DungeonTransformer
from dataset import DungeonDataset, collate
from vocab import Vocab
import argparse, json

def train(args):
    vocab = Vocab(args.vocab)
    # Combined vocab size: vocab tokens + position/yaw/district bins + special
    combined_vocab_size = vocab.size + 64*3 + 4 + 5 + 10  # generous headroom

    dataset = DungeonDataset(args.data, max_seq_len=args.max_seq_len)
    train_size = int(0.9 * len(dataset))
    train_ds, val_ds = random_split(dataset, [train_size, len(dataset)-train_size])

    train_loader = DataLoader(train_ds, batch_size=args.batch, shuffle=True,
                              collate_fn=collate)
    val_loader   = DataLoader(val_ds,   batch_size=args.batch, shuffle=False,
                              collate_fn=collate)

    model = DungeonTransformer(combined_vocab_size,
                               d_model=256, n_heads=8, n_layers=6,
                               max_seq_len=args.max_seq_len).to(args.device)

    optimizer = torch.optim.AdamW(model.parameters(), lr=3e-4, weight_decay=0.1)
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=args.epochs)

    for epoch in range(args.epochs):
        model.train()
        total_loss = 0
        for batch, mask in train_loader:
            batch, mask = batch.to(args.device), mask.to(args.device)
            logits = model(batch[:, :-1], mask[:, :-1])     # (B, T-1, V)
            targets = batch[:, 1:]                            # (B, T-1)
            loss = F.cross_entropy(logits.reshape(-1, logits.size(-1)),
                                   targets.reshape(-1), ignore_index=0)
            optimizer.zero_grad(); loss.backward(); optimizer.step()
            total_loss += loss.item()
        scheduler.step()
        print(f"Epoch {epoch+1} | train_loss={total_loss/len(train_loader):.4f}")

        # Validation
        model.eval()
        val_loss = 0
        with torch.no_grad():
            for batch, mask in val_loader:
                batch, mask = batch.to(args.device), mask.to(args.device)
                logits = model(batch[:, :-1], mask[:, :-1])
                val_loss += F.cross_entropy(logits.reshape(-1, logits.size(-1)),
                                            batch[:, 1:].reshape(-1), ignore_index=0).item()
        print(f"          val_loss={val_loss/len(val_loader):.4f}")

        # Checkpoint
        torch.save(model.state_dict(), f"{args.outdir}/model_epoch{epoch+1}.pt")

    # Export to ONNX
    dummy = torch.zeros(1, 32, dtype=torch.long).to(args.device)
    torch.onnx.export(model, (dummy,), f"{args.outdir}/model.onnx",
                      input_names=["tokens"], output_names=["logits"],
                      dynamic_axes={"tokens": {1: "seq_len"}, "logits": {1: "seq_len"}})
    print(f"ONNX model exported to {args.outdir}/model.onnx")

if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--data",        required=True)
    p.add_argument("--vocab",       required=True)
    p.add_argument("--outdir",      default="ml_output")
    p.add_argument("--epochs",      type=int, default=50)
    p.add_argument("--batch",       type=int, default=32)
    p.add_argument("--max_seq_len", type=int, default=512)
    p.add_argument("--device",      default="cuda" if torch.cuda.is_available() else "cpu")
    train(p.parse_args())
```

### `ml/generate.py`
```python
# Interactive generation + decoding back to readable room sequence
from model import DungeonTransformer
from vocab import Vocab, SPECIAL
import torch, json

def generate_dungeon(model_path, vocab_path, faction="spacer", temperature=0.9):
    vocab = Vocab(vocab_path)
    combined_vocab_size = vocab.size + 64*3 + 4 + 5 + 10
    model = DungeonTransformer(combined_vocab_size)
    model.load_state_dict(torch.load(model_path, map_location="cpu"))

    prompt = [SPECIAL["START"]]
    output = model.generate(prompt, max_new=300, temperature=temperature)

    # Decode: group flat integers back into token structs (every 7 ints: type+3pos+yaw+dist+SEP)
    print("Generated room sequence:")
    i = 1  # skip START
    room_num = 0
    while i < len(output) - 1:
        if output[i] == SPECIAL["END"]: break
        if len(output) - i < 7: break
        tid, xb, yb, zb, yaw, dist = output[i:i+6]
        sep = output[i+6]
        editor_id = vocab.decode(tid)
        print(f"  Room {room_num:02d}: {editor_id:40s} x={xb-5} y={yb-5-64} z={zb-5-128} yaw={yaw} dist={dist}")
        room_num += 1
        i += 7  # skip SEP

if __name__ == "__main__":
    import sys
    generate_dungeon(sys.argv[1], sys.argv[2])
```

---

## Phase 7 — C# Inference Wrapper

#### `Retrograde.Library/Vectorization/ModelInference.cs`
```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class ModelInference : IDisposable
{
    private readonly InferenceSession _session;
    private readonly TokenVocab _vocab;
    private readonly PositionQuantizer _quantizer;

    public ModelInference(string onnxPath, TokenVocab vocab, PositionQuantizer quantizer)
    {
        _session  = new InferenceSession(onnxPath);
        _vocab    = vocab;
        _quantizer = quantizer;
    }

    // Given a partial sequence, predict the most likely next token
    public DungeonToken? SuggestNextToken(
        List<DungeonToken> context,
        float temperature = 1.0f,
        int topK = 20)
    {
        var flat = EncodeFlat(context);
        var tensor = new DenseTensor<long>(flat.Select(x => (long)x).ToArray(),
                                           new[] { 1, flat.Count });
        var inputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("tokens", tensor) };
        using var results = _session.Run(inputs);
        var logits = results[0].AsEnumerable<float>().ToArray();

        // logits shape: (1, seq_len, vocab_size) — take last position
        int vocabSize = logits.Length / flat.Count;
        var lastLogits = logits.Skip((flat.Count - 1) * vocabSize).Take(vocabSize).ToArray();

        int nextTypeId = SampleTopK(lastLogits, topK, temperature);
        return new DungeonToken
        {
            TypeId = nextTypeId,
            EditorId = _vocab.Decode(nextTypeId),
            Category = _vocab.CategoryFor(nextTypeId),
        };
    }

    private List<int> EncodeFlat(List<DungeonToken> tokens)
    {
        var out = new List<int> { SpecialTokens.START };
        foreach (var t in tokens)
        {
            out.Add(t.TypeId);
            out.Add(t.XBin + SpecialTokens.FIRST_REAL);
            out.Add(t.YBin + SpecialTokens.FIRST_REAL + 64);
            out.Add(t.ZBin + SpecialTokens.FIRST_REAL + 128);
            out.Add(t.YawSteps + SpecialTokens.FIRST_REAL + 192);
            out.Add(t.DistrictId + SpecialTokens.FIRST_REAL + 196);
            out.Add(SpecialTokens.SEP);
        }
        return out;
    }

    private static int SampleTopK(float[] logits, int k, float temperature)
    {
        var scaled = logits.Select((l, i) => (l / temperature, i))
                           .OrderByDescending(x => x.Item1).Take(k).ToList();
        var probs = Softmax(scaled.Select(x => x.Item1).ToArray());
        float r = Random.Shared.NextSingle();
        float cumSum = 0;
        for (int i = 0; i < probs.Length; i++)
        {
            cumSum += probs[i];
            if (r < cumSum) return scaled[i].i;
        }
        return scaled[0].i;
    }

    private static float[] Softmax(float[] x)
    {
        var exp = x.Select(MathF.Exp).ToArray();
        float sum = exp.Sum();
        return exp.Select(e => e / sum).ToArray();
    }

    public void Dispose() => _session.Dispose();
}
```

**NuGet dependency to add:** `Microsoft.ML.OnnxRuntime` (CPU) or `Microsoft.ML.OnnxRuntime.Gpu` (GPU).

---

## Phase 8 — Neural Topology Pass

#### `Retrograde.Library/Passes/SpaceStation/NeuralTopologyPass.cs`
```csharp
public class NeuralTopologyPass : IGenPass
{
    private readonly ModelInference _model;
    private readonly TokenVocab _vocab;
    private readonly int _targetRoomCount;

    public NeuralTopologyPass(ModelInference model, TokenVocab vocab, int targetRoomCount = 15)
    {
        _model = model;
        _vocab = vocab;
        _targetRoomCount = targetRoomCount;
    }

    public void Run(DungeonState state)
    {
        var context = new List<DungeonToken>();

        for (int attempt = 0; attempt < _targetRoomCount * 3; attempt++)
        {
            if (state.placedRooms.Count >= _targetRoomCount) break;

            // Get model suggestion
            var suggestion = _model.SuggestNextToken(context);
            if (suggestion == null) break;

            // Validate: does this EditorID have an available prefab that can connect?
            var candidates = state.AvailableConnectors()
                .Where(c => CanConnect(suggestion.EditorId, c))
                .ToList();

            if (candidates.Count == 0)
                continue;  // model suggestion invalid, skip (don't retrain, just try next)

            // Place room via existing placement logic
            var connector = candidates[Random.Shared.Next(candidates.Count)];
            PlaceRoom(state, suggestion.EditorId, connector);

            // Update context for next prediction
            var last = state.placedRooms.Last();
            context.Add(TokenFromPlacedRoom(last));
        }

        // Fallback: if model didn't fill enough rooms, run rule-based pass
        if (state.placedRooms.Count < _targetRoomCount / 2)
        {
            Console.WriteLine("[NeuralTopologyPass] Model underperformed, falling back to TrunkTopologyPass");
            new TrunkTopologyPass().Run(state);
        }
    }

    private DungeonToken TokenFromPlacedRoom(PlacedRoom room) => new()
    {
        TypeId   = _vocab.Encode(room.Prefab.PrefabEditorId),
        YawSteps = room.YawSteps,
        Category = _vocab.CategoryFor(_vocab.Encode(room.Prefab.PrefabEditorId)),
        EditorId = room.Prefab.PrefabEditorId,
    };

    // ... PlaceRoom(), CanConnect() delegate to existing RoomUtils / ConnectorUtils
}
```

---

## Phased Rollout & Go/No-Go Criteria

### Phase 1 — Vocabulary & Extraction (Week 1)
Deliverables: `TokenVocab`, `DungeonToken`, `DungeonSequence`, `TopologyExtractor`, `SequenceSerializer`, `gen_vectorize`

**Go criteria:**
- `gen_vectorize --build-vocab` produces `vocab.json` with 400+ tokens
- `gen_vectorize 100` runs successfully, produces `sequences.jsonl` with 80+ valid sequences
- Each sequence has 8–25 tokens (typical station room count)
- `SequenceSerializer.PrintStats()` shows reasonable position/rotation distributions

### Phase 2 — Training Data Generation (Week 1–2)
Deliverables: 2000+ generated sequences across factions (spacer, crimson, varuun)

**Go criteria:**
- Sequences for all 3 major faction styles generated
- Sequence length distribution is unimodal, no degenerate sequences (length < 5 or > 40)
- Position bins show uniform spread across the quantizer range (no clipping > 2%)

### Phase 3 — Model Training (Week 2–3)
Deliverables: trained `.pt` weights + exported `model.onnx`

**Go criteria:**
- Validation loss < 1.5 nats (random baseline ≈ ln(vocab_size) ≈ 6.5)
- `generate.py` produces sequences of reasonable length (8–25 rooms) > 80% of the time
- Generated sequences have same district distribution as training data (trunk-heavy with hab/ore satellites)

### Phase 4 — C# Inference Integration (Week 3–4)
Deliverables: `ModelInference.cs`, `NeuralTopologyPass.cs`

**Go criteria:**
- `NeuralTopologyPass` produces valid dungeons (all open connectors sealable)
- At least 70% of rooms come from model suggestions (not fallback)
- End-to-end generation time increase < 2× vs rule-based baseline

---

## Summary of All New Files

```
Retrograde.Library/Vectorization/
  TokenCategory.cs
  DungeonToken.cs
  DungeonSequence.cs
  TokenVocab.cs
  PositionQuantizer.cs
  TopologyExtractor.cs
  ObjectExtractor.cs
  SequenceSerializer.cs
  ModelInference.cs                      (Phase 4)

Retrograde.Library/Passes/SpaceStation/
  NeuralTopologyPass.cs                  (Phase 4)

gen_vectorize.cs
scripts/gen_vectorize.sh

ml/
  vocab.py
  dataset.py
  model.py
  train.py
  generate.py
  requirements.txt
```

**Requirements.txt:**
```
torch>=2.2.0
numpy>=1.26
```

**NuGet additions (FrankyCLI.csproj):**
```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.18.0" />
```

## Modifications to Existing Files

| File | Change |
|------|--------|
| `Program.cs` | Add `"gen_vectorize" => gen_vectorize.Generate(args[1..])` dispatch case |
| `StationDungeonGenerator.cs` | Verify `GenerateTopology()` is publicly callable and returns `DungeonState` with `placedRooms` populated. No structural change expected. |
| `FrankyCLI.csproj` | Add `Microsoft.ML.OnnxRuntime` package reference (Phase 4 only) |
