# FluxProHWiNFO

Shows your CPU and GPU temperatures on the **Antec Flux Pro** case's built-in side-panel display, using **HWiNFO64** as the sensor source.

- The display shows the same numbers you see in HWiNFO.
- No admin rights, no kernel driver, no sensor library of its own. HWiNFO does the sensor reading.
- Runs quietly in the system tray and can start with Windows.
- Keeps working when HWiNFO is closed or restarted, and when the display is unplugged or the PC sleeps: it shows dashes and reconnects.

By [Radlic](https://github.com/Radlic). MIT licence.

## Requirements

- Windows 10 or 11, 64-bit.
- An Antec Flux Pro case with its temperature display connected to an internal USB header.
- [HWiNFO64](https://www.hwinfo.com/) (the free version is fine). Tested with HWiNFO64 8.50.
- For the small download only: the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64). The self-contained download needs nothing extra.
- To build from source: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

> **Don't run Antec iUnity or FluxProDisplay at the same time.** Only one program can drive the display; two will fight over it and the numbers will flicker between them.

## 1. Set up HWiNFO

FluxProHWiNFO reads the values HWiNFO publishes for desktop gadgets. It does **not** use HWiNFO's Shared Memory Support, which the free version switches off every 12 hours.

1. Open HWiNFO64 and choose **Sensors-only** mode.
2. In the Sensors window, click the **Configure** (gear) button to open Sensor Settings, and go to the **HWiNFO Gadget** tab.
3. Tick **Enable reporting to Gadget**.
4. In the list, find your CPU, click the **CPU (Tctl/Tdie)** row and tick **Report value in Gadget**. (On Intel CPUs, use a row such as **CPU Package** and set it in the config, see below.)
5. Scroll down to your graphics card. HWiNFO 8.50 lists a dedicated card as `dGPU`, for example `[dGPU [#0]: NVIDIA GeForce RTX 5070 Ti: Zotac GeForce RTX 5070 Ti]`. Click its **GPU Temperature** row and tick **Report value in Gadget**. If your CPU has integrated graphics, make sure you pick the dedicated card, not the integrated one.
6. Check the list: each row you ticked shows **Yes** in the right-hand column.
7. Click **OK**. Nothing is published until you do, and closing the window another way may discard the changes.
8. In HWiNFO's main **Settings**, turn on starting with Windows, minimised, in Sensors-only mode, so it's running whenever this app is.

Check that HWiNFO is publishing the values:

```powershell
reg query HKCU\Software\HWiNFO64\VSB
```

You should see `Sensor0`, `Label0`, `ValueRaw0` and so on, one group per ticked row. Or use the app's own check, which also shows what it will match:

```powershell
.\FluxProHWiNFO.exe --dump-sensors | Out-Host
```

## 2. Install and run FluxProHWiNFO

There are three downloads. All contain the same app as a single `FluxProHWiNFO.exe`:

| Download | Size | Use it if |
|---|---|---|
| `FluxProHWiNFO-<version>-win-x64-setup.exe` | about 33 MB | you want a normal setup wizard with a Start menu entry and an uninstaller. Nothing else to install. |
| `FluxProHWiNFO-<version>-win-x64-selfcontained.zip` | about 44 MB (111 MB unzipped) | you want a portable copy with nothing else to install |
| `FluxProHWiNFO-<version>-win-x64.zip` | under 1 MB | you want a portable copy and have the .NET 10 Desktop Runtime installed, or don't mind installing it (if it's missing, the app shows a message with a download link when you start it) |

All use about 20 MB of memory while running.

### With the setup wizard

1. Run `FluxProHWiNFO-<version>-win-x64-setup.exe`. Setup isn't code-signed, so Windows may show "Windows protected your PC": click **More info**, then **Run anyway**.
2. Choose who to install for:
   - **Install for me only** (the default) needs no admin rights. It installs to `%LOCALAPPDATA%\Programs\FluxProHWiNFO` and adds a Start menu entry for you.
   - **Install for all users** asks for admin rights (UAC). It installs to `C:\Program Files\FluxProHWiNFO` and adds a Start menu entry for everyone on the PC.
3. Leave **Start with Windows** ticked if you want it to start when you sign in (with **all users**, it starts for everyone who signs in).
4. Leave **Start FluxProHWiNFO now** ticked on the last page. Even after an all-users install, the app starts as you, not as administrator.

If the app is already running (for example when upgrading), Setup asks you to exit it first: tray icon → **Exit**, then click **OK** in Setup.

### Portable (zip)

1. Extract the zip to a folder where it can stay, for example `%LOCALAPPDATA%\Programs\FluxProHWiNFO`. Don't run it from inside the zip or from Downloads if you plan to start it with Windows: the startup entry points at the exe's location.
2. Start `FluxProHWiNFO.exe`. The exe isn't code-signed, so Windows may show "Windows protected your PC" the first time: click **More info**, then **Run anyway**.

### Either way

