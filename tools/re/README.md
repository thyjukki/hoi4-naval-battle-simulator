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
| `srcfiles.py` | Lists source-file assert paths (e.g. `naval\naval_fire_exchange_member.cpp`) and the functions that use them |

Typical start after a game patch: `python defmap.py`, `python uses.py`, `python srcfiles.py`, then follow the
functions for the spec item you are checking.

Known gaps: functions without unwind entries (small leaf functions) are not attributed, so a define read only from
such a function shows as unused; confirm "no reader" results with `ctx.py` before relying on them.

Do not commit the executable, the generated JSON indexes or large disassembly dumps (see
`docs/reverse-engineering/README.md`).
