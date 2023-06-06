> **Warning**
>
> YARG is **not done yet**! Expect incomplete features and bugs!

<br/>

<div align="center">
  <img src="Images/YARG_Full.png" width="60%" alt="YARG">

  <br/>
  <br/>

  <a href="https://twitter.com/EliteAsian123">
    <img src="https://raw.githubusercontent.com/gauravghongde/social-icons/master/PNG/Color/Twitter.png" width="48px" height="48px" alt="Twitter">
  </a>
  <a href="https://discord.gg/sqpu4R552r">
    <img src="https://discord.com/assets/847541504914fd33810e70a0ea73177e.ico" height="48px" width="48px" alt="Discord">
  </a>

  <br/>
  <br/>

  <img src="Images/README_Top.png" width="80%" alt="README top">
  <img src="Images/README_Gif.gif" width="80%" alt="README gif">
</div>

# 👉 Disclaimer

We **DO NOT** encourage, advocate, or promote **PIRATING** of songs, or of anything else. This game's intended use is for you, the player, to play songs that you already own. This means, ripping songs of a game **YOU OWN** for **YOURSELF** for **PERSONAL USE**, or downloading creative commons/public domain songs off of the internet.

**YARG** has nothing to do with pirates. It stands for "Yet Another Rhythm Game."

# 📃 Table of Contents

