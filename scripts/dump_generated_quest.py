"""
Dumps the rg_audiolog_quest record from outlaws02.esm to compare with vanilla.
"""
import struct, os, zlib

ESM_PATH = "C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data/outlaws02.esm"
TARGET_EDID = "rg_audiolog_quest"

COMPRESSED_FLAG = 0x00040000

def read4(f): return struct.unpack('<I', f.read(4))[0]
def read2(f): return struct.unpack('<H', f.read(2))[0]

def decode_flags(v):
    flag_names = {
        0x001: 'StartGameEnabled', 0x002: 'Completed', 0x004: 'AddIdleTopicToHello',
        0x008: 'AllowRepeatedStages', 0x010: 'StartsEnabled', 0x020: 'DisplaysInHud',
        0x040: 'Failed', 0x080: 'StageWait', 0x100: 'RunOnce',
        0x200: 'ExcludeFromDialogExport', 0x400: 'WarnOnAliasFillFailure',
        0x800: 'Active', 0x1000: 'RepeatsConditions', 0x2000: 'KeepInstance',
        0x4000: 'WantDormant', 0x8000: 'HasDialogueData',
        0x100000: 'bit20(UnknownExtra)',
    }
    return [name for bit, name in flag_names.items() if v & bit] or ['(none)']

def parse_subrecords_full(data):
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

def decode_major_flags(raw):
    quest_bits = {
        0x0001: 'ESM', 0x0004: 'NotPlayable', 0x0020: 'Deleted',
        0x0800: 'InitiallyDisabled', 0x1000: 'Ignored', 0x4000: 'PartialForm',
        0x8000: 'VisibleWhenDistant', 0x20000: 'Dangerous', 0x40000: 'Compressed',
        0x80000: 'CantWait',
    }
    return [name for bit, name in quest_bits.items() if raw & bit] or ['(none)']

if not os.path.exists(ESM_PATH):
    print(f"File not found: {ESM_PATH}")
    exit(1)

found = False
with open(ESM_PATH, 'rb') as f:
    tag = f.read(4)
    rec_size = read4(f)
    f.seek(24 + rec_size)

    total = os.path.getsize(ESM_PATH)

    while f.tell() < total - 24 and not found:
        pos = f.tell()
        raw_tag = f.read(4)
        if len(raw_tag) < 4: break

        tag = raw_tag.decode('ascii', errors='replace')

        if tag == 'GRUP':
            f.read(4)
            f.read(16)
            continue

        data_size = read4(f)
        major_flags = read4(f)
        form_id = read4(f)
        f.read(4)
        form_version = read2(f)
        version2 = read2(f)
        rec_start = f.tell()

        if tag == 'QUST':
            raw_data = f.read(data_size)
            if major_flags & COMPRESSED_FLAG:
                raw_data = zlib.decompress(raw_data[4:], 15)

            subs = parse_subrecords_full(raw_data)

            edid = ''
            for t, v in subs:
                if t == 'EDID':
                    edid = v.decode('utf-8', errors='replace').rstrip('\x00')
                    break

            if edid == TARGET_EDID:
                found = True
                print(f"=== QUST 0x{form_id:06X}  EditorID={edid} ===")
                print(f"  File offset:          {pos}")
                print(f"  MajorRecordFlagsRaw:  0x{major_flags:08X}  = {decode_major_flags(major_flags)}")
                print(f"  FormVersion:          {form_version}")
                print(f"  Version2:             {version2}")
                print(f"  Total subrecords:     {len(subs)}")
                print()
                for t, v in subs:
                    if t == 'EDID':
                        print(f"  EDID: {v.decode('utf-8', errors='replace').rstrip(chr(0))!r}")
                    elif t == 'DNAM':
                        if len(v) >= 12:
                            flags4 = struct.unpack_from('<I', v, 0)[0]
                            pri = v[4]
                            type4 = struct.unpack_from('<I', v, 8)[0]
                            print(f"  DNAM ({len(v)}B): {v.hex()}")
                            print(f"    Flags:    0x{flags4:08X} = {decode_flags(flags4)}")
                            print(f"    Priority: {pri}")
                            print(f"    Type:     {type4}")
                        else:
                            print(f"  DNAM ({len(v)}B): {v.hex()}")
                    elif t == 'FULL':
                        print(f"  FULL: {v.decode('utf-8', errors='replace').rstrip(chr(0))!r}")
                    elif t == 'ANAM':
                        print(f"  ANAM ({len(v)}B): {v.hex()}")
                    elif t == 'NEXT':
                        print(f"  NEXT ({len(v)}B): (separator)")
                    else:
                        if len(v) <= 16:
                            print(f"  {t} ({len(v)}B): {v.hex()}")
                        else:
                            print(f"  {t} ({len(v)}B)")
        else:
            f.seek(rec_start + data_size)

if not found:
    print(f"Quest '{TARGET_EDID}' not found in {ESM_PATH}")
