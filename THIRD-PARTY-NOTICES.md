# Third-party notices

FluxProHWiNFO is licensed under the MIT License (see `LICENSE`). Builds of it include the following third-party component, under its own licence.

## HidSharp

- Version: 2.6.4 (NuGet package `HidSharp`)
- Copyright 2010-2025 James F. Bellinger, <http://software.seekye.com/hidsharp>
- Licence: Apache License, Version 2.0. The full text is in `licenses/HidSharp-LICENSE.txt`.
- Used unmodified, to open the display as a USB HID device and write reports to it.

## Not included, credited for research

- **nishtahir/antec-flux-pro-display** (GPL-3.0): its Linux driver is where the display's USB IDs and 12-byte packet format were first worked out. FluxProHWiNFO contains none of its code: the protocol was reimplemented from a written description of it and then confirmed on real hardware.
- **HWiNFO64** by Martin Malík (REALiX) is a separate program that the user installs. FluxProHWiNFO only reads the values HWiNFO publishes to the registry for gadgets, and is not affiliated with or endorsed by REALiX.
- Antec, Flux Pro and iUnity are trademarks of Antec, Inc. FluxProHWiNFO is not affiliated with or endorsed by Antec.
