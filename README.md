# SuperPotato
### Control your resources for free performance!

![image](https://github.com/user-attachments/assets/64277fd3-383a-46cc-85b6-7b96efabcd4a)

## Features
- Simple presets with ability for advanced control
- Easy drop in VRAM (~500mb) without much quality loss
- Difficult to notice in-motion (on Medium preset)
- Additional ability to crank GFX even higher than normal

## Installation
1. Download [MelonLoader](https://github.com/LavaGang/MelonLoader/releases/latest) and install v0.6.1 onto your `Neon White.exe`.
2. Run the game once. This will create required folders.
3. Download and follow the installation instructions for [NeonLite](https://github.com/Faustas156/NeonLite).
    - NeonLite is **required** for this mod.
4. Download `SuperPotato.dll` from the [Releases page](https://github.com/stxticOVFL/SuperPotato/releases/latest) and drop it in the `Mods` folder.

## Building & Contributing
This project uses Visual Studio 2022 as its project manager. When opening the Visual Studio solution, ensure your references are corrected by right clicking and selecting `Add Reference...` as shown below. 
Most will be in `Neon White_data/Managed`. Some will be in `MelonLoader/net35`, **not** `net6`. Select the `NeonLite` mod for that reference. 
If you get any weird errors, try deleting the references and re-adding them manually.

![image](https://github.com/user-attachments/assets/8c9b7408-d217-477a-93b1-12b565c8ee4b)

Once your references are correct, build using the keybind or like the picture below.

![image](https://github.com/stxticOVFL/EventTracker/assets/29069561/40a50e46-5fc2-4acc-a3c9-4d4edb8c7d83)

Make any edits as needed, and make a PR for review. PRs are very appreciated.