using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FrankyCLI
{
    // Conform a generated part's records to the shape every RENDERING part has, for the two
    // fields MUTAGEN CANNOT AUTHOR.
    //
    //   conform <modname> <prefix> <item>
    //
    // WHY THIS EXISTS. A part generated entirely by gen_shipstruct builds, attaches, is buyable,
    // is paintable -- and DRAWS NOTHING in the ship builder. Two subrecords are missing, and
    // neither has a property on any Mutagen type, so no amount of fixing the generator's record
    // objects can produce them:
    //
    //   GBFM  STRV = "BGSMod_Template_Component"
    //         The string that TYPES the ANAM template link. Without it the GBFM carries a
    //         template reference of no declared kind -- a half-written component.
    //   CELL  XCLL's final word = 3   (the PackIn's storage cell)
    //         The ship builder renders the module OUT OF that cell. CellLighting exposes
    //         DirectionalFade/FogPower/FogMax/Near+FarHeightRange/Unknown1 and stops; the last
    //         word has no name at all.
    //
    // Mutagen round-trips both faithfully, which is exactly why this went unnoticed for
    // fourteen parts: every one of them passed through a Creation Kit save (to repoint a swap,
    // place a flare, swap a label) and the CK writes both fields. atsd_panelvent01_port,
    // 2026-07-30, was the first part ever taken to the glass without one. It was invisible.
    //
    // VALUES ARE CONSTANTS, AND THEY ARE MEASURED, NOT CHOSEN. STRV is byte-identical on all
    // fourteen GenericBaseForms in avontechstardust.esm INCLUDING the vanilla
    // Asp_Destroyer_B_Deimos_Hoplite_Aegis_Template that shipped with the game. The XCLL word is
    // 3 on every cell whose part renders and 0 on every generated one. Using constants rather
    // than copying from a donor means a mod with no known-good part yet can still conform.
    //
    // IDEMPOTENT: a record already carrying the field is left untouched and reported as such.
    //
    // Called at the end of gen_shipstruct so a freshly built part is correct without anyone
    // remembering a second step -- the gen_setbounds pattern (a reusable core wired into the
    // generator AND exposed standalone for records that already exist).
    class gen_conform
    {
        const string TemplateComponent = "BGSMod_Template_Component";
        const uint CellLightingTailWord = 3;
        const int XcllTailOffset = 104;      // XCLL is 108 bytes; the final u32
        const uint CompressedFlag = 0x00040000;

        public static int Generate(string[] args)
        {
            // args: [modname, "conform", prefix, item]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: conform <modname> <prefix> <item>");
                return 1;
            }
            string modname = args[0], prefix = args[2], item = args[3];
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            string datapath;
            using (var env = GameEnvironment.Typical
                       .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
            }
            return Apply(Path.Combine(datapath, modname + ".esm"), prefix, item, verbose: true);
        }

        /// <summary>
        /// Splice the two unauthorable fields into <paramref name="item"/>'s records, in place.
        /// Returns 0 on success (including "nothing to do"), 1 on a refusal.
        /// </summary>
        public static int Apply(string pluginPath, string prefix, string item, bool verbose)
        {
            if (!File.Exists(pluginPath))
            {
                Console.WriteLine("conform: " + pluginPath + " not found");
                return 1;
            }
            var raw = new List<byte>(File.ReadAllBytes(pluginPath));
            int before = raw.Count;

            // VALIDATE EVERYTHING FIRST, MUTATE NOTHING. The first cut interleaved the two and
            // printed "+ STRV" for a change that was then discarded when a LATER lookup failed --
            // a success line over an untouched disk, which is the exact trap this lane has been
            // bitten by twice (a removerecord that reported success while the CK held the file,
            // and a deploy verified on mtime). A tool that can print a change it did not make is
            // worse than one that fails, because the transcript lies. Nothing below writes until
            // every lookup has succeeded.
            var idx = Index(raw);

            string gbfmId = (prefix + "_gbfm_" + item).ToLowerInvariant();
            if (!idx.TryGetValue(gbfmId, out var gbfm))
            {
                Console.WriteLine("conform: no GenericBaseForm '" + prefix + "_gbfm_" + item + "' -- nothing written");
                return 1;
            }
            if (IsCompressed(raw, gbfm.Offset))
            {
                Console.WriteLine("conform: " + gbfmId + " is a COMPRESSED record -- refusing, nothing written.");
                Console.WriteLine("  The Creation Kit writes records compressed; FrankyCLI's writers set");
                Console.WriteLine("  IsCompressed=false, so running any set* command on this plugin first");
                Console.WriteLine("  will inflate it and this will then work.");
                return 1;
            }
            bool needStrv = FindSub(raw, gbfm, "STRV") == null;
            Sub? anam = needStrv ? FindSub(raw, gbfm, "ANAM") : null;
            if (needStrv && anam == null)
            {
                Console.WriteLine("conform: " + gbfmId + " has no ANAM template link to anchor STRV to"
                                  + " -- nothing written");
                return 1;
            }

            // The cell is reached through the PackIn's CNAM, NOT by EditorID: the CK renames a
            // generated cell to PackIn<...>StorageCell on save, so the name is not stable.
            string pkinId = (prefix + "_pkn_" + item).ToLowerInvariant();
            if (!idx.TryGetValue(pkinId, out var pkin))
            {
                Console.WriteLine("conform: no PackIn '" + prefix + "_pkn_" + item + "' -- nothing written");
                return 1;
            }
            var cnam = FindSub(raw, pkin, "CNAM");
            if (cnam == null)
            {
                Console.WriteLine("conform: " + pkinId + " has no CNAM cell link -- nothing written");
                return 1;
            }
            uint cellFid = BitConverter.ToUInt32(raw.GetRange(cnam.Value.Offset + 6, 4).ToArray(), 0);
            var cell = ByFormKey(raw, cellFid);
            if (cell == null)
            {
                Console.WriteLine(string.Format("conform: cell 0x{0:X8} not found -- nothing written", cellFid));
                return 1;
            }
            if (IsCompressed(raw, cell.Value.Offset))
            {
                Console.WriteLine(string.Format("conform: cell 0x{0:X8} is COMPRESSED -- refusing, nothing "
                    + "written (run any set* command first to inflate it)", cellFid));
                return 1;
            }
            var xcll = FindSub(raw, cell.Value, "XCLL");
            if (xcll == null || xcll.Value.Length - 6 < XcllTailOffset + 4)
            {
                Console.WriteLine("conform: cell XCLL missing or too short -- refusing, nothing written");
                return 1;
            }

            // ---- everything resolved; now mutate --------------------------------------
            var report = new List<string>();
            int changes = 0;

            // XCLL first: an in-place overwrite, so it cannot move the STRV anchor.
            int tailAt = xcll.Value.Offset + 6 + XcllTailOffset;
            uint was = BitConverter.ToUInt32(raw.GetRange(tailAt, 4).ToArray(), 0);
            if (was == CellLightingTailWord)
                report.Add(string.Format("  cell 0x{0:X8}: XCLL tail already {1} -- left as is", cellFid, was));
            else
            {
                var v = BitConverter.GetBytes(CellLightingTailWord);
                for (int i = 0; i < 4; i++) raw[tailAt + i] = v[i];
                changes++;
                report.Add(string.Format("  cell 0x{0:X8}: XCLL tail {1} -> {2}", cellFid, was, CellLightingTailWord));
            }

            if (!needStrv)
                report.Add("  " + gbfmId + ": STRV already present -- left as is");
            else
            {
                var body = Encoding.ASCII.GetBytes(TemplateComponent).Concat(new byte[] { 0 }).ToArray();
                var sub = new List<byte>();
                sub.AddRange(Encoding.ASCII.GetBytes("STRV"));
                sub.AddRange(BitConverter.GetBytes((ushort)body.Length));
                sub.AddRange(body);
                raw.InsertRange(anam.Value.Offset + anam.Value.Length, sub);
                Grow(raw, gbfm, sub.Count);
                changes++;
                report.Add("  " + gbfmId + ": + STRV \"" + TemplateComponent + "\"");
            }

            if (changes == 0)
            {
                if (verbose)
                {
                    foreach (var l in report) Console.WriteLine(l);
                    Console.WriteLine("conform: nothing to do -- already conformed.");
                }
                return 0;
            }

            // SELF-CHECK BEFORE WRITING. Inserting bytes means growing the record AND every group
            // that contains it; miss one and the file is structurally broken while the tool still
            // reports the right change and the right byte count. That happened on the first cut
            // (the top-level group was omitted from the chain), and nothing about the output
            // looked wrong. So the tool now proves its own arithmetic: walk every top-level group
            // by its declared size and land exactly on EOF.
            string desync = WalkCheck(raw);
            if (desync != null)
            {
                Console.WriteLine("conform: REFUSING TO WRITE -- " + desync);
                Console.WriteLine("  The size arithmetic is wrong; the plugin on disk is untouched.");
                return 1;
            }

            File.WriteAllBytes(pluginPath, raw.ToArray());
            if (verbose)
            {
                foreach (var l in report) Console.WriteLine(l);
                Console.WriteLine(string.Format("conform: {0} change(s) WRITTEN, {1} -> {2} bytes, "
                    + "group sizes verified, FormIDs unchanged.", changes, before, raw.Count));
            }
            return 0;
        }

        // ------------------------------------------------------------------ byte plumbing

        struct Rec
        {
            public string Sig;
            public int Offset;      // start of the record header
            public int Length;      // 24 + declared data size
            public List<int> Grups; // offsets of every enclosing GRUP header
        }

        struct Sub
        {
            public int Offset;      // start of the subrecord header
            public int Length;      // 6 + declared length
        }

        static uint U32(List<byte> b, int at) => BitConverter.ToUInt32(new[] { b[at], b[at + 1], b[at + 2], b[at + 3] }, 0);
        static ushort U16(List<byte> b, int at) => BitConverter.ToUInt16(new[] { b[at], b[at + 1] }, 0);
        static string Sig(List<byte> b, int at) => Encoding.ASCII.GetString(new[] { b[at], b[at + 1], b[at + 2], b[at + 3] });

        static bool IsCompressed(List<byte> b, int recOffset) => (U32(b, recOffset + 8) & CompressedFlag) != 0;

        static Dictionary<string, Rec> Index(List<byte> raw)
        {
            var map = new Dictionary<string, Rec>();
            void Walk(int off, int end, List<int> chain)
            {
                while (off < end)
                {
                    string sig = Sig(raw, off);
                    int size = (int)U32(raw, off + 4);
                    if (sig == "GRUP")
                    {
                        var next = new List<int>(chain) { off };
                        Walk(off + 24, off + size, next);
                        off += size;
                    }
                    else
                    {
                        var rec = new Rec { Sig = sig, Offset = off, Length = 24 + size, Grups = new List<int>(chain) };
                        // EDID is the first subrecord when present; a compressed record has none readable
                        if (!IsCompressed(raw, off) && 24 + 6 <= rec.Length && Sig(raw, off + 24) == "EDID")
                        {
                            int len = U16(raw, off + 28);
                            string eid = Encoding.ASCII.GetString(raw.GetRange(off + 30, len).ToArray()).TrimEnd('\0');
                            map[eid.ToLowerInvariant()] = rec;
                        }
                        off += 24 + size;
                    }
                }
            }
            int hdr = (int)U32(raw, 4);
            int pos = 24 + hdr;
            while (pos < raw.Count)
            {
                int size = (int)U32(raw, pos + 4);
                // The TOP-LEVEL group must be in the chain too: growing a record grows every
                // group that CONTAINS it, and the outermost one contains it as much as any
                // inner one does. Omitting it left the outer size short by exactly the inserted
                // length, so every byte after that group desynced -- caught by the bite test's
                // whole-file walk, not by the tool appearing to work (it reported the right
                // change and the right byte count over a structurally broken file).
                if (Sig(raw, pos) == "GRUP") Walk(pos + 24, pos + size, new List<int> { pos });
                pos += size;
            }
            return map;
        }

        static Rec? ByFormKey(List<byte> raw, uint fid)
        {
            Rec? hit = null;
            void Walk(int off, int end, List<int> chain)
            {
                while (off < end && hit == null)
                {
                    string sig = Sig(raw, off);
                    int size = (int)U32(raw, off + 4);
                    if (sig == "GRUP")
                    {
                        var next = new List<int>(chain) { off };
                        Walk(off + 24, off + size, next);
                        off += size;
                    }
                    else
                    {
                        if (U32(raw, off + 12) == fid)
                            hit = new Rec { Sig = sig, Offset = off, Length = 24 + size, Grups = new List<int>(chain) };
                        off += 24 + size;
                    }
                }
            }
            int hdr = (int)U32(raw, 4);
            int pos = 24 + hdr;
            while (pos < raw.Count && hit == null)
            {
                int size = (int)U32(raw, pos + 4);
                // The TOP-LEVEL group must be in the chain too: growing a record grows every
                // group that CONTAINS it, and the outermost one contains it as much as any
                // inner one does. Omitting it left the outer size short by exactly the inserted
                // length, so every byte after that group desynced -- caught by the bite test's
                // whole-file walk, not by the tool appearing to work (it reported the right
                // change and the right byte count over a structurally broken file).
                if (Sig(raw, pos) == "GRUP") Walk(pos + 24, pos + size, new List<int> { pos });
                pos += size;
            }
            return hit;
        }

        static Sub? FindSub(List<byte> raw, Rec rec, string want)
        {
            int p = rec.Offset + 24;
            int end = rec.Offset + rec.Length;
            while (p < end)
            {
                string sig = Sig(raw, p);
                int len = 6 + U16(raw, p + 4);
                if (sig == want) return new Sub { Offset = p, Length = len };
                p += len;
            }
            return null;
        }

        /// <summary>Null if every top-level group's declared size chains exactly to EOF.</summary>
        static string WalkCheck(List<byte> raw)
        {
            int pos = 24 + (int)U32(raw, 4);
            int groups = 0;
            while (pos < raw.Count)
            {
                if (pos + 8 > raw.Count) return "truncated group header at " + pos;
                if (Sig(raw, pos) != "GRUP")
                    return string.Format("desync at {0} after {1} group(s): expected GRUP, found '{2}'",
                        pos, groups, Sig(raw, pos));
                int size = (int)U32(raw, pos + 4);
                if (size <= 0) return "non-positive group size at " + pos;
                pos += size;
                groups++;
            }
            return pos == raw.Count ? null
                : string.Format("groups end at {0}, file is {1} bytes", pos, raw.Count);
        }

        static void Grow(List<byte> raw, Rec rec, int n)
        {
            uint size = U32(raw, rec.Offset + 4) + (uint)n;
            var b = BitConverter.GetBytes(size);
            for (int i = 0; i < 4; i++) raw[rec.Offset + 4 + i] = b[i];
            foreach (int g in rec.Grups)
            {
                uint gs = U32(raw, g + 4) + (uint)n;
                var gb = BitConverter.GetBytes(gs);
                for (int i = 0; i < 4; i++) raw[g + 4 + i] = gb[i];
            }
        }
    }
}
