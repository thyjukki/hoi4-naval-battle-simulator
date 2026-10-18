"""Usage: python ctx.py <hex rva> [before=0x60] [after=0x80]  - disassemble a window with annotations."""
import sys

import hoi4re

im = hoi4re.image()
rva = int(sys.argv[1], 16)
before = int(sys.argv[2], 16) if len(sys.argv) > 2 else 0x60
after = int(sys.argv[3], 16) if len(sys.argv) > 3 else 0x80
f = im.func_of(rva)
print("func", hex(f[0]) if f else None, hex(f[1]) if f else None)
start = max(f[0], rva - before) if f else rva - before
# resync: disassemble from function start when close, else from the window start
if f and rva - f[0] < 0x4000:
    insns = [i for i in im.disasm_range(f[0], min(f[1], rva + after)) if i.address >= rva - before]
else:
    insns = im.disasm_range(start, rva + after)
for ins in insns:
    notes = [d for d in (im.describe(t) for t in im.rip_targets(ins)) if d]
    mark = ">>" if ins.address == rva else "  "
    print(f"{mark}{ins.address:08x}  {ins.mnemonic:8} {ins.op_str:46} {'; ' + ' | '.join(notes) if notes else ''}")
