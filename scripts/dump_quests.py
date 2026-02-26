import struct, os, zlib

ESM = "C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data/Starfield.esm"
TARGET_IDS = {0x0251D2, 0x012996, 0x20C63F}

def read4(f): return struct.unpack('<I', f.read(4))[0]
def read2(f): return struct.unpack('<H', f.read(2))[0]

def parse_subrecords(data):
    result = {}
    i = 0
    while i < len(data):
        if i + 6 > len(data): break
        tag = data[i:i+4].decode('ascii', errors='replace')
        size = struct.unpack_from('<H', data, i+4)[0]
        i += 6
        val = data[i:i+size]
        i += size
        if tag not in result:
            result[tag] = []
        result[tag].append(val)
    return result

def decode_flags(flags_val):
    flag_names = {
        0x001: 'StartGameEnabled', 0x002: 'Completed', 0x004: 'AddIdleTopicToHello',
        0x008: 'AllowRepeatedStages', 0x010: 'StartsEnabled', 0x020: 'DisplaysInHud',
        0x040: 'Failed', 0x080: 'StageWait', 0x100: 'RunOnce',
        0x200: 'ExcludeFromDialogExport', 0x400: 'WarnOnAliasFillFailure',
        0x800: 'Active', 0x1000: 'RepeatsConditions', 0x2000: 'KeepInstance',
        0x4000: 'WantDormant', 0x8000: 'HasDialogueData',
    }
    return [name for bit, name in flag_names.items() if flags_val & bit] or ['(none)']

def decode_type(t):
    types = ['None','MainQuest','BrotherhoodOfSteel','Institute','Minutemen','Railroad',
             'Misc','SideQuests','DLC01','DLC02','DLC03','DLC04','DLC05','DLC06','DLC07']
    return types[t] if t < len(types) else f'Unknown({t})'

def decode_major_flags(raw):
    quest_bits = {
        0x0001: 'ESM', 0x0004: 'NotPlayable', 0x0020: 'Deleted',
        0x0800: 'InitiallyDisabled', 0x1000: 'Ignored', 0x4000: 'PartialForm',
        0x8000: 'VisibleWhenDistant', 0x20000: 'Dangerous', 0x40000: 'Compressed',
        0x80000: 'CantWait',
    }
    return [name for bit, name in quest_bits.items() if raw & bit] or ['(none)']

COMPRESSED_FLAG = 0x00040000
found = {}

with open(ESM, 'rb') as f:
    tag = f.read(4)
    if tag != b'TES4':
        print("Not an ESM!")
        exit(1)
    rec_size = read4(f)
    f.seek(24 + rec_size)

    total = os.path.getsize(ESM)
    scanned = 0

    while f.tell() < total - 24 and len(found) < len(TARGET_IDS):
        pos = f.tell()
        raw_tag = f.read(4)
        if len(raw_tag) < 4: break

        tag = raw_tag.decode('ascii', errors='replace')

        if tag == 'GRUP':
            grp_size = read4(f)
            f.read(16)
            continue

        data_size = read4(f)
        major_flags = read4(f)
        form_id = read4(f)
        version_control = read4(f)
        form_version = read2(f)
        version2 = read2(f)

        rec_start = f.tell()

        if form_id in TARGET_IDS and tag == 'QUST':
            raw_data = f.read(data_size)

            if major_flags & COMPRESSED_FLAG:
                uncomp_size = struct.unpack_from('<I', raw_data, 0)[0]
                raw_data = zlib.decompress(raw_data[4:], 15)

            subs = parse_subrecords(raw_data)

            dnam = subs.get('DNAM', [b''])[0]
            if len(dnam) >= 2:
                q_flags = struct.unpack_from('<H', dnam, 0)[0]
                q_priority = dnam[2] if len(dnam) > 2 else 0
                q_unused = dnam[3] if len(dnam) > 3 else 0
                q_type = struct.unpack_from('<H', dnam, 4)[0] if len(dnam) >= 6 else 0
            else:
                q_flags = q_priority = q_unused = q_type = 0

            full_name = ''
            if 'FULL' in subs:
                try:
                    full_name = subs['FULL'][0].decode('utf-8', errors='replace').rstrip('\x00')
                except: pass

            has_vmad = 'VMAD' in subs
            vmad_size = len(subs.get('VMAD', [b''])[0])

            fltr = subs.get('FLTR', [None])[0]
            fltr_str = fltr.decode('utf-8', errors='replace').rstrip('\x00') if fltr else None

            alias_markers = len(subs.get('ALST', [])) + len(subs.get('ALLS', []))
            stage_count = len(subs.get('INDX', []))
            obj_count = len(subs.get('QOBJ', []))
            ctda_count = len(subs.get('CTDA', []))

            cnam = subs.get('CNAM', [None])[0]
            script_comment = cnam.decode('utf-8', errors='replace').rstrip('\x00') if cnam else None

            kwda = subs.get('KWDA', [None])[0]
            kw_count = len(kwda) // 4 if kwda else 0

            qtgl = subs.get('QTGL', [None])[0]
            qtgl_fid = struct.unpack_from('<I', qtgl)[0] if qtgl and len(qtgl) >= 4 else None

            qtyp = subs.get('QTYP', [None])[0]
            qtyp_fid = struct.unpack_from('<I', qtyp)[0] if qtyp and len(qtyp) >= 4 else None

            qfct = subs.get('QFCT', [None])[0]
            qfct_fid = struct.unpack_from('<I', qfct)[0] if qfct and len(qfct) >= 4 else None

            qloc = subs.get('QLOC', [None])[0]
            qloc_fid = struct.unpack_from('<I', qloc)[0] if qloc and len(qloc) >= 4 else None

            qsrc = subs.get('QSRC', [None])[0]
            qsrc_fid = struct.unpack_from('<I', qsrc)[0] if qsrc and len(qsrc) >= 4 else None

            mtfl = subs.get('MTFL', [None])[0]
            mtfl_fid = struct.unpack_from('<I', mtfl)[0] if mtfl and len(mtfl) >= 4 else None

            swfp = subs.get('SWFP', [None])[0]
            swfp_str = swfp.decode('utf-8', errors='replace').rstrip('\x00') if swfp else None

            srfl = subs.get('SRFL', [None])[0]
            srfl_val = struct.unpack_from('<H', srfl)[0] if srfl and len(srfl) >= 2 else None

            misd = subs.get('MISD', [None])[0]

            qdnam_extra = subs.get('NAM0', [None])[0]  # Timestamp?

            found[form_id] = {
                'editorid': subs.get('EDID', [b''])[0].decode('utf-8', errors='replace').rstrip('\x00'),
                'major_flags_raw': major_flags,
                'major_flags_names': decode_major_flags(major_flags),
                'form_version': form_version,
                'version2': version2,
                'q_flags': q_flags,
                'q_flags_names': decode_flags(q_flags),
                'q_priority': q_priority,
                'q_type': q_type,
                'q_type_name': decode_type(q_type),
                'dnam_raw': dnam.hex(),
                'full_name': full_name,
                'has_vmad': has_vmad,
                'vmad_size': vmad_size,
                'alias_count': alias_markers,
                'stage_count': stage_count,
                'objective_count': obj_count,
                'ctda_count': ctda_count,
                'filter': fltr_str,
                'quest_group': f'0x{qtgl_fid:08X}' if qtgl_fid else None,
                'quest_type_kw': f'0x{qtyp_fid:08X}' if qtyp_fid else None,
                'quest_faction': f'0x{qfct_fid:08X}' if qfct_fid else None,
                'location': f'0x{qloc_fid:08X}' if qloc_fid else None,
                'source_quest': f'0x{qsrc_fid:08X}' if qsrc_fid else None,
                'script_comment': script_comment,
                'keywords_count': kw_count,
                'mission_type_kw': f'0x{mtfl_fid:08X}' if mtfl_fid else None,
                'swf_file': swfp_str,
                'srfl': f'0x{srfl_val:04X}' if srfl_val is not None else None,
                'misd_present': misd is not None,
                'all_subrecord_types': sorted({k: len(v) for k, v in subs.items()}.keys()),
            }
            print(f"Found 0x{form_id:06X} at file offset {pos}")
        else:
            f.seek(rec_start + data_size)

        scanned += 1

