"""Map every define (category, key) in the 1.19.3 defines file to its storage global and reader function.
Writes defmap.json: {"NNavy.KEY": {"global": rva, "reader": rva, "loader": rva}}."""
import json
import os
import re

import hoi4re

DEFINES = os.environ.get("HOI4_DEFINES", r"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV\common\defines\00_defines.lua")
CATEGORIES = ["NNavy", "NAir", "NMilitary"]

text = open(DEFINES, encoding="utf-8-sig", errors="replace").read()
keys = {c: set() for c in CATEGORIES}
current = None
depth = 0
for line in text.splitlines():
    code = line.split("--")[0]
    m = re.match(r"^(\w+)\s*=\s*\{", code)
    if depth == 1 and m:
        current = m.group(1)
    if depth == 2 and current in keys:
        km = re.match(r"^\s*([A-Z][A-Z0-9_]+)\s*=", code)
        if km:
            keys[current].add(km.group(1))
    depth += code.count("{") - code.count("}")
    if depth <= 1:
        current = None if depth < 2 else current

im = hoi4re.image()
cat_strings = {c: set(im.find_cstring(c)) for c in CATEGORIES}
name_locs = {}
for c in CATEGORIES:
    for k in keys[c]:
        for loc in im.find_cstring(k):
            name_locs.setdefault(loc, k)
print("keys", {c: len(v) for c, v in keys.items()}, "string locations", len(name_locs))

refs = im.code_xrefs(list(name_locs))
print("code refs", len(refs))
result = {}
func_cache = {}
for addr, target, ins in refs:
    f = im.func_of(addr)
    if not f or f[1] - f[0] > 0x2000:
        continue
    if f not in func_cache:
        func_cache[f] = im.disasm_range(*f)
    insns = func_cache[f]
    category = None
    for i in insns:
        for t in im.rip_targets(i):
            for c, locs in cat_strings.items():
                if t in locs:
                    category = c
    if category is None:
        continue
    idx = next(n for n, i in enumerate(insns) if i.address == addr)
    glob = None
    for back in insns[max(0, idx - 4):idx][::-1]:
        if back.mnemonic == "lea" and back.op_str.startswith("r8,"):
            ts = im.rip_targets(back)
            if ts:
                glob = ts[0]
                break
    reader = None
    for fwd in insns[idx + 1:idx + 4]:
        if fwd.mnemonic == "call" and fwd.op_str.startswith("0x"):
            reader = int(fwd.op_str, 16)
            break
    key = name_locs[target]
    if key not in keys[category]:
        continue
    result[f"{category}.{key}"] = {"global": glob, "reader": reader, "loader": f[0]}

json.dump(result, open("defmap.json", "w"), indent=1, sort_keys=True)
print("mapped", len(result), "of", sum(len(v) for v in keys.values()))
readers = {}
for v in result.values():
    readers[v["reader"]] = readers.get(v["reader"], 0) + 1
print("reader functions:", {hex(k) if k else None: n for k, n in sorted(readers.items(), key=lambda kv: -kv[1])})
