"""Maps addresses from one hoi4.exe build to another by matching functions on their code.

    python remap.py OLD_EXE NEW_EXE 0x1dd370 0x15c3600 ...      map the given RVAs
    python remap.py OLD_EXE NEW_EXE --file ../ingame/battle_logger.js --out map.json
                                                                 map every 0x... literal in a file that lies in .text/.data

Each function (from .pdata) is reduced to its bytes with rip-relative displacements and rel32 branch targets masked.
Functions whose masked bytes are unique in both builds match first; matched pairs then match their callees and the
globals they read instruction by instruction, which settles duplicates and finds functions whose code changed (same
call site, different bytes). A mid-function address maps to the same offset when the code is the same, else through
an instruction alignment, and is then marked "changed": read both disassemblies before trusting it (a changed struct
offset or a new branch shows up there).

Status per address: same (masked code identical), changed (matched function, different code), unmatched.
Indexes are cached as <exe sha256 prefix>.remap.pkl next to this file (not committed).
"""
import argparse
import difflib
import hashlib
import json
import os
import pickle
import re
import sys
from collections import Counter, defaultdict

from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

from hoi4re import HERE, Image


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest().upper()


class Build:
    """Per-function masked hash, loose (mnemonic) hash and ordered external references."""

    def __init__(self, path):
        self.path = path
        self.sha = sha256(path)
        cache = os.path.join(HERE, f"{self.sha[:16]}.remap.pkl")
        self.im = Image(path)
        if os.path.exists(cache):
            with open(cache, "rb") as f:
                self.funcs = pickle.load(f)
            return
        self.funcs = {}
        text = self.im.sections[".text"]
        n = len(self.im.fstart)
        for k, (a, b) in enumerate(zip(self.im.fstart, self.im.fend)):
            a, b = int(a), int(b)
            if not (text[0] <= a < text[1]):
                continue
            if k % 10000 == 0:
                print(f"  {os.path.basename(path)}: {k}/{n} functions", file=sys.stderr)
            self.funcs[a] = self._index(a, b)
        with open(cache, "wb") as f:
            pickle.dump(self.funcs, f, protocol=pickle.HIGHEST_PROTOCOL)

    def _index(self, a, b):
        code = bytearray(self.im.mem[a:b])
        refs = []  # (offset in function, kind, target)
        mnem = []
        for ins in self.im.md.disasm(self.im.mem[a:b], a):
            off = ins.address - a
            mnem.append(ins.mnemonic)
            target = None
            kind = None
            if ins.mnemonic == "call" or ins.mnemonic.startswith("j"):
                ops = ins.operands
                if len(ops) == 1 and ops[0].type == X86_OP_IMM:
                    t = ops[0].imm
                    if not (a <= t < b):
                        target, kind = t, "call"
                        size = 4 if ins.size >= 5 else 1
                        start = ins.size - size
                        code[off + start:off + ins.size] = b"\0" * size
            for op in ins.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    target, kind = ins.address + ins.size + op.mem.disp, "rip"
                    d = ins.disp_offset
                    code[off + d:off + d + 4] = b"\0" * 4
            if target is not None:
                refs.append((off, kind, target))
        return (hashlib.blake2b(bytes(code), digest_size=12).digest(),
                hashlib.blake2b(" ".join(mnem).encode(), digest_size=12).digest(),
                b - a, refs)

    def func_of(self, rva):
        return self.im.func_of(rva)

    def disasm(self, start):
        f = self.func_of(start)
        return self.im.disasm_range(*f) if f else []


def unique_pairs(old, new, field):
    by_old, by_new = defaultdict(list), defaultdict(list)
    for a, v in old.funcs.items():
        by_old[v[field]].append(a)
    for a, v in new.funcs.items():
        by_new[v[field]].append(a)
    return {o[0]: by_new[h][0] for h, o in by_old.items() if len(o) == 1 and len(by_new.get(h, ())) == 1}