- [Downloading and Playing](#-downloading-and-playing)
  - [Windows](#windows)
  - [Mac](#mac)
  - [Linux](#linux)
  - [In-Game Setup](#in-game-setup)
- [Building](#-building)
  - [Setup Instructions](#setup-instructions)
  - [Unity YAML Merge Tool](#unity-yaml-merge-tool)
- [Contributing](#️-contributing)
- [License](#️-license)
- [External Licenses](#-external-licenses)
- [External Assets and Libraries](#-external-assets-and-libraries)
- [Donate](#-donate)

# 📥 Downloading and Playing

**An official installation video is [available here](https://www.youtube.com/watch?v=bSPSttKNnKc).**

A community made one is [available as well](https://youtu.be/hEJHuAGGlD8).

## Windows

1. Go to [the latest release](https://github.com/EliteAsian123/YARG/releases/latest) and click on the "Assets" dropdown, then click on `YARG_vX.X.X-Windows-x64.zip` to download.
2. Extract the contents of the zip file by right clicking it and pressing "Extract All..."
3. Choose where you want to extract it to, then click "Extract".
4. Open the extracted folder and double-click `YARG.exe` (if you don't have file extensions on, it is called just `YARG`)
5. You may get a "Windows protected your PC" warning. This is because not many people have run YARG before, so Windows does not know if it is harmful or not. Click on "More info" and then "Run anyway" to run it anyways. If you don't trust me, please feel free to scan the folder with an anti-virus, and remember that false positives can still happen.

## Mac

1. Go to the [the latest release](https://github.com/EliteAsian123/YARG/releases/latest) and click on the "Assets" dropdown, then click on `YARG_vX.X.X-MacOS-Universal.dmg` to download.
2. Open the downloaded .dmg and drag the YARG app to your Apps folder.
3. Double-click the YARG app to run it.

## Linux

1. Go to [the latest release](https://github.com/EliteAsian123/YARG/releases/latest) and click on the "Assets" dropdown, then click on `YARG_vX.X.X-Linux-x86_64.zip` to download.
2. Extract the zip to the location of your choosing.
3. Inside the folder you extracted the game to, open a terminal and run `chmod +x ./YARG.x86_64` to give the game executable permission.
4. You can now double-click the `YARG.x86_64` file or use `./YARG.x86_64` in a terminal to run the game, however there are some dependencies that will be needed for HID devices (such as PS3 and Wii instruments).
5. Next, install `hidapi` and `libudev`:
  - (Package names may differ depending on package repositories.)
  - On apt-based distros (such as Ubuntu or Debian), use `sudo apt install libhidapi-hidraw0 libudev1`.
  - On pacman-based distros (such as Arch Linux), use `pacman -S hidapi systemd-libs`.
  - On Fedora, use `dnf install hidapi systemd-libs`.
6. Finally, create a new udev rules file called `69-hid.rules` inside of `/etc/udev/rules.d/` or `/usr/lib/udev/rules.d/`, with the following contents:
  ```
  KERNEL=="hidraw*", TAG+="uaccess"
  ```
  - Without this file, YARG will not be able to access HID devices without special permissions such as being run with `sudo`, which is not recommended.
  - The file name may differ if desired, but it must come before `73-seat-late.rules`!
7. Reboot your system to apply the new udev rule, then you should be all good to go!

## In-Game Setup

- Set up your song folders:
  1. From the main menu, click on "SETTINGS", then click on "Open Song Folder Manager"
  2. Next, click on "Add Folder." A new entry should pop-up.
  3. Click on the folder icon to open a folder picker, then choose the folder your songs are stored in.
  4. Repeat the previous two steps for each of your song folders.
  5. Now, click on "Refresh All Caches" to make YARG scan that folder for songs. Doing this may take a while depending on the amount of songs you have. If you ever add more songs, **be sure** to come back here and click on "Refresh All Caches".
- Set up your controllers:
  1. From the main menu, click on "ADD/EDIT PLAYERS".
  2. Click "Add Player" and select the controller you wish to use for that player (or select "Create a BOT" to create a bot player).
  3. Enter in a name for this player, and select what type of instrument you will be playing from the dropdown below it (i.e. "Five Fret", "Microphone", etc.).
  4. Depending on the instrument type, you may have to bind some controls. To do this, click on each mapping and press the control you want to map to it.
- Finally, click on "QUICKPLAY" to enter the song list.

Have fun!

# 🔨 Building

### ⚠️ If you wish to contribute, use the `dev` branch. Your PR will NOT be merged if it's on `master`. ⚠️

## Setup Instructions

> **Warning**
>
> If you would like to build the game yourself, please follow these instructions.
>
> If you don't follow these instructions, **YOU WILL NOT BE ABLE TO RUN THE GAME**.

1. Make sure you have the latest version of [Blender](https://www.blender.org/) installed. This is for loading models, even if you don't plan on editing them.
2. Make sure you have [Python (3.10)](https://www.python.org/downloads/) or greater installed. Be sure it is added to system path. This is required to download dependencies.
3. Clone the repository. If you don't know how to do this:
  1. Download [Git](https://git-scm.com/downloads). Be sure it is added to system path.
  2. Open the command prompt in the directory you want to store the repository.
  3. Type in `git clone https://github.com/YARC-Official/YARG.git`.
4. Install Unity Hub and Unity `2021.3.21f1` (LTS).
  1. Download and install [Unity Hub](https://unity.com/download).
  2. Sign-in/create an account with a personal license (free).
  3. In Unity Hub, click on "Install Editor" and select `2021.3.21f1` (LTS). It may be favourable to unselect Visual Studio if you are not using it.
  4. Click "Install"
5. Open the command prompt at the root of the repo, and type in:
  1. `pip install requests`
  2. `python InstallLibraries/install.py`. This may take a bit. Wait for the command prompt to say "Done!" before closing. This installs all of the needed dependencies for you.
6. Open the project in Unity (select "Open" and select YARG's repo's folder).
7. Load in **without** entering safe mode. Click "Ignore".
8. (You may need to) click on `NuGet` on the menu bar, then click on `Restore Packages`.
9. You're ready to go!

## Unity YAML Merge Tool

Sometimes merge conflicts may happen between Unity scenes. These can be much more difficult to resolve than other merge conflicts, so we recommend using the Unity YAML merge tool to resolve these instead of attempting to do so manually.

Setup:
1. Open a command prompt to the repository (on VS Code you can do Terminal > New Terminal)
2. Type in `git config --local --edit`
3. In the file that gets opened, go to the bottom and paste this in:
  ```
  [merge]
      tool = unityyamlmerge
  [mergetool "unityyamlmerge"]
      trustExitCode = false
      cmd = 'C:\\Program Files\\Unity\\Hub\\Editor\\2021.3.21f1\\Editor\\Data\\Tools\\UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
  ```
  - You may need to change the file path depending on where you installed Unity to.
4. Save and close the file.

Resolving conflicts:
1. Start the merge/cherry-pick which is causing conflicts.
2. If the conflict doesn't resolve automatically, open the command prompt and use `git merge-tool`.
3. Verify that the conflict was resolved correctly, then commit/continue the merge.

# ✍️ Contributing

If you want to contribute, please feel free! Please join [our Discord](https://discord.gg/sqpu4R552r) if you want your PR/Art merged.

# 🛡️ License

YARG is licensed under the MIT License - see the [`LICENSE`](../master/LICENSE) file for details.

# 🧰 External Licenses

Some libraries/assets are **packaged** with the source code have licenses that must be included.

| Library | License |
| --- | --- |
| [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity) | [MIT](https://github.com/GlitchEnzo/NuGetForUnity/blob/master/LICENSE)
| [Unity Standalone File Browser](https://github.com/gkngkc/UnityStandaloneFileBrowser) | [MIT](https://github.com/gkngkc/UnityStandaloneFileBrowser/blob/master/LICENSE.txt)
| [Discord GameSDK](https://discord.com/developers/docs/game-sdk/sdk-starter-guide) | Licenseless
| [Lucide](https://lucide.dev/) | [ISC](https://lucide.dev/license)
| [DtxCS](https://github.com/maxton/DtxCS) | Licenseless
| [Moonscraper](https://github.com/FireFox2000000/Moonscraper-Chart-Editor) | [BSD 3-Clause License](https://github.com/FireFox2000000/Moonscraper-Chart-Editor/blob/master/LICENSE)

Please note that other libraries are **not** packaged within the source code, and are to be install by NuGet.

BASS is the audio library for YARG. [It has it's own license for release](https://www.un4seen.com/).

# 📦 External Assets and Libraries

| Link | Type | Use |
| --- | --- | --- |
| [Unbounded](https://fonts.google.com/specimen/Unbounded) | Font | Combo/Multipier Meter
| [Barlow](https://fonts.google.com/specimen/Barlow) | Font | UI Font
| [Material Symbols](https://fonts.google.com/icons) | Icons | UI Icons
| [Lucide](https://lucide.dev/) | Icons | UI Icons
| [PolyHaven](https://polyhaven.com/) | Assets | Textures and Models
| [PlasticBand](https://github.com/TheNathannator/PlasticBand) | Reference | Controller Support Info
| [PlasticBand-Unity](https://github.com/TheNathannator/PlasticBand-Unity) | Library | GH/RB Controller Support
| [HIDrogen](https://github.com/TheNathannator/HIDrogen) | Library | Linux HID Controller Support
| [GuitarGame_ChartFormats](https://github.com/TheNathannator/GuitarGame_ChartFormats) | Reference | File Format Documentation
| [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity) | Library | NuGet Packages in Unity
| [EliteAsian's Unity Extensions](https://github.com/EliteAsian123/EliteAsians-Unity-Extensions) | Library | Utility
| [Unity Standalone File Browser](https://github.com/gkngkc/UnityStandaloneFileBrowser) | Library | "Browse" Button
| [FuzzySharp](https://www.nuget.org/packages/FuzzySharp) | Library | Search Function
| [ini-parser](https://www.nuget.org/packages/ini-parser-netstandard) | Library | Parsing `song.ini` Files
| [DryWetMidi](https://www.nuget.org/packages/Melanchall.DryWetMidi) | Library | Parsing `.mid` Files
| [TagLibSharp](https://www.nuget.org/packages/TagLibSharp) | Library | Finding Audio Metadata
| [Minis](https://github.com/keijiro/Minis/tree/master) | Library | MIDI Input for Unity
| [Discord GameSDK](https://discord.com/developers/docs/game-sdk/sdk-starter-guide) | Library | Discord Rich Presence
| [DtxCS](https://github.com/maxton/DtxCS) | Library | Parsing `.dta` Files
| [Moonscraper](https://github.com/FireFox2000000/Moonscraper-Chart-Editor) | Library | Parsing `.chart` Files
| [DOTween](https://github.com/Demigiant/dotween) | Library | Animation Utility
| [UniTask](https://github.com/Cysharp/UniTask) | Library | Async Library
| [unity-toolbar-extender](https://github.com/marijnz/unity-toolbar-extender/) | Library | Unity Editor Stuff

# 💸 Donate

Some people have expressed interest in donating. This is an open-source project and therefore donating is not required. If you do want to still help out, spread the word or contribute!
