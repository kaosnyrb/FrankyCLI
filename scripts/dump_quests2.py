"""
Checks ANAM and NEXT subrecord contents in vanilla AudioLog quests.
Also checks a 4th variant: AudioLogsQuest_LVC (0x08612C) which has 9 topics.
"""
import struct, os, zlib

ESM = "C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data/Starfield.esm"
# AudioLogsQuest_LVC = 0x08612C (9 topics, small quest)
TARGET_IDS = {0x012996, 0x0251D2, 0x08612C}

COMPRESSED_FLAG = 0x00040000

def read4(f): return struct.unpack('<I', f.read(4))[0]
def read2(f): return struct.unpack('<H', f.read(2))[0]

def parse_subrecords_full(data):
    """Parse all subrecords, returning list of (tag, bytes) in order"""
    result = []
    i = 0
    while i < len(data):
        if i + 6 > len(data): break
        tag = data[i:i+4].decode('ascii', errors='replace')
        size = struct.unpack_from('<H', data, i+4)[0]
        i += 6
        val = data[i:i+size]
        i += size
        result.append((tag, val))
    return result

found = {}

with open(ESM, 'rb') as f:
    tag = f.read(4)
    rec_size = read4(f)
    f.seek(24 + rec_size)

    total = os.path.getsize(ESM)

    while f.tell() < total - 24 and len(found) < len(TARGET_IDS):
        pos = f.tell()
        raw_tag = f.read(4)
        if len(raw_tag) < 4: break

        tag = raw_tag.decode('ascii', errors='replace')

        if tag == 'GRUP':
            f.read(4)  # grp_size (skip, don't use it)
            f.read(16)
            continue

        data_size = read4(f)
        major_flags = read4(f)
        form_id = read4(f)
        f.read(4)  # version_control
        form_version = read2(f)
        version2 = read2(f)

        rec_start = f.tell()

        if form_id in TARGET_IDS and tag == 'QUST':
            raw_data = f.read(data_size)

            if major_flags & COMPRESSED_FLAG:
                raw_data = zlib.decompress(raw_data[4:], 15)

            subs = parse_subrecords_full(raw_data)

            print(f"\n=== QUST 0x{form_id:06X} FormVersion={form_version} Version2={version2} ===")
            print(f"  Total subrecords: {len(subs)}")
            for tag2, val in subs:
                if tag2 == 'EDID':
                    print(f"  EDID: {val.decode('utf-8', errors='replace').rstrip(chr(0))!r}")
                elif tag2 == 'DNAM':
                    print(f"  DNAM ({len(val)}B): {val.hex()}  = flags=0x{struct.unpack_from('<I',val,0)[0]:08X} pri={val[4]} type={struct.unpack_from('<I',val,8)[0] if len(val)>=12 else 'n/a'}")
                elif tag2 == 'ANAM':
                    print(f"  ANAM ({len(val)}B): {val.hex()!r} = {val}")
                elif tag2 == 'NEXT':
                    print(f"  NEXT ({len(val)}B): (separator)")
                elif tag2 == 'FULL':
                    print(f"  FULL ({len(val)}B): {val.decode('utf-8', errors='replace').rstrip(chr(0))!r}")
                else:
                    print(f"  {tag2} ({len(val)}B)")

            found[form_id] = True
        else:
            f.seek(rec_start + data_size)

print(f"\nFound {len(found)}/{len(TARGET_IDS)} targets")
