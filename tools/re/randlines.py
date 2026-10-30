"""Source lines of the game's random draws, and how they moved between two builds.

    python randlines.py OLD_EXE NEW_EXE [file-substring ...]

Finds every call to random_fixed `(out, file, line)` and random_int `(file, line)` (located from their 1.19.3 RVAs
through remap.py) with the source file string and line immediate set just before the call. Old and new draws are
paired by call site (1.19.4 passes an empty file string, so only the old build names the file).
The roll logger and the replay reader key rolls by these lines (GameBattleLog, battle_logger.js).
"""
import sys
from collections import defaultdict

from remap import Build, map_rva, match

OLD_RANDOM_FIXED, OLD_RANDOM_INT = 0x2233db0, 0x2233fa0


def draws(build, targets):
    """(file, line, kind, call site, caller) for every call to one of targets {rva: kind}."""
    out = []
    im = build.im
    for site, target, _ in im.code_xrefs(list(targets)):
        f = im.func_of(site)
        if not f:
            continue
        code = [i for i in im.disasm_range(f[0], site + 16) if i.address <= site]
        kind = targets[target]
        file_reg, line_reg = ("rdx", ("r8d", "r8")) if kind == "fixed" else ("rcx", ("edx", "rdx"))
        file, line = None, None
        for ins in reversed(code[-40:-1]):
            regs = ins.op_str.split(",")[0].strip()
            if file is None and ins.mnemonic == "lea" and regs == file_reg:
                t = im.rip_targets(ins)
                file = im.cstr(t[0], 200) if t else "?"
            if line is None and ins.mnemonic == "mov" and regs in line_reg:
                src = ins.op_str.split(",", 1)[1].strip()
                line = int(src, 0) if src.startswith("0x") or src.isdigit() else src
            if ins.mnemonic == "call" and ins.address != site:
                break
        name = (file or "?").replace("\\", "/").split("/")[-1]
        out.append((name, line, kind, site, f[0]))
    return out


def main():
    old, new = Build(sys.argv[1]), Build(sys.argv[2])
    wanted = sys.argv[3:]
    fmap, dmap, ambiguous = match(old, new)
    targets_old = {OLD_RANDOM_FIXED: "fixed", OLD_RANDOM_INT: "int"}
    targets_new = {map_rva(old, new, fmap, dmap, ambiguous, a)["new"]: k for a, k in targets_old.items()}
    # 1.19.4 passes an empty file string, so draws pair by call site: the old site mapped into the new build,
    # else the new draw in the matched function with the same rank.
    new_draws = draws(new, targets_new)
    new_by_site = {d[3]: d for d in new_draws}
    new_by_func = defaultdict(list)
    for d in new_draws:
        new_by_func[d[4]].append(d)
    fx = lambda v: f"0x{v:x}" if isinstance(v, int) else str(v)
    rows = defaultdict(list)
    for name, line, kind, site, func in draws(old, targets_old):
        if wanted and not any(w in name for w in wanted):
            continue
        m = map_rva(old, new, fmap, dmap, ambiguous, site)
        y = new_by_site.get(m["new"])
        if y is None and func in fmap:
            olds = sorted(d[3] for d in draws_in(old, targets_old, func))
            news = sorted(new_by_func.get(fmap[func], []), key=lambda d: d[3])
            if len(olds) == len(news):
                y = news[olds.index(site)]
        rows[name].append((line, kind, site, y))
    for name in sorted(rows):
        print(name)
        for line, kind, site, y in sorted(rows[name], key=lambda r: (r[0] if isinstance(r[0], int) else -1)):
            if y is None:
                print(f"  {kind:5} {fx(line):>7} -> ?       site 0x{site:x} not found")
                continue
            moved = "" if line == y[1] else "   MOVED"
            print(f"  {kind:5} {fx(line):>7} -> {fx(y[1]):<7} site 0x{site:x} -> 0x{y[3]:x}{moved}")


_cache = {}


def draws_in(build, targets, func):
    key = id(build)
    if key not in _cache:
        _cache[key] = defaultdict(list)
        for d in draws(build, targets):
            _cache[key][d[4]].append(d)
    return _cache[key].get(func, [])


if __name__ == "__main__":
    main()
