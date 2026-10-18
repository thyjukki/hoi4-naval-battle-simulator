"""Usage: python modmap.py <start hex> <end hex>  - list functions in a range with size, defines used, strings, callees in range."""
import json
import sys
from collections import defaultdict

import hoi4re

im = hoi4re.image()
lo, hi = int(sys.argv[1], 16), int(sys.argv[2], 16)
uses = json.load(open("uses.json"))
defs_by_func = defaultdict(set)
for name, lst in uses.items():
    for u in lst:
        if u["offset"] == 0:
            defs_by_func[u["func"]].add(name.split(".", 1)[1])
starts = [int(s) for s in im.fstart if lo <= s < hi]
for s in starts:
    f = im.func_of(s)
    callees = sorted({t for a, t in im.callees(s) if lo <= t < hi})
    strs = sorted({x for a, x in im.strings_in(s)})[:6]
    print(f"{s:08x} size {f[1] - f[0]:5d} defs[{', '.join(sorted(defs_by_func.get(s, [])))[:220]}]"
          + (f" calls[{', '.join(f'{c:x}' for c in callees)[:200]}]" if callees else "")
          + (f" strs{strs}"[:240] if strs else ""))
