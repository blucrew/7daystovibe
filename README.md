# 7 Days to Vibe

A [BepInEx](https://github.com/BepInEx/BepInEx) plugin that drives haptic devices from in-game events in **7 Days to Die**. Take a zombie hit, fire a gun, get bitten, level up, survive a blood moon — and feel it.

Two output paths fire in parallel for every event:

- **Intiface / Buttplug** — local USB/Bluetooth devices via [Intiface Central](https://intiface.com/central/) (vibration, linear/thrust, rotation). ~5 ms latency.
- **XToys** — cloud-connected toys (e.g. DG-LAB Coyote e-stim) via an [xtoys.app](https://xtoys.app) webhook. ~100–500 ms latency.

> ⚠️ **Adult content.** This mod integrates with adult haptic hardware. 18+.

![The in-game config panel](docs/panel.png)

---

## Features

- **49 events** across 6 categories — Combat, Status Effects, World Events, Activities, Vehicles, Stealth.
- **Per-event tuning** — enable/disable, intensity (0–1), duration, pattern (vibrate / pulse), target device slot, and actuator (vibrate / linear / rotate, by index).
- **In-game config panel** — open with **`Insert`**. Live device status, per-event **Test** buttons, a colour-coded log tab, and an XToys setup guide. Auto-scales to your resolution.
- **Damage/impact scaling** — events like Player Damage, Explosion and Fall Landing scale intensity with how hard you got hit.
- **Two simultaneous outputs** — Intiface and XToys both fire; use whichever hardware you have.

---

## Requirements

| | |
|---|---|
| **7 Days to Die** | Tested on **V 2.6 (b14)**. Must be launched **without EasyAntiCheat** (see below). |
| **BepInEx** | **5.x — x64 (Mono)**. Not BepInEx 6 / IL2CPP. |
| **Intiface Central** | For local devices. Run it and "Start Server" before launching the game. Optional if you only use XToys. |
| **XToys account** | Optional, for cloud devices. See the in-game XToys tab for setup. |

### EasyAntiCheat must be OFF

BepInEx injects a proxy DLL, which EAC blocks (you'll get a `0xc000007b` crash). Launch via the game launcher with **EasyAntiCheat disabled**, or run `7DaysToDie.exe` directly with `-noeac`. The game logs `anticheat disabled` when you're on the right path.

---

## Install (players)

1. Install **BepInEx 5 x64 (Mono)** into your 7DTD folder, then launch the game once and quit so it generates the `BepInEx/` folders.
2. Download the latest release and copy **all** of its `.dll` files into:
   ```
   <7 Days To Die>\BepInEx\plugins\
   ```
   (The plugin ships with its dependencies — Buttplug, the WebSocket connector, etc. They all need to be in `plugins/`.)
3. Start **Intiface Central** and connect your device(s).
4. Launch the game **without EAC**, load a save, and press **`Insert`** to open the panel.

Settings live in `BepInEx/config/com.rustyblu.7dtd_haptics.cfg` and also in the in-game panel (changes save automatically).

---

## Build (developers)

Requires the **.NET SDK** and a local copy of the game (for the reference assemblies).

```sh
git clone https://github.com/blucrew/7daystovibe.git
cd 7daystovibe
# Point the build at your install:
#   edit <GameDir> in 7DTD_Haptics.csproj
dotnet build
```

On a successful build, the plugin **and all its dependencies** are copied straight into `<GameDir>\BepInEx\plugins\`.

### Layout

| Path | What |
|---|---|
| `Plugin.cs` | BepInEx entry point; resilient Harmony patching. |
| `HapticsConfig.cs` | All config bindings + the per-event `EventConfig`. |
| `ButtplugManager.cs` | Intiface/Buttplug connection and command dispatch. |
| `XToysManager.cs` | XToys webhook output. |
| `HapticsGUI.cs` / `HapticsTheme.cs` | In-game IMGUI panel and its dark-mode theme. |
| `Patches/` | Harmony patches, one file per event group. |
| `HapticsConfigUI.html` | Standalone browser-based config editor (generates a `.cfg`). |
| `tools/` | Helper scripts for finding renamed methods across game versions. |

### A note on game versions

7 Days to Die renames and moves classes between major versions. Patches are applied **one class at a time** — if a target method doesn't exist on your version, that single event is skipped (and logged in the panel's Log tab) instead of breaking the whole mod. If an event isn't firing, check the Log tab for a `✗ skipped` line and update the method name in the relevant `Patches/*.cs` file. `tools/FindMethods.csx` helps locate the new name.

---

## Credits

Built by **RustyBlu**. Haptics via [Buttplug / Intiface](https://buttplug.io) and [XToys](https://xtoys.app).

## License

[GPLv3](LICENSE).
