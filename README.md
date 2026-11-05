# arrkit

Shared code for Yann's media apps: [Ripparr](https://github.com/yannb2/ripparr),
[Remuxarr](https://github.com/yannb2/remuxarr), [Salvarr](https://github.com/yannb2/salvarr)
and [Specarr](https://github.com/yannb2/specarr).

Each app adds this repository as a git submodule, so there is no package to publish and no token
needed in CI or Docker builds.

## What is in it

| Namespace | What it does |
|---|---|
| `Arrkit.Subtitles` | PGS (Blu-ray picture subtitle) reader and renderer, PNG writer, Tesseract OCR, SDH cleaner, the SDH/commentary/forced classifier, French region (fr-CA or fr-FR) guess, side-by-side caption alignment, and `TrackSampler` (reads a few slices of a track from a big MKV with ffmpeg, plus Listen) |
| `Arrkit.Logging` | `MemoryLog` and its logger provider: the app's last 2000 log lines in memory, newest first, for a Logs page (Ripparr, Salvarr, Specarr) |
| `Arrkit.Library` | The media library: `LibraryFolders` (film and series folders under /movies and /tv, by category), `ExtraTypes` (Jellyfin extras folders and a guess from a file name), `VideoFiles` (video extensions, the videos in a folder), `ExtrasPlan` + `ExtrasCopier` (copy extras into a title's folder, read back and compared, never overwriting), `FileNames` (safe names, season folders), `JellyfinScan` (tell Jellyfin about new files). Ripparr and Scratcharr use it |
| `Arrkit.Tables` | `SortHeader` (a column header you click to sort, with an arrow) and `TableSort` (the shared sort rules: every column both ways, sizes and dates biggest or newest first on the first click, missing values last, column and direction in the URL). The app's CSS styles `.sv-sort`. Specarr uses it; Ripparr and Scratcharr have their own copies to switch later |
| `Arrkit.Links` | `SpecarrLink` and the `OpenInSpecarr` button: "Open in Specarr" for any file or folder (Specarr's `/open?path=` page finds it in the library or /downloads). Scratcharr and Ripparr use it, with `<App>__SpecarrUrl` set to Specarr's address |
| `Arrkit.Files` | `Mounts`: the container's mount points (from /proc/self/mountinfo) and `HasDisk` / `WithDisk`, which tell a plugged-in USB disk or share from an empty mount point (an empty folder on its parent's mount). Scratcharr's External page, Specarr's External browse and Remuxarr's donor folders list only the disks that are there |
| `Arrkit.Processes` | `ToolRunner`, a small runner for ffmpeg and tesseract. Each app keeps its own runner for long jobs with progress |

The library has no package references (only the ASP.NET Core framework reference, for `ILogger`),
so it builds inside an app that uses central package management (Muxarr) as well as one that
doesn't (Ripparr, Salvarr, Specarr).

## Using it in an app

```sh
git submodule add https://github.com/yannb2/arrkit arrkit
```

Then reference `arrkit/src/Arrkit/Arrkit.csproj` from the app's project, check out submodules in
CI (`actions/checkout` with `submodules: true`), and copy the `arrkit` folder into the Docker
build before `dotnet restore`.

To take a newer arrkit in an app: `git -C arrkit pull origin main`, then commit the submodule change.

## Tools the live tests need

`ffmpeg` and `tesseract` (with English) on PATH. Without them those tests are skipped as
inconclusive; CI installs both.

```sh
dotnet test
```