def match(old, new):
    fmap = unique_pairs(old, new, 0)  # old func -> new func
    dvotes = defaultdict(Counter)  # old data rva -> votes for new data rva
    fvotes = defaultdict(Counter)
    done = set()
    vdone = set()
    for _ in range(12):
        added = 0
        for o, n in list(fmap.items()):
            if o in done:
                continue
            done.add(o)
            ro, rn = old.funcs[o][3], new.funcs[n][3]
            if old.funcs[o][0] == new.funcs[n][0] or old.funcs[o][1] == new.funcs[n][1]:
                pairs = zip(ro, rn) if len(ro) == len(rn) else []
            else:
                pairs = align_refs(old, new, o, n)
            for (_, k1, t1), (_, k2, t2) in pairs:
                if k1 != k2:
                    continue
                if t1 in old.funcs and t2 in new.funcs:
                    fvotes[t1][t2] += 1
                else:
                    dvotes[t1][t2] += 1
        # Vtables: a matched .rdata pointer whose slots are function starts votes slot by slot.
        for t1, votes in list(dvotes.items()):
            t2 = votes.most_common(1)[0][0]
            if old.im.sec_of(t1) != ".rdata" or new.im.sec_of(t2) != ".rdata" or (t1, t2) in vdone:
                continue
            vdone.add((t1, t2))
            for k in range(0, 8 * 512, 8):
                p1, p2 = old.im.u64(t1 + k) - old.im.base, new.im.u64(t2 + k) - new.im.base
                if p1 not in old.funcs or p2 not in new.funcs:
                    break
                fvotes[p1][p2] += 1
        taken = set(fmap.values())
        for t1, votes in fvotes.items():
            if t1 in fmap:
                continue
            t2, c = votes.most_common(1)[0]
            if t2 not in taken and c * 2 > sum(votes.values()):
                fmap[t1] = t2
                taken.add(t2)
                added += 1
        if not added:
            break
    # Loose pass: same mnemonic sequence, unique on both sides.
    taken = set(fmap.values())
    for o, n in unique_pairs(old, new, 1).items():
        if o not in fmap and n not in taken:
            fmap[o] = n
            taken.add(n)
    # Duplicate groups (identical code, e.g. template copies) of the same size on both sides: pair the unmatched
    # members in address order. Marked "ambiguous" by map_rva.
    groups_old, groups_new = defaultdict(list), defaultdict(list)
    for a, v in old.funcs.items():
        if a not in fmap:
            groups_old[v[0]].append(a)
    for a, v in new.funcs.items():
        if a not in taken:
            groups_new[v[0]].append(a)
    ambiguous = set()
    for h, olds in groups_old.items():
        news = groups_new.get(h, [])
        if len(olds) == len(news) > 1:
            for o, n in zip(sorted(olds), sorted(news)):
                fmap[o] = n
                ambiguous.add(o)
    dmap = {}
    for t1, votes in dvotes.items():
        t2, c = votes.most_common(1)[0]
        dmap[t1] = (t2, c, sum(votes.values()))
    return fmap, dmap, ambiguous


def align_refs(old, new, o, n):
    """Ref pairs of two changed functions, aligned on their instruction sequences."""
    io, inn = old.disasm(o), new.disasm(n)
    ko = [f"{i.mnemonic}" for i in io]
    kn = [f"{i.mnemonic}" for i in inn]
    ro = {off: (off, k, t) for off, k, t in old.funcs[o][3]}
    rn = {off: (off, k, t) for off, k, t in new.funcs[n][3]}
    out = []
    for tag, a1, a2, b1, b2 in difflib.SequenceMatcher(None, ko, kn, autojunk=False).get_opcodes():
        if tag != "equal":
            continue
        for x, y in zip(io[a1:a2], inn[b1:b2]):
            ox, oy = x.address - o, y.address - n
            if ox in ro and oy in rn:
                out.append((ro[ox], rn[oy]))
    return out


