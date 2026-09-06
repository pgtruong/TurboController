# TurboController

A [Dalamud](https://dalamud.dev) plugin that adds turbo (auto-repeat) to the FFXIV **cross hotbar**.

Hold a face/d-pad button while a trigger is held, and the action under that crossbar slot re-fires on a
configurable interval instead of once. Controller only — the keyboard hotbar case is already covered by
[ReAction](https://github.com/UnknownX7/ReAction).

- Configurable interval (default 250 ms) with optional random jitter
- Independent filters for GCDs, oGCDs, and in/out of combat
- Works on Windows and Steam Deck (FFXIV under Proton)

This is input repeat, not automation. Nothing happens unless the button is physically held.

## Building

### Prerequisites

* XIVLauncher, FINAL FANTASY XIV, and Dalamud installed, and the game run with Dalamud at least once.
* XIVLauncher installed to its default directories, or `DALAMUD_HOME` set to a custom Dalamud dev directory.
* A .NET SDK matching the one required by `Dalamud.NET.Sdk` (the IDE usually handles this).

### Build

```
dotnet build .\TurboController\TurboController.csproj -c Release
```

Output lands in `TurboController/bin/x64/Release/TurboController.dll`.

### Activating in-game

1. `/xlsettings` → **Experimental** → add the full path to `TurboController.dll` to Dev Plugin Locations.
   You only need to do this once.
2. `/xlplugins` → **Dev Tools > Installed Dev Plugins** → enable **TurboController**.
3. Configure it from the ⚙ button on its plugin installer entry.

Use `/xllog` to see plugin log output.

## License

See [LICENSE.md](LICENSE.md).
