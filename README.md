# 🎸 YARG

> **Warning**
>
> YARG is **not done yet**! Expect incomplete features and bugs!
>
> ...And terrible menus!

**Y**et **A**nother **R**hythm **G**ame inspired off of Rockband, Guitar Hero, Clone Hero, or similar.

![Gif of YARG](gif.gif)

# 📥 Downloading and Playing

Windows:
1. [Click here](https://github.com/EliteAsian123/YARG/releases) to view all releases.
2. Download the lastest zip file by clicking on the "Assets" dropdown and then clicking on `YARG_vX.X.X.zip`.
3. Extract the contents of the zip file by right clicking it and pressing "Extract All..."
4. Click "Extract".
5. Open the extracted folder and double-click `YARG.exe` (if you don't have file extensions on, it is called just `YARG`)
6. You may get a "Windows protected your PC" error. This is because not many people have ran the program before, so Windows does not know if it is harmful or not. Click on "More info" and then "Run anyway" to run YARG anyways. If you don't trust me, please feel free to scan the folder with an anti-virus. Please note that some anti-viruses may have the same problem as Windows.
7. Once you load in, click on "SETTINGS"
8. Then, choose your song folder. You can browse folder by click on the `B`.
9. Once you've chosen your folder, click on "Select Folder". Please be sure that the folder has at least one song in it.
10. Next click on "ADD/EDIT PLAYERS". This part is a little scuffed still.
    1. Click on "Add Player"
    2. Then click on the dropdown box and select the device you will be playing with.
    3. Click on what type of instrument you will be playing with (i.e. "Five Fret", "Microphone", etc.)
    4. Depending on the input type, you may have to bind keys on the very right side. To do this, click on each button and press the key of choice on your controller.
11. Finally, click on "QUICK PLAY". YARG will cache all of the files into a `yarg_cache.json` file in the folder you chose. Doing this may take a while depending on the amount of songs you have. If you ever add more songs, **be sure** to go to "SETTINGS" and then click on "Refresh Cache". This will add the new songs into "QUICK PLAY".
12. Have fun!

# 🛡️ License

YARG is licensed under the MIT License - see the [`LICENSE`](../master/LICENSE) file for details.

# 🔨 Building

1. Clone repository.
2. Open it in Unity version `2022.2.0b16` (or newer)
3. Load in **without** entering safe mode.
4. Click on `NuGet` on the menu bar.
5. Click on `Restore Packages`.

# 📦 External Assets and Libraries

| Link | Type | Use |
| --- | --- | --- |
| [Righteous](https://fonts.google.com/specimen/Righteous) | Font | Headers, Logo
| [Unbounded](https://fonts.google.com/specimen/Unbounded) | Font | Combo/Multipier Meter
| [Barlow](https://fonts.google.com/specimen/Barlow) | Font | UI Font
| [Material Symbols](https://fonts.google.com/icons) | Icons | UI Icons
| [PlasticBand](https://github.com/TheNathannator/PlasticBand) | Reference | Controller Support Info
| [EliteAsian's Unity Extensions](https://github.com/EliteAsian123/EliteAsians-Unity-Extensions) | Library | Utility
| [Unity Standalone File Browser](https://github.com/gkngkc/UnityStandaloneFileBrowser) | Library | "Browse" Button
| [FuzzySharp](https://www.nuget.org/packages/FuzzySharp) | Library | Search Function
| [ini-parser](https://www.nuget.org/packages/ini-parser-netstandard) | Library | Parsing `song.ini` Files
| [DryWetMidi](https://www.nuget.org/packages/Melanchall.DryWetMidi) | Library | Parsing `.mid` Files
| [TagLibSharp](https://www.nuget.org/packages/TagLibSharp) | Library | Finding Audio Metadata


# 💸 Donate

Some people have expressed interest in donating. I just do this for fun and as a hobby, and as such, money is *not* something that you need to give me by any means. However, if you really want to, scroll up and under the "Sponsor this project" heading there is a link so you can donate. If you do such, I'd really appreciate, but again, it is not required.