def map_rva(old, new, fmap, dmap, ambiguous, rva):
    f = old.func_of(rva)
    sec = old.im.sec_of(rva)
    if sec == ".text" and not f:
        # Leaf code without an unwind entry: mapped by the call sites of matched functions, if the bytes agree.
        starts = [t for t in dmap if t <= rva and rva - t < 0x400 and old.im.sec_of(t) == ".text"]
        if starts:
            t1 = max(starts)
            t2, c, total = dmap[t1]
            n = t2 + rva - t1
            length = rva - t1 + 16
            agree = old.im.mem[t1:t1 + length] == new.im.mem[t2:t2 + length]
            return {"new": n, "status": "same" if agree and c * 2 > total else "changed", "votes": [c, total]}
    if sec == ".text" and f and f[0] in fmap:
        o = f[0]
        n = fmap[o]
        same = old.funcs[o][0] == new.funcs[n][0]
        if same:
            return {"new": n + rva - o, "status": "ambiguous" if o in ambiguous else "same", "function": [o, n]}
        io, inn = old.disasm(o), new.disasm(n)
        ko = [(i.mnemonic, i.op_str) for i in io]
        kn = [(i.mnemonic, i.op_str) for i in inn]
        sm = difflib.SequenceMatcher(None, [k[0] for k in ko], [k[0] for k in kn], autojunk=False)
        target = None
        for tag, a1, a2, b1, b2 in sm.get_opcodes():
            if tag == "equal":
                for x, y in zip(io[a1:a2], inn[b1:b2]):
                    if x.address <= rva < x.address + x.size:
                        target = y.address + rva - x.address
        return {"new": target, "status": "changed", "function": [o, n],
                "sizes": [old.funcs[o][2], new.funcs[n][2]]}
    if sec in (".data", ".rdata"):
        # Globals: nearest voted neighbour at or below rva (fields of a struct global shift with it).
        best = None
        for t1, (t2, c, total) in dmap.items():
            if t1 <= rva and rva - t1 < 0x100 and (best is None or t1 > best[0]):
                best = (t1, t2, c, total)
        if best and best[2] * 2 > best[3]:
            t1, t2, c, total = best
            return {"new": t2 + rva - t1, "status": "same" if t1 == rva else "near", "votes": [c, total]}
    return {"new": None, "status": "unmatched"}


def norm(build, ins, fmap_or_inv, dmap_or_inv, side):
    """Instruction text with rip targets and outside branch targets replaced by a build-neutral name."""
    text = ins.op_str
    targets = build.im.rip_targets(ins)
    if ins.mnemonic == "call" or ins.mnemonic.startswith("j"):
        if ins.op_str.startswith("0x"):
            t = int(ins.op_str, 16)
            f = build.func_of(ins.address)
            if f and f[0] <= t < f[1]:
                return f"{ins.mnemonic} local"
            targets = [t]
    for t in targets:
        key = t
        if side == "new":
            key = fmap_or_inv.get(t, dmap_or_inv.get(t, t))
        name = f"<{key:x}>"
        text = re.sub(r"0x%x\b" % t, name, text)
        text = re.sub(r"rip \+ 0x[0-9a-f]+|rip - 0x[0-9a-f]+", name, text)
    return f"{ins.mnemonic} {text}"


def diff(old, new, fmap, dmap, o):
    o = old.func_of(o)[0]
    n = fmap[o]
    inv_f = {v: k for k, v in fmap.items()}
    inv_d = {v[0]: k for k, v in dmap.items()}
    io, inn = old.disasm(o), new.disasm(n)
    ko = [norm(old, i, None, None, "old") for i in io]
    kn = [norm(new, i, inv_f, inv_d, "new") for i in inn]
    print(f"--- 0x{o:x} (0x{old.funcs[o][2]:x} bytes) vs 0x{n:x} (0x{new.funcs[n][2]:x} bytes)")
    for tag, a1, a2, b1, b2 in difflib.SequenceMatcher(None, ko, kn, autojunk=False).get_opcodes():
        if tag == "equal":
            continue
        for i in range(a1, a2):
            print(f"  - {io[i].address:08x} {ko[i]}")
        for i in range(b1, b2):
            print(f"  + {inn[i].address:08x} {kn[i]}")


STACK = re.compile(r"\[(rsp|rbp)\b[^\]]*\]")
ASSERT_NOISE = {0x860, 0x20e8, 0x864, 0x20ec}  # thread-local slot offsets that every assert reads, differ per build


def fingerprint(build, f, fmap_inv, dmap_inv, side):
    """What a function computes, without register allocation and stack layout: struct displacements, immediates,
    and the (build-neutral) functions and globals it uses."""
    out = Counter()
    for ins in build.disasm(f):
        if ins.mnemonic in ("call",) or ins.mnemonic.startswith("j"):
            if ins.op_str.startswith("0x"):
                t = int(ins.op_str, 16)
                lo, hi = build.func_of(f)
                if not (lo <= t < hi):
                    out[("call", fmap_inv.get(t, dmap_inv.get(t, t)) if side == "new" else t)] += 1
            continue
        for t in build.im.rip_targets(ins):
            out[("glob", dmap_inv.get(t, t) if side == "new" else t)] += 1
        text = STACK.sub("[stack]", ins.op_str)
        for m in re.finditer(r"\[[a-z0-9]+ [+-] (0x[0-9a-f]+)\]", text):
            if "rip" not in m.group(0):
                out[("disp", int(m.group(1), 16))] += 1
        if ins.mnemonic not in ("lea", "nop") and "[" not in text:
            for m in re.finditer(r"(?<![\w+-])(0x[0-9a-f]+|\d+)$", text):
                v = int(m.group(1), 0)
                if v not in ASSERT_NOISE and not (ins.mnemonic in ("add", "sub") and text.startswith("rsp")):
                    out[("imm", ins.mnemonic, v)] += 1
    return out


