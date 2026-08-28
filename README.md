<div align="center">

<img src="src/Assets/logo.svg" alt="cleaN" width="88" height="88">

# cleaN

**An open source system cleaner for Windows.**

[Leer en español](README.es.md) · [MIT licensed](LICENSE) · Windows 10/11 · .NET 8 + WPF

</div>

---

## What cleaN does

cleaN frees disk space and tidies up Windows without guessing on your behalf. It scans
first, shows you exactly what it found, and only deletes once you say so. Preview mode is
on by default, so a fresh install cannot delete anything until you deliberately turn it off.

What sets it apart from the usual cleaners is the **unused application detector**: instead
of shipping a hand-maintained list of known programs, cleaN reads the Windows uninstall
registry and cross-references every entry against the launch history Windows already keeps
(Prefetch, and Explorer's UserAssist records). The result works for whatever happens to be
installed on your machine, including software nobody has ever heard of.

### Features

| Section | What it cleans |
| --- | --- |
| **Temporary files** | `%TEMP%`, `C:\Windows\Temp`, the Windows Update download cache, thumbnail and icon caches, Windows Error Reporting queues, old logs in `C:\Windows\Logs` |
| **Browser cache** | Cache, cookies and history for every profile of every installed browser: Chrome, Edge, Brave, Vivaldi, Opera, Chromium, Yandex, Firefox, Waterfox, LibreWolf. Cookies and history are opt-in and never selected by default |
| **Empty folders** | Recursive scan for folders that hold no files at any depth, shown as a list you confirm before anything is removed |
| **Unused applications** | Everything installed, ranked by how long it has gone unused. cleaN never uninstalls anything itself; it hands the application's own uninstaller to Windows when you ask |
| **Recycle Bin** | Emptied through the Windows shell API, across every drive |
| **Report and logs** | A plain text log of every run: each file removed and the total space recovered. Log saving is **off by default**; toggle it with *Save log files* in the header or the Report tab. Logs can be deleted in bulk from the same tab |

### How cleaN keeps you safe

- **Preview mode by default.** Every run reports what it *would* delete and touches nothing
  until you switch it off yourself.
- **Whitelist, not blacklist.** Each module declares the folders it is allowed to work in. A
  path is only deleted when it sits *strictly below* one of those folders, is outside every
  protected area, and is at least two levels below the drive root. A single failed check
  means the item is skipped and reported, never removed.
- **Protected areas are absolute.** `System32`, `WinSxS`, `Program Files`, the shell folders,
  drive roots, `$Recycle.Bin`, `System Volume Information` and your OneDrive folders are off
  limits. Inside `C:\Windows` the rule is inverted: only `Temp`, `Logs`,
  `SoftwareDistribution\Download` and `Downloaded Program Files` are cleanable, and nothing else.
- **Junctions and symbolic links are never followed**, so a reparse point cannot lead a scan
  somewhere it should not go.
- **Temporary files younger than 24 hours are left alone**, because running installers keep
  live state there.
- **Every run can be logged**, so you can always audit what happened. Log saving is opt-in and can be toggled or bulk-deleted from the Report tab.

## Screenshots

Screenshots will be added here once the interface is final. In the meantime: a white,
minimal default theme with a dark theme one click away in the top right, and a six-entry
sidebar matching the table above.

## Quick install

1. Download [`release/cleaN.exe`](release/cleaN.exe).
2. Run it. Windows asks for administrator rights, which cleaN needs to read `C:\Windows\Prefetch`
   and to clean machine-wide locations. It still runs without them, with reduced results.

There is nothing else to install: the .NET 8 runtime is bundled in the executable.

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
cd src
./build.ps1
```

The script publishes a self-contained, single-file build and drops it into `release/cleaN.exe`.
Options:

```powershell
./build.ps1 -Runtime win-arm64      # build for ARM64 devices
./build.ps1 -Output ../compilado    # write the binary somewhere else
```

Under the hood it is a plain `dotnet publish`, so `src/cleaN.sln` opens directly in Visual
Studio or Rider if you prefer to work there.

The application icon is generated from the same geometry as the logo:

```powershell
python3 src/Assets/generate-icon.py
```

## Repository structure

```
cleaN/
├── src/                        Full source of the .NET 8 / WPF application
│   ├── cleaN.sln
│   ├── cleaN.csproj
│   ├── build.ps1               Build script (see above)
│   ├── app.manifest            Requests administrator rights, per-monitor DPI, long paths
│   ├── Assets/                 Logo, generated icon and the light/dark themes
│   ├── Core/                   Safety rules, the delete engine, logging, settings
│   ├── Modules/                One module per kind of cleaning
│   ├── Apps/                   Installed applications and their launch history
│   ├── Interop/                The few Windows API calls cleaN needs
│   ├── ViewModels/             Application logic, free of any UI type
│   └── Views/                  WPF windows, user controls and services
├── release/                    The compiled binary, ready to download and run
│   └── cleaN.exe
├── docs/screenshots/           Screenshots for this README
├── README.md                   This file
├── README.es.md                Spanish version
└── LICENSE                     MIT
```

The interesting files, if you want to read the code:

- `src/Core/SafetyGuard.cs` — the rules that decide whether a path may be deleted.
- `src/Core/FileSweeper.cs` — the only place in cleaN where files are actually removed.
- `src/Apps/UsageAnalyzer.cs` — how "when was this last used?" is answered.

## Legal notice

cleaN is provided **as is, with no warranty of any kind**. It deletes files, and deleting
files carries risk. You are responsible for what you clean on your own system. Read the
preview list before turning preview mode off.

cleaN contains **no code from CCleaner or from any other proprietary cleaner**, and is not
affiliated with, endorsed by or derived from any of them. CCleaner is closed source, so
there is nothing of it to copy even in principle. Everything here was written from
Microsoft's public documentation on Windows file, cache and registry locations, and from
what open source cleaners such as BleachBit have long documented in the open. Product names
belong to their respective owners and are used only to say which software cleaN can clean.

## License

[MIT](LICENSE). Use it, change it, ship it — just keep the copyright notice.

## Changelog

### cleaN 1.2

- **Log saving is now off by default.** A fresh install no longer writes any files to
  `%LOCALAPPDATA%\cleaN\logs`. Turn it on with the *Save log files* checkbox in the
  top bar or in the *Report and logs* tab.
- **Delete all logs button.** The Report tab now includes a *Delete all logs* button that
  removes every previously saved log file in one click. The button is disabled when there
  are no logs to delete.
