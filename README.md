# PostMortem (Death Recap)

A BepInEx/Harmony mod for Valheim. On death, shows a recap of what actually
killed you - attacker, damage type, damage amount - plus a situational tip
generated from tracked context: your stamina in the seconds before death,
how many hits you took in quick succession, and active status effects (wet,
freezing, poison, tarred, encumbered, etc.) at the moment of death.

Fully rule-based, no external API calls - see `Plugin.cs` for the ~25
condition/tip combinations.

## Install

1. Make sure BepInEx is installed for Valheim.
2. Copy `DeathRecap.dll` and the `Lang/` folder into
   `<Valheim install>\BepInEx\plugins\DeathRecap\`.
3. Launch the game.

## Localization

Translations live in `Lang/<LanguageName>.txt` as simple `key=value` lines,
one file per language, named exactly as Valheim's own language list (e.g.
`English.txt`, `German.txt`, `Dutch.txt`). Missing keys or missing language
files fall back to English automatically. Damage-type labels (Blunt, Fire,
Poison, etc.) reuse Valheim's own existing translations rather than
duplicating them.

## Building

Requires the .NET 8 SDK. `ValheimPath` in `DeathRecap.csproj` points at the
local Valheim install for its assembly references - update it if building
on a different machine.

```
dotnet build
```