A thermometer icon appears in the system tray (it may be under the **^** overflow arrow), and the display lights up with your temperatures. Each Windows user has their own config and log in `%APPDATA%\FluxProHWiNFO`, and needs their own HWiNFO set up as in step 1.

Run it as your normal user, **not** "as administrator": HWiNFO writes its values to your own user's registry, and the app must read them as the same user.

### Tray icon

| Icon colour | Meaning |
|---|---|
| Blue | All good: display connected, both temperatures found |
| Amber | Display connected, but something is missing: HWiNFO isn't running, a sensor isn't found, or the config has an error |
| Grey | Display not found; the app retries every 3 seconds |

Hover over the icon for the current values. Right-click it for:

- **CPU / GPU**: the current values and which HWiNFO sensor each one comes from.
- **HWiNFO** and **Display** status.
- **Open config**, **Reload config** and **Open log**.
- **Start with Windows**: on or off.
- **Exit**: the display shows dashes, then goes dark about 2 seconds later.

### Start with Windows

Tick **Start with Windows** in the tray menu. This adds the app to your user's startup list (the `HKCU\...\CurrentVersion\Run` registry entry, the same thing a Startup-folder shortcut does) and marks it **Enabled** in Task Manager's startup list. It needs no admin rights and doesn't run elevated. You can also turn it off in **Task Manager → Startup apps**.

If you used version 0.1.0, tick **Start with Windows** again (it may already show as unticked). 0.1.0 added the startup entry without marking it Enabled, and on at least some Windows 11 builds such an entry is skipped at sign-in.

If you move the exe, tick it again so the entry points at the new location.

After an **all users** install with Start with Windows ticked, the entry is in `HKLM\...\CurrentVersion\Run` instead. It starts the app for every user who signs in, still as that user and not elevated. The tray menu then shows **Start with Windows: on for all users**, greyed out, because changing it needs admin rights. To change it, run Setup again and untick the box.

## What the display shows

- Temperatures are rounded to one decimal place (24.66 → 24.7).
- The display has two digits before the decimal point, so values are **clamped to 0.0–99.9**: anything at 99.95 or above shows as **99.9**, and anything below zero shows as **0.0**.
- A value that isn't available (HWiNFO closed, Gadget reporting off, sensor not ticked or not found) shows as **dashes**. The other value still shows.
- **The display goes dark about 2 seconds after it stops receiving data**, and it can't be woken with the case button while nothing is sending. The app sends once a second the whole time it runs, which keeps the screen on.

## Configuration

Settings live in `%APPDATA%\FluxProHWiNFO\config.json`, created with defaults on first run. If a `config.json` exists next to the exe, that one is used instead (handy for a portable copy). Changes are picked up within about 2 seconds; no restart is needed. If you make a mistake in the file, the app keeps its previous settings and shows the error in the tray menu and the log.

Defaults:

```json
{
  "cpu": {
    "labels": [ "CPU (Tctl/Tdie)", "CPU Die (average)" ],
    "sensorContains": null
  },
  "gpu": {
    "labels": [ "GPU Temperature" ],
    "sensorContains": null
  },
  "pollIntervalMs": 1000,
  "fahrenheit": false
}
```

| Setting | Meaning |
|---|---|
| `labels` | HWiNFO row names to look for, in order of preference. The first one that has a value is used. Matching ignores upper/lower case but must otherwise be exact. An empty list (`[]`) leaves that value as dashes. |
| `sensorContains` | Optional text the HWiNFO **sensor group** name must contain. Use it when two devices report the same row name, for example `"RTX"` to pick the NVIDIA card over the CPU's integrated graphics. |
| `pollIntervalMs` | How often to read HWiNFO and update the display, in milliseconds. Allowed range 250–1000. It can't go above 1000 because the display turns off after about 2 seconds without data. HWiNFO itself updates every 2 seconds by default, so faster than 1000 gains little. |
| `fahrenheit` | `true` shows °F. Because the display tops out at 99.9, a CPU or GPU above 99.9 °F (37.7 °C), which is nearly always, shows as **99.9**. °C is recommended. |

The rows don't have to be temperatures, and the match uses the row name only. Examples:

```jsonc
// Show the GPU hot spot instead of the core temperature, from the NVIDIA card only
"gpu": { "labels": [ "GPU Hot Spot Temperature" ], "sensorContains": "RTX" }

// Show VRAM temperature
"gpu": { "labels": [ "GPU Memory Junction Temperature" ] }
```

Remember to tick **Report value in Gadget** in HWiNFO for any row you name here.

## Command-line tools

These are for setup and troubleshooting. In PowerShell, add `| Out-Host` so the prompt waits for the output.

| Command | What it does |
|---|---|
| `FluxProHWiNFO.exe --dump-sensors` | Lists every value HWiNFO publishes, and what the default labels match. |
| `FluxProHWiNFO.exe --hid-info` | Shows the display's USB HID details. Writes nothing to it. |
| `FluxProHWiNFO.exe --test-pattern` | Shows 12.3 / 45.6 on the display for 30 seconds. Options: `--cpu <value>`, `--gpu <value>`, `--seconds <n>`. |
| `FluxProHWiNFO.exe --blank` | Sends dashes once. |
| `FluxProHWiNFO.exe --help` | Shows all options. |

