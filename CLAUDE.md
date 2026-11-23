# FluxProHWiNFO — handoff for Claude Code

A small Windows app that shows CPU and GPU temperatures on the Antec Flux Pro's built-in side-panel display. It reads the temperatures from **HWiNFO64**, not from its own sensor library.

## Goals
- Use HWiNFO as the only sensor source. The display then matches what the user sees in HWiNFO, and the app needs no admin rights, no PawnIO, WinRing0 or LibreHardwareMonitor, and no kernel driver of its own.
- Make it lightweight and invisible: a system-tray app (or a windowless background process) that starts at logon.
- Keep working when HWiNFO is closed or restarted: blank the display and reconnect, never crash.

## Owner / credits rule (important)
- Credit the author ONLY as **Radlic** (GitHub: Radlic) in README, LICENSE, assembly metadata, commits and anything else public.
- Never put the owner's real name or personal email in any file, commit or metadata.

## Target machine
- Windows 11 x64. Ryzen 7 9800X3D on an MSI MAG X670E Tomahawk WiFi, Zotac RTX 5070 Ti, Antec Flux Pro case.
- CPU, GPU and RAM are all overclocked, so temperatures can run high under load.
- The .NET SDK is installed (`~/.dotnet` exists). Run `dotnet --list-sdks` first and target the newest LTS that's installed (net8.0-windows or net10.0-windows).

## Data source: HWiNFO "Gadget" registry interface (NOT shared memory)
- **Do not use HWiNFO Shared Memory (SHM).** In free HWiNFO64 it switches itself off after 12 hours and has to be re-enabled by hand.
- Use the Gadget/VSB registry values instead. HWiNFO writes them for every sensor the user marks "Report value in Gadget":
  - Key: `HKEY_CURRENT_USER\Software\HWiNFO64\VSB`
  - For each index `i` there are string values `Sensor{i}`, `Label{i}`, `Value{i}` (formatted with unit), `ValueRaw{i}` (number as text) and `Color{i}`.
  - Example: `Sensor0 = "CPU [#0]: AMD Ryzen 7 9800X3D: Enhanced"`, `Label0 = "CPU (Tctl/Tdie)"`, `ValueRaw0 = "54.3"`.
- Indices shift whenever the user changes which sensors are reported. **Match by label (optionally sensor substring), not by index**: re-scan the key every poll, or whenever a match goes missing.
- Parse `ValueRaw` with InvariantCulture, and also accept `,` as the decimal separator to be safe.
- HKCU is per-user, so HWiNFO and this app must run as the same Windows user. Don't make the app elevate itself: an elevated process can see a different HKCU hive.
- If HWiNFO deletes the registry lines while a sensor stops reporting, treat that sensor as absent (blank on the display).
- Make the labels configurable. Defaults:
  - CPU: `CPU (Tctl/Tdie)` (fallback `CPU Die (average)`)
  - GPU: `GPU Temperature`
- Possible extra feature: let the user pick a different GPU value instead, e.g. `GPU Hot Spot Temperature` or `GPU Memory Junction Temperature`.

### HWiNFO setup the user must do (put this in README)
1. Open HWiNFO64 in Sensors-only mode, then open Sensor Settings.
2. On the **HWiNFO Gadget** tab, tick "Enable reporting to Gadget".
3. Select the CPU temperature row (`CPU (Tctl/Tdie)`) and tick "Report value in Gadget". Do the same for `GPU Temperature`.
4. In HWiNFO's main Settings, set it to start with Windows, minimised, in Sensors-only mode.
5. Check the setup with `reg query HKCU\Software\HWiNFO64\VSB`.

