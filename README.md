# arrkit

Shared code for Yann's media apps: [Ripparr](https://github.com/yannb2/ripparr),
[Muxarr (fork)](https://github.com/yannb2/muxarr), and later Specarr and Salvarr.

Each app adds this repository as a git submodule, so there is no package to publish and no token
needed in CI or Docker builds.

## What is in it

| Namespace | What it does |
|---|---|
| `Arrkit.Subtitles` | PGS (Blu-ray picture subtitle) reader and renderer, PNG writer, Tesseract OCR, SDH cleaner, the SDH/commentary/forced classifier, French region (fr-CA or fr-FR) guess, side-by-side caption alignment, and `TrackSampler` (reads a few slices of a track from a big MKV with ffmpeg, plus Listen) |
| `Arrkit.Processes` | `ToolRunner`, a small runner for ffmpeg and tesseract. Each app keeps its own runner for long jobs with progress |

The library has no package references, so it builds inside an app that uses central package
management (Muxarr) as well as one that doesn't (Ripparr).

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
