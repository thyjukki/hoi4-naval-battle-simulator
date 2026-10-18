"""List source-path strings under hoi4\\source\\naval (and *combat*) and the address ranges of functions referencing them."""
import re

import hoi4re

im = hoi4re.image()
lo, hi = im.sections[".rdata"]
pat = re.compile(rb"C:\\mnt\\gsg\\hoi4\\hoi4_merged\\hoi4\\source\\[^\x00]*\x00")
paths = {}
for m in pat.finditer(im.mem, lo, hi):
    s = m.group(0)[:-1].decode("latin1")
    if "naval" in s.lower() or "combat" in s.lower() or "navy" in s.lower() or "ship" in s.lower():
        paths[m.start()] = s
refs = im.code_xrefs(list(paths))
by_path = {}
for a, t, i in refs:
    f = im.func_of(a)
    if f:
        by_path.setdefault(paths[t], set()).add(f[0])
for p, funcs in sorted(by_path.items()):
    fs = sorted(funcs)
    print(f"{p.split('source', 1)[1]:60} {len(fs):4d} funcs  {fs[0]:x}..{fs[-1]:x}")
