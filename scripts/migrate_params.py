import re, os

SETTER_FILES = [
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Cities.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Cities_Akila.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Cities_Cydonia.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Cities_Neon.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_SpaceInformant.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_SpaceActivator.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Derelicts.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_SpaceDestroy.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_SpecificDungeons.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Spacestation.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_PlanetSmallBaseDestroy.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_PlanetSmallBaseInformant.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_PlanetCombat.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_PlanetInvestigate.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Dataslate.cs',
    'c:/Git/FrankyCLI/Retrograde.Library/Quests/Templates/Templates_Fork.cs',
]

STRING_PAT = r'"(?:[^"\\]|\\.)*"'  # matches a quoted C# string literal


def find_block_end(text, start):
    assert text[start] == '{', f"Expected '{{' at {start}, got '{text[start]}'"
    depth = 0
    for i in range(start, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return i
    return -1


def find_mission_template_blocks(content):
    blocks = []
    for m in re.finditer(r'new MissionTemplate\(\s*\)', content):
        brace_start = content.find('{', m.end())
        if brace_start == -1:
            continue
        brace_end = find_block_end(content, brace_start)
        if brace_end == -1:
            continue
        blocks.append((brace_start, brace_end + 1))  # end is exclusive
    return blocks


def extract_old_fields(block):
    spacesuit = None
    label = None
    formid = None

    m = re.search(r'\bneedSpacesuit\s*=\s*(true|false)\s*,', block)
    if m:
        spacesuit = m.group(1)

    pat = r'\bparameter1\s*=\s*(' + STRING_PAT + r')\s*,'
    m = re.search(pat, block)
    if m:
        label = m.group(1)

    m = re.search(r'\bparameterformid\s*=\s*([^\r\n,]+)\s*,', block)
    if m:
        formid = m.group(1).strip()

    return spacesuit, label, formid


def remove_old_field_lines(block):
    block = re.sub(r'^[ \t]*needSpacesuit\s*=\s*(?:true|false)\s*,[ \t]*\r?\n', '', block, flags=re.MULTILINE)
    pat = r'^[ \t]*parameter1\s*=\s*' + STRING_PAT + r'\s*,[ \t]*\r?\n'
    block = re.sub(pat, '', block, flags=re.MULTILINE)
    block = re.sub(r'^[ \t]*parameterformid\s*=\s*[^\r\n,]+\s*,[ \t]*\r?\n', '', block, flags=re.MULTILINE)
    return block


def add_trailing_comma_to_last_line(text):
    lines = text.split('\n')
    for i in range(len(lines) - 1, -1, -1):
        if lines[i].strip():
            if not lines[i].rstrip().endswith(','):
                lines[i] = lines[i].rstrip() + ','
            break
    return '\n'.join(lines)


def migrate_block(block):
    spacesuit, label, formid = extract_old_fields(block)
    if spacesuit is None and label is None and formid is None:
        return block

    new_entries = []
    if spacesuit is not None:
        new_entries.append('{"NeedSpacesuit", ' + spacesuit + '}')
    if label is not None:
        new_entries.append('{"Label", ' + label + '}')
    if formid is not None:
        new_entries.append('{"FormId", ' + formid + '}')

    block = remove_old_field_lines(block)

    params_match = re.search(r'\bparameters\s*=\s*new\s+Dictionary', block)

    if params_match:
        dict_brace_pos = block.find('{', params_match.end())
        if dict_brace_pos == -1:
            return block
        dict_end_pos = find_block_end(block, dict_brace_pos)
        if dict_end_pos == -1:
            return block

        dict_content = block[dict_brace_pos:dict_end_pos + 1]

        if '\n' not in dict_content:
            # Single-line dict
            inner = dict_content[1:-1].strip()
            entries_str = ', '.join(new_entries)
            if inner:
                new_dict = '{ ' + inner + ', ' + entries_str + ' }'
            else:
                new_dict = '{ ' + entries_str + ' }'
        else:
            # Multi-line dict
            lines = dict_content.split('\n')
            entry_indent = '                '
            for line in lines[1:]:
                stripped = line.strip()
                if stripped.startswith('{'):
                    entry_indent = re.match(r'^([ \t]*)', line).group(1)
                    break

            # Find last non-whitespace position before closing }
            last_content_pos = dict_end_pos - dict_brace_pos - 1
            while last_content_pos > 0 and dict_content[last_content_pos] in ' \t\r\n':
                last_content_pos -= 1

            newline_pos = dict_content.rfind('\n', 0, dict_end_pos - dict_brace_pos)
            indent_of_close = dict_content[newline_pos + 1:dict_end_pos - dict_brace_pos]

            entries_to_add = ',\n' + ',\n'.join(entry_indent + e for e in new_entries)
            new_dict = dict_content[:last_content_pos + 1] + entries_to_add + '\n' + indent_of_close + '}'

        block = block[:dict_brace_pos] + new_dict + block[dict_end_pos + 1:]

    else:
        # No existing params dict
        indent_match = re.search(r'^([ \t]+)\w', block, re.MULTILINE)
        indent = indent_match.group(1) if indent_match else '                '

        entries_str = ', '.join(new_entries)
        new_params_line = indent + 'parameters = new Dictionary<string, object>() { ' + entries_str + ' }'

        close_match = re.search(r'\n([ \t]*\})\s*$', block)
        if close_match:
            before_close = block[:close_match.start()]
            before_close = add_trailing_comma_to_last_line(before_close)
            block = before_close + '\n' + new_params_line + close_match.group(0)

    return block


def migrate_file(filepath):
    for enc in ('utf-8', 'utf-8-sig', 'cp1252'):
        try:
            with open(filepath, 'r', encoding=enc) as f:
                content = f.read()
            break
        except UnicodeDecodeError:
            continue
    else:
        print(f'ERROR in {os.path.basename(filepath)}: could not decode with any known encoding')
        return

    blocks = find_mission_template_blocks(content)
    result = content

    for brace_start, brace_end in reversed(blocks):
        block_text = result[brace_start:brace_end]
        migrated = migrate_block(block_text)
        if migrated != block_text:
            result = result[:brace_start] + migrated + result[brace_end:]

    if result != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(result)
        print(f'Migrated: {os.path.basename(filepath)}')
    else:
        print(f'No changes: {os.path.basename(filepath)}')


for filepath in SETTER_FILES:
    try:
        migrate_file(filepath)
    except Exception as e:
        print(f'ERROR in {os.path.basename(filepath)}: {e}')
        import traceback
        traceback.print_exc()

print('Done.')
