# Reise

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
# one-time: put the WebView2 assemblies in lib#   Microsoft.Web.WebView2.Core.dll
#   Microsoft.Web.WebView2.WinForms.dll
#   WebView2Loader.dll
powershell -ExecutionPolicy Bypass -File build.ps1
```

The compiler is the one that ships with Windows, so there is nothing to install:

```
C:\Windows\Microsoft.NET\Framework644.0.30319\csc.exe /target:winexe /platform:x64
```

## Features

| | |
|---|---|
| **Attach** | discovery through WMI, a Toolhelp snapshot and a local process scan, deduped by pid |
| **Targets** | several clients at once — the chip lists every pid with its game id |
| **Editor** | Lua colouring, synced gutter, tab indent, line count in the tab bar |
| **Console** | timestamped, filterable by ok / warn / err |
| **Storage** | plain `.lua` files in `scripts\` |
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
