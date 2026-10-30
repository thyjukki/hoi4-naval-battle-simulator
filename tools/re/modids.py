"""Modifier id table of a hoi4.exe build: (id, loc key) from the registration function that references MODIFIER_NONE.

    python modids.py                         print the installed build's table
    python modids.py OLD_EXE NEW_EXE [0x28 ...]   ids whose loc key changed id between builds (or only the given ids)

Each registration block loads its `MODIFIER_*` loc key, then calls the registration (0x5574d0 in 1.19.3) with the id in
edx (`xor edx, edx` for 0). The registration function is found as the most called callee of that block function.
"""
import sys
from collections import Counter

from hoi4re import EXE, Image


def table(path):
    im = Image(path)
    func = im.func_of(im.code_xrefs(im.find_cstring("MODIFIER_NONE"))[0][0])
    code = im.disasm_range(*func)
    # The registration call takes the id as an immediate in edx: count calls preceded by one.
    calls = Counter()
    edx_imm = False
    for ins in code:
        if ins.mnemonic in ("xor", "mov") and ins.op_str.startswith("edx, "):
            edx_imm = ins.op_str == "edx, edx" or ins.op_str[5:7] == "0x" or ins.op_str[5:].isdigit()
        elif ins.mnemonic == "call" and ins.op_str.startswith("0x"):
            if edx_imm:
                calls[int(ins.op_str, 16)] += 1
            edx_imm = False
    register = calls.most_common(1)[0][0]
    out = {}
    key, edx = None, None
    for ins in code:
        for t in im.rip_targets(ins):
            s = im.cstr(t, 120) if im.sec_of(t) == ".rdata" else None
            if s and s.startswith("MODIFIER_"):
                key = s
        if ins.mnemonic == "xor" and ins.op_str == "edx, edx":
            edx = 0
        elif ins.mnemonic == "mov" and (ins.op_str.startswith("edx, 0x") or ins.op_str.startswith("edx, ") and ins.op_str[5:].isdigit()):
            edx = int(ins.op_str[5:], 0)
        elif ins.mnemonic == "call" and ins.op_str == hex(register):
            if key is not None and edx is not None:
                out[edx] = key
            key, edx = None, None
    return register, out


def main():
    if len(sys.argv) < 3:
        register, ids = table(sys.argv[1] if len(sys.argv) > 1 else EXE)
        print(f"registration 0x{register:x}, {len(ids)} ids")
        for i, k in sorted(ids.items()):
            print(f"0x{i:x}\t{k}")
        return
    _, old = table(sys.argv[1])
    _, new = table(sys.argv[2])
    by_key = {k: i for i, k in new.items()}
    wanted = [int(x, 16) for x in sys.argv[3:]] or sorted(old)
    print(f"old {len(old)} ids, new {len(new)} ids")
    for i in wanted:
        k = old.get(i)
        j = by_key.get(k)
        if j != i:
            print(f"0x{i:x} {k} -> {'0x%x' % j if j is not None else 'gone'}")


if __name__ == "__main__":
    main()
