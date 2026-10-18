"""Usage: python who.py KEY [KEY...]  - list functions that read each define (category optional, e.g. NNavy.KEY or KEY)."""
import json
import sys

uses = json.load(open("uses.json"))
defmap = json.load(open("defmap.json"))
for arg in sys.argv[1:]:
    names = [n for n in defmap if n == arg or n.endswith("." + arg)]
    for name in names:
        g = defmap[name]["global"]
        lst = uses.get(name, [])
        funcs = sorted({u["func"] for u in lst})
        print(f"{name} @ data:{g:x} reader {defmap[name]['reader']:x}: {len(lst)} uses in {len(funcs)} funcs")
        for u in lst:
            print(f"    {u['addr']:08x} in {u['func']:08x}  {u['ins']}" + (f"  (+{u['offset']})" if u['offset'] else ""))
