# AutoClicker

A Windows-only auto clicker built with C#/.NET 10 and WinUI 3.

The app uses normal Windows input APIs:

- `SendInput` for left mouse clicks.
- `RegisterHotKey` for global start/stop and emergency-stop hotkeys.
- No anti-cheat bypass, driver behavior, process injection, or game-specific evasion.

## Features

- Fixed compact WinUI 3 desktop window.
- Nintendo.com 2001-inspired periwinkle/carbon/amber UI based on `DESIGN.md`.
- Left-click-only click engine.
- Preset CPS selector: 1, 2, 5, 10, 15, 20, 30, 50, 75, 100, 150, and 200 CPS.
- Global hotkeys:
  - Toggle clicking: `F6`
  - Emergency stop: `F8`
- JSON settings stored under local app data.

## Requirements

- Windows 10 version 1809 or newer.
- .NET 10 SDK.
- Windows App SDK dependencies are restored through NuGet.

## Build

```powershell
dotnet build .\AutoClicker.csproj --configuration Debug
```

## Test

```powershell
dotnet test .\AutoClicker.sln --configuration Debug -p:Platform=x64
```

## Publish

The project includes publish profiles for `win-x64`, `win-x86`, and `win-arm64`.

```powershell
dotnet publish .\AutoClicker.csproj --configuration Release -p:PublishProfile=win-x64 -p:OutputPath="$PWD/artifacts/build/win-x64/"
```

The x64 executable is written to:

```text
artifacts\publish\win-x64\AutoClicker.exe
```

Run it from inside the publish folder. The app is not a single-file executable, so the adjacent DLLs must stay beside `AutoClicker.exe`.

## VS Code

The repository includes VS Code tasks for build, test, and publish:

- `build app`
- `build solution`
- `test`
- `publish release x64`
- `publish release x86`
- `publish release arm64`

The launch config `Run Published AutoClicker` publishes x64 and runs the published executable.

## Notes

If the app says it cannot register `F6` or `F8`, another process is already holding that hotkey. Close the other process or restart Windows.
