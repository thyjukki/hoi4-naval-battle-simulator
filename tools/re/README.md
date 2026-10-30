# hoi4.exe static analysis helpers

Scripts used to produce `docs/reverse-engineering/findings/`. They read your own installed `hoi4.exe` and write
nothing outside this folder. Requirements: Python 3.12 with `pefile`, `capstone` and `numpy`.

Set `HOI4_EXE` and `HOI4_DEFINES` if the game is not in the default Steam location. Run from this folder.

| Script | Purpose |
|---|---|
| `hoi4re.py` | Library: loads the PE image, function bounds from `.pdata`, string search, code/data cross-references, annotated disassembly |
| `defmap.py` | Maps every `NNavy`/`NAir`/`NMilitary` define in `00_defines.lua` to its storage global; writes `defmap.json` |
| `uses.py` | Finds every code read of those globals; writes `uses.json` and ranks functions by naval defines used |
| `who.py KEY...` | Lists the functions that read a define |
| `ctx.py <rva> [before] [after]` | Annotated disassembly around an address |
| `modmap.py <start> <end>` | Lists functions in an address range with their defines, strings and callees |
| `darkness.py` | Fixed-point emulation of the province darkness (S-NIGHT), for checks against logged battles |
| `srcfiles.py` | Lists source-file assert paths (e.g. `naval\naval_fire_exchange_member.cpp`) and the functions that use them |
| `remap.py OLD NEW` | Maps RVAs from one build to another by matching functions on masked code (then callees, globals, vtables): `same` / `changed` / `unmatched` per address; `--diff` prints an instruction diff, `--check` the struct offsets, constants and callees that changed in the functions holding the RVAs |
| `modids.py [OLD NEW]` | The modifier id table (id → `MODIFIER_*` loc key) of a build, or the ids that moved between two |
| `randlines.py OLD NEW` | The source lines of every `random_fixed` / `random_int` call, paired between two builds by call site (the roll logger and the replay reader key rolls by line) |

Typical start after a game patch: keep a copy of the old hoi4.exe before Steam replaces it (Steam's betas list has
the previous version as a branch if it is too late), then `python remap.py OLD NEW --file ../ingame/battle_logger.js`,
`python randlines.py OLD NEW`, `python modids.py OLD NEW`, and `--check` over the index and findings (see
`docs/reverse-engineering/README.md#patch-1194`). With the new build as `HOI4_EXE`: `python defmap.py`,
`python uses.py`, `python srcfiles.py`, then follow the functions for the spec item you are checking. 1.19.4 passes an
empty source-file string to asserts and random draws, so `srcfiles.py` and source file names only work on 1.19.3.

Known gaps: functions without unwind entries (small leaf functions) are not attributed, so a define read only from
such a function shows as unused; confirm "no reader" results with `ctx.py` before relying on them.

Do not commit the executable, the generated JSON indexes or large disassembly dumps (see
`docs/reverse-engineering/README.md`).