def check(old, new, fmap, dmap, rvas):
    """Functions containing the given RVAs whose fingerprint changed, with the differing items."""
    inv_f = {v: k for k, v in fmap.items()}
    inv_d = {v[0]: k for k, v in dmap.items()}
    seen = set()
    for rva in sorted(rvas):
        f = old.func_of(rva)
        if not f or f[0] in seen:
            continue
        seen.add(f[0])
        if f[0] not in fmap:
            print(f"0x{f[0]:x}: unmatched")
            continue
        n = fmap[f[0]]
        if old.funcs[f[0]][0] == new.funcs[n][0]:
            continue
        a = fingerprint(old, f[0], None, None, "old")
        b = fingerprint(new, n, inv_f, inv_d, "new")
        gone, added = a - b, b - a
        # Unmapped string/global targets and assert line immediates show up as noise pairs; keep everything else.
        keep = lambda c: Counter({k: v for k, v in c.items() if k[0] not in ("glob",)})
        gone, added = keep(gone), keep(added)
        if not gone and not added:
            print(f"0x{f[0]:x} -> 0x{n:x}: same fingerprint")
            continue
        fmt = lambda k: f"{k[0]} " + " ".join(f"0x{x:x}" if isinstance(x, int) else str(x) for x in k[1:])
        print(f"0x{f[0]:x} -> 0x{n:x}: - {', '.join(fmt(k) for k in sorted(gone, key=str))} | "
              f"+ {', '.join(fmt(k) for k in sorted(added, key=str))}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("old")
    ap.add_argument("new")
    ap.add_argument("rvas", nargs="*")
    ap.add_argument("--file", action="append", default=[], help="map every 0x literal of 5+ hex digits in this file")
    ap.add_argument("--out", help="write the mapping as JSON")
    ap.add_argument("--diff", action="append", default=[], help="print the instruction diff of the function at this old RVA")
    ap.add_argument("--check", action="store_true",
                    help="for the functions holding the RVAs: report changed struct offsets, constants and callees")
    args = ap.parse_args()
    old, new = Build(args.old), Build(args.new)
    print(f"old {old.sha[:16]} {len(old.funcs)} functions, new {new.sha[:16]} {len(new.funcs)} functions",
          file=sys.stderr)
    fmap, dmap, ambiguous = match(old, new)
    same = sum(1 for o, n in fmap.items() if old.funcs[o][0] == new.funcs[n][0])
    print(f"matched {len(fmap)} functions ({same} with identical masked code), {len(dmap)} globals", file=sys.stderr)
    for d in args.diff:
        diff(old, new, fmap, dmap, int(d, 16))
    rvas = [int(r, 16) for r in args.rvas]
    for path in args.file:
        with open(path, encoding="utf-8", errors="replace") as f:
            rvas += [int(m, 16) for m in re.findall(r"0x([0-9a-fA-F]{5,8})\b", f.read())]
    if args.check:
        check(old, new, fmap, dmap, [r for r in set(rvas) if old.im.sec_of(r) == ".text"])
        return
    result = {}
    for rva in sorted(set(rvas)):
        if old.im.sec_of(rva) not in (".text", ".data", ".rdata"):
            continue
        r = map_rva(old, new, fmap, dmap, ambiguous, rva)
        result[f"0x{rva:x}"] = {k: (f"0x{v:x}" if k == "new" and v is not None else v) for k, v in r.items()}
        if "function" in r:
            result[f"0x{rva:x}"]["function"] = [f"0x{x:x}" for x in r["function"]]
        nv = f"0x{r['new']:x}" if r["new"] is not None else "-"
        print(f"0x{rva:x} -> {nv:12} {r['status']}")
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            json.dump({"old": old.sha, "new": new.sha, "map": result}, f, indent=1)


if __name__ == "__main__":
    main()
