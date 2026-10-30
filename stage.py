"""Stage the Reise repository: docs site, source, scripts, README, licence."""
from pathlib import Path
import shutil

WS = Path(r"C:\Users\aegis\Documents\deepseek-harness\default-workspace")
REPO = WS / "reise-site"
SRC = WS / "steam-remote" / "reise"

# ---------------------------------------------------------------- assets
assets = REPO / "docs" / "assets"
assets.mkdir(parents=True, exist_ok=True)

mark = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="none"
  stroke="#f2f2f3" stroke-width="1">
  <circle cx="8" cy="8" r="4.2"/>
  <ellipse cx="8" cy="8" rx="7.4" ry="2.3" transform="rotate(-20 8 8)" opacity=".65"/>
</svg>
"""
(assets / "mark.svg").write_text(mark, encoding="utf-8")

shots = [
    (WS / "steam-remote" / "out" / "reise_final.png", assets / "app.png"),
]
for src, dst in shots:
    if src.is_file():
        shutil.copy2(src, dst)
        print("asset:", dst.name, dst.stat().st_size, "B")
    else:
        print("MISSING asset:", src)

# ---------------------------------------------------------------- source tree
src_dst = REPO / "src"
src_dst.mkdir(exist_ok=True)
shutil.copy2(SRC / "src" / "Reise.cs", src_dst / "Reise.cs")

ui_dst = REPO / "ui"
ui_dst.mkdir(exist_ok=True)
shutil.copy2(SRC / "ui" / "index.html", ui_dst / "index.html")

scripts_dst = REPO / "scripts"
scripts_dst.mkdir(exist_ok=True)
for f in (SRC / "scripts").glob("*.lua"):
    shutil.copy2(f, scripts_dst / f.name)

shutil.copy2(SRC / "build.ps1", REPO / "build.ps1")
if (SRC / "assets" / "logo.ico").is_file():
    (REPO / "assets").mkdir(exist_ok=True)
    shutil.copy2(SRC / "assets" / "logo.ico", REPO / "assets" / "logo.ico")
    shutil.copy2(SRC / "assets" / "logo.png", REPO / "assets" / "logo.png")

# ---------------------------------------------------------------- meta files
(REPO / "docs" / ".nojekyll").write_text("", encoding="utf-8")

(REPO / ".gitignore").write_text("""# build output
Reise.exe
*.exe
*.pdb

# WebView2 assemblies (Microsoft redistributables, not ours to ship)
lib/

# local runtime + Chromium profile
runtime/
profile/

# logs
*.log
deploy.log
""", encoding="utf-8")

(REPO / "LICENSE").write_text("""MIT License

Copyright (c) 2026 Reise contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
""", encoding="utf-8")

(REPO / "README.md").write_text("""# Reise

A quiet Windows script editor for your own game clients: attach to a process you own, edit Lua with
syntax highlighting, run scripts, keep them on disk, and update the runtime in one click.

```
Reise.exe
├─ ui/            interface (HTML/CSS/JS behind WebView2 — no browser chrome)
├─ src/Reise.cs   host: attach · targets · select · execute · save · hub · version · update · diagnose
├─ scripts/       your .lua files
├─ assets/        logo.ico · logo.png
├─ docs/          the landing page served by GitHub Pages
└─ build.ps1      one-command build
```

## Build

```powershell
# one-time: put the WebView2 assemblies in lib\
#   Microsoft.Web.WebView2.Core.dll
#   Microsoft.Web.WebView2.WinForms.dll
#   WebView2Loader.dll
powershell -ExecutionPolicy Bypass -File build.ps1
```

The compiler is the one that ships with Windows, so there is nothing to install:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /platform:x64
```

## Features

| | |
|---|---|
| **Attach** | discovery through WMI, a Toolhelp snapshot and a local process scan, deduped by pid |
| **Targets** | several clients at once — the chip lists every pid with its game id |
| **Editor** | Lua colouring, synced gutter, tab indent, line count in the tab bar |
| **Console** | timestamped, filterable by ok / warn / err |
| **Storage** | plain `.lua` files in `scripts\\` |
| **Updater** | downloads the new runtime, compares SHA-256, replaces it, restarts hidden |
| **Diagnose** | a failed attach prints the process counts it could see and which layer returned them |

## Scope

Reise does not execute Luau inside Roblox. Hyperion blocks external injection and re-validates code
pages on every build, so the Execute button saves the script and says exactly that rather than
pretending to have run it. Everything else in the table above works.

Use it on processes you own or are authorised to test.

## Landing page

`docs/` is a self-contained static page. GitHub Pages serves it from `main → /docs`.

## Licence

MIT — see [LICENSE](LICENSE).
""", encoding="utf-8")

print("repo staged at", REPO)
