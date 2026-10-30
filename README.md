# LimitedLauncher

A small Windows 11-style (WPF Fluent) app that runs a program under a separate **limited local Windows user**.
Pure .NET 10: no NuGet packages, no extra DLLs - only the framework plus P/Invoke into `netapi32` / `crypt32`.

Why: software running under your admin account can inspect other admin-level programs. A game started under a
limited user can't see what you run elsewhere as administrator.

## Features

- Create / remove a local limited user (hidden from the logon screen), or pick any existing one.
- Password is generated if left empty and stored in `%APPDATA%\LimitedLauncher\config.json`, encrypted with DPAPI
  for your Windows account.
- Pick or drag & drop an `.exe`, set arguments and working directory, press **Run as limited user**.
- Optionally grants the user read + execute on the program folder so it can actually start.
- Follows the system light/dark theme and accent colour (WPF Fluent theme, .NET 10).

## Build

```powershell
./release.ps1
```

Produces `LimitedLauncher.exe` and `LimitedLauncher.zip` in `publish/` (git-ignored).
Needs the .NET 10 Desktop runtime to run.

The app asks for administrator rights on start - needed to create users and change folder permissions.
