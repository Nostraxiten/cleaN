# release/

`cleaN.exe` in this folder is the ready-to-run build of the source in [`../src`](../src).

- **Version:** 0.1.0
- **Target:** Windows 10/11, x64
- **Self-contained:** yes. The .NET 8 runtime is bundled, so nothing else has to be installed.
- **Single file:** yes, compressed. That is why a small app is around 65 MB.
- **Elevation:** the executable is manifested as `requireAdministrator`, so Windows shows the
  UAC prompt on launch. Without it, Prefetch and the machine-wide locations cannot be read.

## Rebuilding it yourself

Never trust a binary you did not build. From `src/`, with the .NET 8 SDK installed:

```powershell
./build.ps1
```

That overwrites `release/cleaN.exe` with your own build of the current source.

## A note on keeping the binary in git

Every rebuild committed here adds another ~65 MB to the repository history permanently.
If cleaN starts shipping regular versions, attaching the executable to a GitHub Release
instead keeps the clone small while still giving people a direct download.
