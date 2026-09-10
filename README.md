# Turbo Controller

A [Dalamud](https://dalamud.dev) plugin that allows hold to cast to the FFXIV **cross hotbar**.

A personal project for me as I've had issues with my hands hurting after spamming to cast skills 
for so long even with a controller. I will try to keep this updated but don't think this will ever 
make the official Dalamud repo since it may be considered automation.

## Installing

This plugin is not in the official Dalamud repo. To install it from my own repo:

1. `/xlsettings` -> **Experimental** -> **Custom Plugin Repositories**
2. Add `https://raw.githubusercontent.com/pgtruong/TurboController/master/repo.json` and hit **Save**
3. `/xlplugins` -> search for **Turbo Controller** -> **Install**

## Settings

| Setting | Default | What it does |
|---|---|---|
| Enable hold to cast | on | Master switch. Off means every button behaves stock. |
| Repeat interval | 250 ms | Gap between repeats. |
| Repeat variance | 0 ms | Randomises each interval by ± this much. |
| Initial delay | 250 ms | Gap between your real press and the *first* repeat. |
| GCDs | on | Repeat actions that share the global cooldown. |
| Off-GCD abilities | on | Repeat abilities on their own cooldown. |
| Repeat out of combat | off | When off, a held button fires once outside combat. |
| Repeat with weapons drawn | off | Repeats while your weapon is unsheathed. (To account for first hits) |

Items, macros and emotes always repeat while turbo is on for this version, not sure if I'll add an option for this as well.

## Building

```
dotnet build .\TurboController\TurboController.csproj -c Release
dotnet test .\TurboController.Tests\TurboController.Tests.csproj
```

### Activating in-game

1. `/xlsettings` → **Experimental** → add the full path to `TurboController.dll` to Dev Plugin Locations.
2. `/xlplugins` → **Dev Tools > Installed Dev Plugins** → enable **Turbo Controller**.

## AI usage

Per Dalamud's [AI policy](https://dalamud.dev/plugin-publishing/ai-policy/), AI involvement in this
plugin is at the **Pair** level: Active human-AI collaboration throughout. Contribution is roughly equal. All human tested in-game.

## Credits

Plugin icon by [Ayub Irawan](https://www.flaticon.com/authors/ayub-irawan), sourced from
[Flaticon](https://www.flaticon.com/) and used under their
[free licence attribution requirement](https://support.flaticon.com/s/article/Attribution-How-when-and-where-FI).

## License

See [LICENSE.md](LICENSE.md).
