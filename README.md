# Stat Editor Mod

**A BepInEx plugin for Sun Haven that edits every player stat and gold — permanently, through the game's own save data.**

> Sun Haven is a fantasy farming sim by Pixel Sprout Studios — solo or 8-player co-op, with farming, quests, and magic. This mod opens an in-game editor for all 70 stat types plus gold.

---

## 📜 Table of Contents

- [✨ Features](#-features)
- [🚀 Installation](#-installation)
- [⚙️ Settings](#-settings)
- [🏗️ Architecture](#-architecture)
- [🛠️ Technologies Used](#-technologies-used)
- [⚠️ Limitations](#-limitations)
- [📜 License](#-license)
- [🙏 Acknowledgments](#-acknowledgments)
- [📧 Contact](#-contact)

---

## ✨ Features

- **All 70 stats** - every `Wish.StatType` value (Health, Mana, damage, skills, crits, gold gain, etc.) in a scrollable, searchable list
- **Permanent edits** - writes to `CharacterData.statBonuses`, the game's serialized save dictionary (`[Index(12)]`) — the same pool used by stat tomes and the `/addpermenentstatbonus` console command. Values persist across save/load.
- **Live totals** - each row shows the current combined stat value (base + gear + food + skills + bonus) next to your editable bonus field
- **In-game GUI** - IMGUI window toggled with **F2** or a corner button; per-row `+`/`-` steppers, instant **CLR** removal, and **Reset All Bonuses**
- **Gold editor** - sets `worldData.coins` directly
- **Input blocking** - while open, game input is disabled through the game's own `PlayerInput.DisableInput("statEditor")` gate (same mechanism as the dev console); cursor lock/visibility state is saved on open and restored on close
- **Config fallback** - `Stat Bonuses` in `stateditor.cfg` applies bonuses automatically once in-game, no GUI needed
- **Safe & reversible** - delete the DLL to remove the mod; the original game assembly is never modified

---

## 🚀 Installation

### Prerequisites
- Sun Haven (tested on v3.1.3b)
- [BepInEx 5.4](https://bepinex.com) already installed in the game directory
- A prebuilt `StatEditorMod.dll` (in `release/` or the `StatEditorMod-v2.0.0.zip` package)

### Steps
1. Copy `StatEditorMod.dll` into:
   ```
   Sun Haven\BepInEx\plugins\StatEditorMod.dll
   ```
2. Launch the game, load a save, and press **F2** — or click the "Stat Editor" button in the top-left corner.
3. Check `BepInEx\LogOutput.log` for `Stat Editor` entries if the GUI doesn't appear.

### Build from source
1. Clone the repository.
2. Install the .NET SDK (`winget install Microsoft.DotNet.SDK.8`).
3. Point the build at your Sun Haven install — either set the `SUNHAVEN_DIR` environment variable to the game folder, or pass it on the command line:
   ```bash
   dotnet build -c Release /p:SunHavenDir="D:\path\to\Sun Haven"
   ```
4. Build:
   ```bash
   dotnet build -c Release
   ```
   If `SUNHAVEN_DIR`/`SunHavenDir` is set, the post-build step auto-copies the DLL into `BepInEx\plugins\`; otherwise the DLL lands in `bin\Release\net472\` for manual copying.

---

## ⚙️ Settings

`BepInEx\config\stateditor.cfg`:

```ini
[Settings]
## Key to open/close the stat editor window
Toggle Key = F2

## Maximum absolute value allowed for stat bonus inputs
Max Bonus Value = 99999

[Bonuses]
## Permanent stat bonuses applied automatically once in-game — a fallback if the GUI is unusable.
## Format: Name=Value pairs separated by semicolons or commas. Stat names are case-insensitive.
## The config is the source of truth for the stats it lists: listed values are set,
## removed pairs are cleared. Re-applies on file edit and on save/character switch.
## Example: Stat Bonuses = Health=500;AttackDamage=100;GoldGain=25
Stat Bonuses =
```

**GUI usage:** load a save → press F2 → type a bonus value per row → **Apply Bonuses** commits pending fields (a field of `0` removes that bonus). **CLR** removes a row's bonus instantly; **Reset All Bonuses** clears everything.

> Bonuses are *additive*, not absolute — `cur` shows the live total including base stats, gear, food, and skills. To reach a desired total: `desired - (cur - current bonus)`.

---

## 🏗️ Architecture

```
StatEditorMod/
├── StatEditorMod.cs      # single-file plugin: reflection cache, GUI, config sync
├── StatEditorMod.csproj  # net472 class library + game/BepInEx references + post-build copy
├── LICENSE               # proprietary license
└── README.md
```

- **No Harmony patches** - the mod is pure reflection reads/writes against the game's own types.
- `GameSave.Instance.CurrentSave.characterData.StatBonuses` (`Dictionary<StatType, float>`) is the persistence target; `worldData.coins` handles gold.
- Stat totals refresh through the game's own `UpdateMyStats` → `SavedStats` pipeline.
- Input gating goes through `Wish.PlayerInput.DisableInput/EnableInput` with the unique reason `"statEditor"`.

---

## 🛠️ Technologies Used

- **BepInEx 5.4** - plugin framework and configuration
- **HarmonyLib / AccessTools** - reflection helpers for resolving game types (no patches applied)
- **Unity IMGUI** - `OnGUI`/`GUILayout` window, scroll view, and text fields
- **.NET Framework 4.7.2** - matches the game's Mono runtime
- Game references (not redistributed): `Assembly-CSharp.dll`, `UnityEngine*.dll`

---

## ⚠️ Limitations

- **Game version** - built and tested against Sun Haven v3.1.3b; the research was done against decompiled v3.0.2 sources, so a game update that renames `StatBonuses`/`coins` paths would break it (failures are logged, not fatal).
- **Multiplayer** - bonuses are per-character save data and *should* sync per player, but co-op behavior is untested.
- **Additive only** - bonuses add on top of the game's own stat sources; they are not absolute overrides.
- **Balance** - extreme values (e.g. negative regen, huge damage) can produce unintended gameplay states.
- **Testing** - verified by in-game use on the author's install; there is no automated test suite.

---

## 📜 License

Proprietary license — personal, non-commercial use only. See the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **BepInEx team** — the modding framework this plugin runs on
- **Harmony team** — the reflection/patching library
- **Pixelated Milk / Sun Haven team** — the game being modded
- **Sun Haven modding community** — decompiled sources and techniques

---

## 📧 Contact

- **GitHub**: [AliesterEroan](https://github.com/AliesterEroan)
- **Nexus Mods**: [GerardDelon](https://www.nexusmods.com/profile/GerardDelon)

---

*Built with ❤️ by AliesterEroan*
