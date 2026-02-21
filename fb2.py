import re
with open('C:/Git/FrankyCLI/build_output.txt', 'r') as f:
    lines = f.readlines()
seen = set()
for line in lines:
    if 'warning CS' not in line and 'error CS' not in line: continue
    line = line.strip()
    # remove csproj suffix
    idx = line.rfind(' [')
    if idx > 0: line = line[:idx]
    # shorten the path: keep from first non-path part
    m = re.search(r'Retrograde\.Library[^)]+\)', line)
    if m:
        rest = line[m.start():]
        rest = rest.replace('Retrograde.Library', 'Lib')
        if rest not in seen:
            seen.add(rest)
            print(rest)
    else:
        if line not in seen:
            seen.add(line)
            print(line)
