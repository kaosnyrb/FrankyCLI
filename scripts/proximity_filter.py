#!/usr/bin/env python3
"""
Find placed objects within a radius of anchor positions in a worldspace dump.

Usage:
  proximity_filter.py <wsFile> <radius> <x1,y1,z1> [<x2,y2,z2> ...]

Arguments:
  wsFile   Path to the worldspace dump produced by dump_ws.sh (or gi.sh worldspace_objects)
  radius   XY search radius in overlay units (e.g. 3.0)
  x,y,z    One or more anchor positions (comma-separated, no spaces)

Output:
  Sorted by hit count descending. dz = item Z minus anchor Z (positive = above anchor).

Example:
  python3 scripts/proximity_filter.py C:/tmp/ws_OESF003World.txt 3.0 16.1,113.7,26.4 23.7,143.1,32.8
"""
import re
import sys


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)

    ws_file   = sys.argv[1]
    radius    = float(sys.argv[2])
    positions = [tuple(float(v) for v in a.split(',')) for a in sys.argv[3:]]

    pat = re.compile(
        r'PlacedObject \S+ Base=(\S+) EdID=(\S*) '
        r'Pos=([\-\d.Ee+]+), ([\-\d.Ee+]+), ([\-\d.Ee+]+)'
    )
    nearby = {}

    with open(ws_file, encoding='utf-8', errors='replace') as f:
        for line in f:
            m = pat.search(line)
            if not m:
                continue
            base, edid, x, y, z = m.groups()
            x, y, z = float(x), float(y), float(z)
            for cx, cy, cz in positions:
                if ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 < radius:
                    dz = z - cz
                    if base not in nearby:
                        nearby[base] = {'edid': edid, 'dz': dz, 'count': 0}
                    nearby[base]['count'] += 1
                    break

    print(f"Radius={radius}  Anchors={len(positions)}  Distinct bases={len(nearby)}")
    for base, i in sorted(nearby.items(), key=lambda kv: -kv[1]['count']):
        print(f"  count={i['count']:>3}  dz={i['dz']:+.2f}  Base={base}  EdID={i['edid']}")


if __name__ == '__main__':
    main()