`--test-pattern` and `--blank` refuse to run while the tray app is running, since both would write to the display. Exit the tray app first.

## Troubleshooting

**The display stays dark.**
- Check that the tray icon is there. If it's grey, the display wasn't found: check its internal USB cable, and look in Device Manager for "HID-compliant vendor-defined device" (USB ID `VID_2022&PID_0522`).
- Run `--hid-info` (with the tray app closed) to see whether Windows can see the display, then `--test-pattern` to check that it lights up.
- Make sure iUnity and FluxProDisplay aren't running.

**The display shows dashes for one or both values.**
- Hover over the tray icon or open its menu: it says whether HWiNFO is running and which sensor was matched.
- Run `--dump-sensors`. If the row you want isn't listed, HWiNFO isn't publishing it: tick **Report value in Gadget** for that row (its right-hand column should read **Yes**) and click **OK** to close Sensor Settings (see step 1). Ticked rows don't appear until you click OK.
- **A row shows Yes in the Gadget tab but still isn't published?** Check that **monitoring is enabled** for that row in HWiNFO. HWiNFO only publishes rows it is monitoring, so a row whose monitoring was turned off (for example to tidy the Sensors window) stays missing even with "Report value in Gadget" ticked.
- If the row is listed under a different name, put that name in `labels` in the config.
- If HWiNFO is closed, both values show dashes until it starts again. The app checks for it every 5 seconds.

**The GPU value comes from the wrong device** (for example the CPU's integrated graphics).
- Set `"sensorContains": "RTX"` (or another part of your card's name, as shown by `--dump-sensors`) under `gpu` in the config, or untick that row for the integrated GPU in HWiNFO.

**Nothing matches, even though `reg query` shows the values.**
- HWiNFO and FluxProHWiNFO must run as the same Windows user. Don't start FluxProHWiNFO "as administrator" or as a different user.

**"FluxProHWiNFO is already running".**
- Only one copy can run at a time. Look for the thermometer in the tray (and under the **^** overflow arrow).

**Where are the files?**
- Config: `%APPDATA%\FluxProHWiNFO\config.json`
- Log: `%APPDATA%\FluxProHWiNFO\FluxProHWiNFO.log`. It records only changes (HWiNFO found or lost, display connected or lost, sensors matched, errors), and keeps one older file, `FluxProHWiNFO.1.log`, once it passes 512 KB.

## Uninstall

**Installed with Setup:** exit the app (tray icon → **Exit**), then uninstall **FluxProHWiNFO** from **Settings → Apps → Installed apps**. This removes the program, its Start menu entry and its Start with Windows entry. Your config and log stay in `%APPDATA%\FluxProHWiNFO`; delete that folder too if you want them gone. After an all-users install, other users who turned on Start with Windows from their own tray menu keep a harmless leftover entry in Task Manager → Startup apps.

**Portable copy:**

1. Untick **Start with Windows** in the tray menu, then choose **Exit**.
2. Delete the program folder.
3. Delete `%APPDATA%\FluxProHWiNFO` to remove the config and log.

## Building from source

```powershell
dotnet build
dotnet test
```

The app is `src\FluxProHWiNFO\bin\Debug\net10.0-windows\FluxProHWiNFO.exe`.

To build the release downloads (it runs the tests first) into `publish\`:

```powershell
.\build\publish.ps1
```

The setup wizard is built from `installer\FluxProHWiNFO.iss` and needs [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`). Without it, the script builds the two zips and skips Setup with a warning.

The version number comes from `Directory.Build.props`. The self-contained exe is deliberately not compressed: compression would make it 49 MB instead of 111 MB on disk, but it triples the memory the always-running app uses (from about 20 MB to about 60 MB), and the zip compresses the download anyway.

## How it talks to the display

The display enumerates as a USB HID device (VID `0x2022`, PID `0x0522`, vendor usage page `0xFF00`) with 64-byte output reports and no report IDs. Each update is a 12-byte packet, sent as one HID output report (a `0x00` report-ID byte, the packet, then zero padding to 65 bytes):

| Byte | Value |
|---|---|
| 0–4 | `55 AA 01 01 06` |
| 5–7 | CPU temperature: tens, units, tenths digit (`EE EE EE` = dashes) |
| 8–10 | GPU temperature: tens, units, tenths digit (`EE EE EE` = dashes) |
| 11 | Checksum: sum of bytes 0–10, lowest 8 bits |

## Credits and licence

- Created by [Radlic](https://github.com/Radlic). Released under the [MIT License](LICENSE).
- The display's packet format was first worked out by [nishtahir/antec-flux-pro-display](https://github.com/nishtahir/antec-flux-pro-display) (Linux). This project contains none of its code.
- Uses [HidSharp](https://software.seekye.com/hidsharp) by James F. Bellinger (Apache-2.0). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- Not affiliated with Antec or with HWiNFO / REALiX.
