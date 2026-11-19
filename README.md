# TinyScholar’s (Sims 3 Pack Mods Folder) Switcher

A portable Windows program for switching your Sims 3 mod folders by expansion pack, stuff pack and Store world. Built with C# and WPF.

I sort my mods by pack and wanted a way to switch the folders I actually need without moving everything in and out of Mods by hand every time. So I made this little program!

## Download and setup

**[Download the program and find setup instructions on Tumblr](https://thetinyscholar.tumblr.com/post/832009571557163008/tinyscholars-sims-3-pack-mods-folder-switcher)**

This repository contains the source code for **Switcher v1.03**. If you just want to use the program, follow the link above for the ready-to-run download and optional folder templates.

## What it does

- Supports folders for 11 expansion packs, 9 stuff packs and 11 Store worlds.
- Copies selected mod folders from STORAGE into your active Mods/Packages and Mods/Overrides folders.
- Shows which managed folders are active.
- Warns about missing folders, duplicate filenames and folders being removed or replaced.
- Includes dark and light themes and remembers your paths and theme preference.

**You supply and organize your own mods.** The Switcher doesn't download or automatically sort mods, or install, enable or disable the actual game packs or Store worlds.

## Before using it

Close The Sims 3 and back up your entire Mods folder before making changes.

The Switcher can replace or remove folders with its 31 recognized pack names inside Mods/Packages and Mods/Overrides. EP00_BaseGame and unrelated folders are outside that managed list.

Please read the setup instructions before applying your first loadout. Duplicate warnings compare filenames, not mod contents or compatibility.

## Building from source

On Windows, with a .NET SDK installed:

1. Download or clone this repository.
2. Run BUILD_WINDOWS.bat.
3. Find the compiled application in the dist folder.

The project targets .NET Framework 4.8 and uses WPF. Running the program requires .NET Framework 4.8 or compatible 4.8.1.

## Development note

I used ChatGPT to help write and revise the program's code, and tested the program on my own Windows 11 computer.

## License

The source code is available under the [PolyForm Noncommercial License 1.0.0](LICENSE.txt).
