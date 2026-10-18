"""Find every code reference to mapped define globals (excluding their loaders); write uses.json and print a ranking."""
import json
from collections import defaultdict

import hoi4re

im = hoi4re.image()
defmap = json.load(open("defmap.json"))
by_global = defaultdict(list)
for name, v in defmap.items():
    if v["global"]:
        by_global[v["global"]].append(name)
# arrays/vectors: also catch references a few bytes into the global (begin/end pointers)
targets = set()
for g in by_global:
    for d in range(0, 16, 8):
        targets.add(g + d)
refs = im.code_xrefs(list(targets))
loaders = {v["loader"] for v in defmap.values()}
uses = defaultdict(list)
for addr, target, ins in refs:
    f = im.func_of(addr)
    if not f or f[0] in loaders:
        continue
    base = target if target in by_global else target - 8
    for name in by_global.get(base, []):
        uses[name].append({"addr": addr, "func": f[0], "ins": f"{ins.mnemonic} {ins.op_str}", "offset": target - base})
json.dump(uses, open("uses.json", "w"), indent=1, sort_keys=True)

func_defs = defaultdict(set)
for name, lst in uses.items():
    for u in lst:
        func_defs[u["func"]].add(name)
ranked = sorted(func_defs.items(), key=lambda kv: -len([n for n in kv[1] if n.startswith("NNavy")]))
print("defines with uses:", len(uses), "unused:", len([n for n in defmap if n not in uses]))
for f, names in ranked[:40]:
    navy = sorted(n.split(".", 1)[1] for n in names if n.startswith("NNavy"))
    other = sorted(n for n in names if not n.startswith("NNavy"))
    fs, fe = im.func_of(f)
    print(f"{f:08x} size {fe - fs:6d} navy {len(navy):2d}: {', '.join(navy)[:300]}" + (f" | {', '.join(other)[:120]}" if other else ""))
