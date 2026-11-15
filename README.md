# MusicTagger CLI

Console tool for fixing MP3 metadata in an offline music library. The
requirements baseline is `docs/MusicTagger_SRS.md`.


## Problem

MP3 files downloaded from the internet often play correctly and still have
missing or wrong tags. Players group a library by artist, album, title, or
track number, so those files show up as Unknown, sort out of order, or become
hard to find.

Fixing them by hand is slow, and a bad write can drop tags or touch the audio
itself. Older players also disagree on ID3 versions and text encoding, so
Vietnamese and other non-ASCII text can turn into mojibake after a save.


## Solution

MusicTagger CLI scans a folder of `.mp3` files, lets you edit one file or a
batch, shows the old and new values, and writes only after you confirm.

Safety is part of the edit, not a separate step:

- A JSON backup of the current tags is written before any file is changed. If that backup fails, that file is left untouched.
- Only the fields you choose are updated. Empty input means keep the current value. Clearing a field is an explicit action.
- The audio stream stays as it is. The tool re-reads the file after writing and checks that the saved tags match.
- You can restore an earlier backup from the menu.
- The default tag format is ID3v2.3 with Unicode text, because many older players handle that more reliably than ID3v2.4. ID3v2.4 is available when you want it.
- Paths and tag text stay Unicode end to end, including Vietnamese.

Settings and backup manifests are JSON files. Tags live in the MP3. There is no database.


## What you can do

1. Scan a music folder. Scan the selected folder only, or include subfolders.
2. Pick one file, several files, or every MP3 in the scan.
3. Set title, artist, album, and the other supported text fields.
4. Preview the change list, then confirm or cancel.
5. Watch per-file progress and a final count of updated, failed, and skipped files.
6. Restore tags from a backup manifest.

The smallest set the tool must edit well is title, artist, and album. Genre, year, track number, disc number, composer, and comment are included when the metadata library handles them reliably.


## Compatibility

Target runtime is .NET 10 on Windows, Linux, and macOS.

ID3v2.3 is the default write format. ID3v2.4 can store UTF-8, but some older MP3 players still display non-ASCII text incorrectly. A successful read-back in this app means the bytes were stored as specified. It does not mean every player will render Vietnamese the same way.

The console falls back to plain text when the terminal has no color.


## Prerequisites

- .NET SDK net10.0


## Build and run

```bash
dotnet build
dotnet run --project src/MusicTaggerCli
```


## Out of scope for the MVP

Later versions may add album art, online metadata lookup, audio fingerprinting, and formats other than MP3. This version edits MP3 tags only. It does not download music, rename files, or change the audio stream.
