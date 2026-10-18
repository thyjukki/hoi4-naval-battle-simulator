"""Static analysis helpers for hoi4.exe (x64 PE). Loads the image once and caches indexes in a .npz next to this file."""
import bisect
import os
import struct
import sys

import numpy as np
import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_REG_RIP

EXE = os.environ.get("HOI4_EXE", r"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV\hoi4.exe")
HERE = os.path.dirname(os.path.abspath(__file__))


class Image:
    def __init__(self, path=EXE):
        self.pe = pefile.PE(path, fast_load=True)
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.sections = {}
        size = max(s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData) for s in self.pe.sections)
        self.mem = bytearray(size)
        for s in self.pe.sections:
            name = s.Name.rstrip(b"\0").decode()
            data = s.get_data()
            self.mem[s.VirtualAddress:s.VirtualAddress + len(data)] = data
            self.sections[name] = (s.VirtualAddress, s.VirtualAddress + max(s.Misc_VirtualSize, len(data)))
        self.mem = bytes(self.mem)
        self._load_pdata()
        self.md = Cs(CS_ARCH_X86, CS_MODE_64)
        self.md.detail = True
        self.labels = {}
        self._fcache = {}

    def _load_pdata(self):
        start, end = self.sections[".pdata"]
        raw = np.frombuffer(self.mem[start:end - (end - start) % 12], dtype="<u4").reshape(-1, 3)
        raw = raw[raw[:, 0] != 0]
        order = np.argsort(raw[:, 0])
        self.fstart = raw[order, 0].astype(np.int64)
        self.fend = raw[order, 1].astype(np.int64)

    # ---- basic reads
    def sec_of(self, rva):
        for name, (a, b) in self.sections.items():
            if a <= rva < b:
                return name
        return None

    def u64(self, rva):
        return struct.unpack_from("<Q", self.mem, rva)[0]

    def f32(self, rva):
        return struct.unpack_from("<f", self.mem, rva)[0]

    def f64(self, rva):
        return struct.unpack_from("<d", self.mem, rva)[0]

    def cstr(self, rva, limit=200):
        end = self.mem.find(b"\0", rva, rva + limit)
        return self.mem[rva:end].decode("latin1") if end > 0 else None

    # ---- strings
    def find_bytes(self, needle, section=None):
        out = []
        lo, hi = self.sections[section] if section else (0, len(self.mem))
        i = self.mem.find(needle, lo, hi)
        while i >= 0:
            out.append(i)
            i = self.mem.find(needle, i + 1, hi)
        return out

    def find_cstring(self, text):
        """Exact NUL-terminated string matches (preceded by NUL)."""
        needle = b"\0" + text.encode() + b"\0"
        return [i + 1 for i in self.find_bytes(needle)]

    # ---- functions
    def func_of(self, rva):
        i = bisect.bisect_right(self.fstart, rva) - 1
        if i >= 0 and self.fstart[i] <= rva < self.fend[i]:
            return int(self.fstart[i]), int(self.fend[i])
        return None

    # ---- cross references
    def code_xrefs(self, targets):
        """All code positions whose rip-relative disp32 resolves to one of targets (candidate; verify with disasm)."""
        targets = np.array(sorted(set(targets)), dtype=np.int64)
        lo, hi = self.sections[".text"]
        arr = np.frombuffer(self.mem[lo:hi], dtype=np.uint8)
        hits = []
        chunk = 1 << 24
        for off in range(0, len(arr) - 4, chunk):
            seg = arr[off:off + chunk + 3]
            n = len(seg) - 3
            disp = (seg[0:n].astype(np.int64) | (seg[1:n + 1].astype(np.int64) << 8) |
                    (seg[2:n + 2].astype(np.int64) << 16) | (seg[3:n + 3].astype(np.int64) << 24))
            disp = np.where(disp >= 1 << 31, disp - (1 << 32), disp)
            pos = np.arange(n, dtype=np.int64) + lo + off
            # rip-relative target for instructions whose disp32 ends the instruction, plus common imm8/imm32 tails
            for tail in (0, 1, 4):
                tgt = pos + 4 + tail + disp
                m = np.isin(tgt, targets)
                for p, t in zip(pos[m], tgt[m]):
                    hits.append((int(p), int(t)))
        return self._verify(hits)

    def _verify(self, hits):
        verified = {}
        for p, t in hits:
            f = self.func_of(p)
            if not f:
                continue
            # Disassemble from the function start so instruction boundaries are correct.
            if f not in self._fcache:
                self._fcache[f] = self.disasm_range(*f)
            for ins in self._fcache[f]:
                if ins.address <= p < ins.address + ins.size:
                    branch = ins.mnemonic in ("call", "jmp") and ins.op_str.startswith("0x") and int(ins.op_str, 16) == t
                    if branch or t in self.rip_targets(ins):
                        verified[ins.address] = (t, ins)
                    break
        return sorted((a, t, i) for a, (t, i) in verified.items())

    def callers(self, func_rva):
        """Functions that call or jump to func_rva."""
        return sorted({self.func_of(a)[0] for a, t, i in self.code_xrefs([func_rva]) if self.func_of(a)})

    def callees(self, func_rva):
        f = self.func_of(func_rva)
        out = []
        for ins in self.disasm_range(*f):
            if ins.mnemonic in ("call", "jmp") and ins.op_str.startswith("0x"):
                t = int(ins.op_str, 16)
                if not (f[0] <= t < f[1]):
                    out.append((ins.address, t))
        return out

    def strings_in(self, func_rva):
        f = self.func_of(func_rva)
        out = []
        for ins in self.disasm_range(*f):
            for t in self.rip_targets(ins):
                if self.sec_of(t) == ".rdata":
                    s = self.cstr(t, 120)
                    if s and len(s) >= 4 and all(32 <= ord(ch) < 127 for ch in s):
                        out.append((ins.address, s))
        return out

    def data_xrefs(self, target_rva):
        """8-byte absolute pointers to target in .data/.rdata."""
        va = struct.pack("<Q", self.base + target_rva)
        return self.find_bytes(va, ".data") + self.find_bytes(va, ".rdata")

    # ---- disassembly
    def disasm_range(self, start, end):
        return list(self.md.disasm(self.mem[start:end], start))

    def rip_targets(self, ins):
        out = []
        try:
            for op in ins.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    out.append(ins.address + ins.size + op.mem.disp)
        except Exception:
            pass
        return out

    def describe(self, rva, width=None):
        if rva in self.labels:
            return self.labels[rva]
        sec = self.sec_of(rva)
        if sec == ".rdata":
            s = self.cstr(rva, 80)
            if s and len(s) >= 3 and all(32 <= ord(c) < 127 for c in s):
                return repr(s)
            return f"rdata f32={self.f32(rva):g} f64={self.f64(rva):g}"
        if sec == ".data":
            return f"data:{rva:x} f32={self.f32(rva):g} f64={self.f64(rva):g}"
        if sec == ".text":
            f = self.func_of(rva)
            return f"sub_{rva:x}" if f and f[0] == rva else None
        return None

    def disasm_func(self, rva, out=sys.stdout, max_ins=4000):
        f = self.func_of(rva)
        if not f:
            print(f"no function at {rva:x}", file=out)
            return
        for n, ins in enumerate(self.disasm_range(*f)):
            if n > max_ins:
                break
            notes = [d for d in (self.describe(t) for t in self.rip_targets(ins)) if d]
            if ins.mnemonic == "call" and ins.op_str.startswith("0x"):
                d = self.describe(int(ins.op_str, 16))
                if d:
                    notes.append(d)
            print(f"{ins.address:08x}  {ins.mnemonic:8} {ins.op_str:48} {'; ' + ' | '.join(notes) if notes else ''}", file=out)


_image = None


def image():
    global _image
    if _image is None:
        _image = Image()
    return _image