(Confirm these menu names against the user's HWiNFO version when testing.)

## Display device protocol
This spec was worked out from nishtahir/antec-flux-pro-display (`src/usb.rs`, GPL-3.0). **Write our own implementation from this spec. Don't copy that code**, so the project's licence stays the owner's choice.

- USB VID `0x2022`, PID `0x0522`.
- The Linux version claims interface 0 and writes to the first **interrupt OUT** endpoint (fallback `0x03`) with a 1000 ms timeout. No response is read.
- **On Windows the device most likely enumerates as HID.** The existing Windows app (techtronika/antec-flux-pro-display-lightweight, net10.0-windows) depends on the `hidlibrary` NuGet package, which suggests it writes HID output reports. Use **HidSharp** (maintained, MIT) or HidLibrary to open the device by VID/PID and write the payload. Report-ID handling has to be confirmed on real hardware: try writing the 12 bytes with a leading `0x00` report ID and compare against the device's output report length. Log the HID capabilities (OutputReportByteLength etc.) on startup to help debug.
- Payload, 12 bytes:

| Byte | Value |
|---|---|
| 0 | `0x55` |
| 1 | `0xAA` |
| 2 | `0x01` |
| 3 | `0x01` |
| 4 | `0x06` |
| 5–7 | CPU temp: tens digit, units digit, tenths digit |
| 8–10 | GPU temp: tens digit, units digit, tenths digit |
| 11 | checksum = (sum of bytes 0–10) & 0xFF |

- A missing value is encoded as `0xEE 0xEE 0xEE` (the display shows blank/dashes).
- Test vectors (must be unit tests):
  - CPU 24.0, GPU 16.0 → `55 AA 01 01 06 02 04 00 01 06 00 14`
  - CPU 24.0, GPU missing → `55 AA 01 01 06 02 04 00 EE EE EE D7`
- Improvements over the Linux version:
  - Round to one decimal place before splitting into digits. The original truncates, so float error turns 24.7 into 24.6.
  - Clamp to 0.0–99.9. The original's behaviour at 100 °C or above is undefined. Write the chosen behaviour down in the README.
  - On a clean exit, or when HWiNFO is gone, send blanks (`EE`) for both values.

## Behaviour
- Poll about once a second (configurable). HWiNFO's own sensor poll defaults to 2000 ms, so faster polling gains nothing.
- If the device isn't found or the write fails, retry every few seconds. Handle unplug/replug and sleep/resume.
- Config: a small JSON file next to the exe or in `%APPDATA%\FluxProHWiNFO\config.json`, holding the labels, poll interval and an optional °F flag (convert before encoding).
- Logging: a rolling text log in `%APPDATA%\FluxProHWiNFO\`. Keep it quiet.
- Tray icon menu: show current CPU/GPU values and the matched sensor names, Open config, Open log, Start with Windows (on/off), Exit.
- Autostart: a Task Scheduler task at logon for the current user, *not* elevated. A Startup-folder shortcut also works, because no admin rights are needed.
- Only one instance at a time (named mutex).

## Conflicts to warn about in README
- Don't run Antec iUnity or techtronika's FluxProDisplay alongside this app. They'd fight over the display.

## Suggested plan
1. `dotnet --list-sdks`, then scaffold a solution: `FluxProHWiNFO` (WinForms tray app) and `FluxProHWiNFO.Tests` (xUnit).
2. Write the payload encoder plus unit tests using the test vectors above.
3. Write the HWiNFO VSB reader plus a `--dump-sensors` CLI flag that prints every index, sensor, label and raw value (helps pick labels).
4. Write the HID writer plus a `--test-pattern` flag that sends fixed values, e.g. 12.3 / 45.6, to confirm the protocol on real hardware.
5. Add the polling loop, tray UI, config, logging and autostart.
6. Write the README (setup steps, HWiNFO config, troubleshooting) and pick a licence with the owner (credit "Radlic").
7. Publish framework-dependent and self-contained builds.

## Status
- Steps 1–4 done: solution scaffolded (net10.0-windows), payload encoder, HWiNFO VSB reader with `--dump-sensors`, HID writer with `--hid-info`, `--test-pattern` and `--blank`.
- **Confirmed on real hardware (2026-11-23):** the display enumerates as one HID interface, vendor usage page `0xFF00`, no report IDs, output report 65 bytes (1 report-ID byte + 64 data), no feature reports. The working framing is `id0-pad`: `0x00` report ID + 12-byte payload + zero padding to 65 bytes, written with HidSharp `HidStream.Write`. Test pattern 12.3 / 45.6 displayed correctly.
- The device returns junk for its manufacturer/product strings (`Љ`), so don't rely on them.
- Next: step 5 (polling loop, tray UI, config, logging, autostart).