print(f"\nScanned {scanned} records, found {len(found)}/{len(TARGET_IDS)} targets\n")

for fid in sorted(found.keys()):
    d = found[fid]
    print(f"=== QUST 0x{fid:06X}  EditorID={d['editorid']} ===")
    print(f"  MajorRecordFlagsRaw:  0x{d['major_flags_raw']:08X}  = {d['major_flags_names']}")
    print(f"  FormVersion:          {d['form_version']}")
    print(f"  Version2:             {d['version2']}")
    print(f"  DNAM ({len(d['dnam_raw'])//2}B raw):       {d['dnam_raw']}")
    print(f"  Data.Flags:           0x{d['q_flags']:04X}  = {d['q_flags_names']}")
    print(f"  Data.Priority:        {d['q_priority']}")
    print(f"  Data.Type:            {d['q_type']} = {d['q_type_name']}")
    print(f"  Name (FULL):          '{d['full_name']}'")
    vmad_str = ('YES (VMAD ' + str(d['vmad_size']) + 'B)') if d['has_vmad'] else 'NO'
    print(f"  VirtualMachineAdapter:{vmad_str}")
    print(f"  Aliases (ALST/ALLS):  {d['alias_count']}")
    print(f"  Stages (INDX):        {d['stage_count']}")
    print(f"  Objectives (QOBJ):    {d['objective_count']}")
    print(f"  DialogConditions:     {d['ctda_count']}")
    print(f"  Filter (FLTR):        {d['filter']}")
    print(f"  QuestGroup (QTGL):    {d['quest_group']}")
    print(f"  QuestType KW (QTYP):  {d['quest_type_kw']}")
    print(f"  QuestFaction (QFCT):  {d['quest_faction']}")
    print(f"  Location (QLOC):      {d['location']}")
    print(f"  SourceQuest (QSRC):   {d['source_quest']}")
    print(f"  ScriptComment (CNAM): {d['script_comment']}")
    print(f"  Keywords (KWDA):      {d['keywords_count']} entries")
    print(f"  MissionTypeKW (MTFL): {d['mission_type_kw']}")
    print(f"  SwfFile (SWFP):       {d['swf_file']}")
    print(f"  StarfieldFlags(SRFL): {d['srfl']}")
    print(f"  MissionBoardDesc:     {'YES' if d['misd_present'] else 'NO'}")
    print(f"  All subrecord types:  {d['all_subrecord_types']}")
    print()
