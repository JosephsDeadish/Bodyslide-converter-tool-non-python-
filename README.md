# SlideSmith (Standalone, C#)

This repository contains the SlideSmith standalone .NET conversion tool (current version `0.1`) that bundles core conversion stages into one app:

- import scan (single `.nif`, armor folder, or zipped archive)
- body signature detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE + CUSTOM fallback)
- mesh type analysis (cloth/leather/plate/skin-tight/physics-enabled/mixed)
- deformation cage generation
- mesh conversion strategy selection
- weight transfer + skeleton bone mapping (source → target, unsupported bone detection)
- morph generation
- partition rebuilding (BSDismemberSkinInstance slot assignment)
- clipping detection + auto-correction pass
- physics profile generation (CBPC + SMP XML config file output)
- **vanilla armor database** — 33 canonical Skyrim armors matched by mesh token for automatic profile recommendations
- **voxel collision detection** — 8×8×8 grid penetration scan after auto-correction; per-region push-out offsets logged per mesh type
- **deformation profile modifier** — fine-tunes regional morphs using named profiles (curvy, slim, petite, athletic, muscular, lean)
- **BodySlide `.osp` project generation** — outputs a valid BodySlide slider-set XML alongside each converted armor
- **texture analysis** — detects DDS textures, identifies missing normal maps, classifies diffuse vs normal
- **plugin scanning** — binary-scans `.esp`/`.esm`/`.esl` sidecar files for NIF mesh paths and generates patch guidance
- export package + manifest/log output
- conversion learning cache output (`.conversion-learning-cache.json`) for repeated runs
- preview metadata output (`preview-renders.json`) for downstream rendering integration
- optional ZIP output (`--output-zip`) for mod-manager-ready packages

## Projects

- `/src/Bodyslide.Core` - standalone conversion pipeline + modules
- `/src/Bodyslide.Standalone` - runnable app entry point
- `/tests/Bodyslide.Core.Tests` - focused orchestration and batch/preset tests

## Run

```bash
# build executable (SlideSmith.exe on Windows publish output)
dotnet publish src/Bodyslide.Standalone/Bodyslide.Standalone.csproj --configuration Release --runtime win-x64 --self-contained false

# simple positional mode
dotnet run --project src/Bodyslide.Standalone -- "<armor path>" "<target body>" "<optional output directory>"

# named mode with preset support
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path|folder|zip>" --preset "3BA Curvy" --output "<optional output directory>"

# apply a deformation profile (overrides the preset's built-in profile)
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --profile curvy

# override the auto-detected source body type
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --source "UNP" --target "3BA"

# produce a mod-manager-ready ZIP instead of a bare output folder
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --output-zip

# show built-in presets
dotnet run --project src/Bodyslide.Standalone -- --list-presets

# show available deformation profiles
dotnet run --project src/Bodyslide.Standalone -- --list-profiles
```

## GitHub Actions (automatic build)

This repository includes `.github/workflows/build.yml`, which runs automatically on pushes to `main`/`master` and on pull requests.

What it does:
- **Every push/PR:** restore, build, test, and publish a Linux standalone artifact.
- **Push to `main`/`master` (post-merge):** publish and upload Windows app files (`SlideSmith.exe` + dependencies) as `slidesmith-win-x64-release`.
- **Pull requests:** includes an approval-gated Windows publish job (`pr-build-approval`) so you can approve packaging on each small PR session before merge.

To require manual approval in PR builds, set required reviewers for the `pr-build-approval` environment in repository settings.

## Deformation profiles

Pass `--profile <name>` to scale regional morphs toward or away from the neutral body shape. The amplifier scales `(value − 1)` so a value of 1.0 (no change) is always preserved.

| Profile | Amplifier | Effect |
|---|---|---|
| curvy | 1.15 | Amplifies curves beyond the base shape |
| muscular | 1.25 | Strongest amplification of all dimensions |
| athletic | 1.08 | Subtle amplification with a toned look |
| lean | 0.88 | Reduces bulk while keeping proportions |
| slim | 0.82 | Visibly slimmer than the neutral body |
| petite | 0.75 | Smallest overall body dimensions |

## Output files

Each successful conversion produces the following files in the output directory:

| File | Description |
|---|---|
| `<ArmorName>.nif` | Converted mesh (copied from source; vertex geometry transform applied by the pipeline) |
| `<ArmorName>_0.nif` + `<ArmorName>_1.nif` | Low/high-weight variant pair when both are detected in input |
| `<ArmorName>.osp` | BodySlide slider-set project (open in BodySlide Studio) |
| `cbpc-config.xml` | CBPC physics config (breast/butt/belly for female; pec/belly for male) |
| `smp-config.xml` | SMP physics config (NPC Breast01, NPC Belly, NPC Butt nodes, etc.) |
| `conversion-manifest.json` | Full conversion log with all pipeline steps |
| `dependency-map.json` | Per-mesh dependency map linking related textures, physics, body refs, and plugin mesh references |
| `plugin-patches.json` | Mesh path references found in sidecar plugins + patch guidance |
| `texture-summary.json` | Texture audit: DDS count, missing normal maps, unrecognised files |
| `preview-renders.json` | Preview metadata for downstream rendering integration |
| `.conversion-learning-cache.json` | Learning cache for faster repeated conversions |
| `fomod/ModuleConfig.xml` + `fomod/info.xml` | FOMOD metadata generated for mod manager packaging |

When a matching cache entry exists in the selected output folder for the same armor mesh + target body, the converter now reuses prior regional morphing data and marks `learning-cache:hit` / `learning-cache:reused` in pipeline steps.

## Issue #2 progress comparison

Implemented from issue scope:
- import scan across single mesh, folder, and zip archive
- body detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE, CUSTOM fallback)
- mesh analysis, cage/strategy stages, weight transfer, morph generation, partition rebuild, clipping detect/correct, physics configs
- plugin scan, texture summary, vanilla armor lookup, voxel collision pass, BodySlide OSP output, BSD/TRI slider data, learning cache reuse
- automatic CI builds with PR approval-gated Windows packaging and merge-time Windows `.exe` artifact publishing
- FOMOD metadata output (`fomod/ModuleConfig.xml`, `fomod/info.xml`)
- **output `.nif` file(s)** written to the output directory; `_0`/`_1` weight variant pairs detected and written as matched pairs
- **`--source` flag** to override auto-detected source body type (`--source CBBE`, etc.)

Still partial / placeholder versus full issue vision:
- live preview rendering is metadata-only placeholder
- plugin editing is guidance output (not direct ESP mutation)
- deformation/physics logic is rule-based simulation, not full geometry/animation engine

## Current built-in presets

| Preset | Target Body | Deformation | Physics |
|---|---|---|---|
| 3BA Curvy | 3BA | curvy | smp+cbpc |
| 3BA Slim | 3BA | slim | smp+cbpc |
| HIMBO Lean | HIMBO | lean | smp |
| HIMBO Muscular | HIMBO | muscular | smp |
| UNP Petite | UNP | petite | cbpc |
| UNP Athletic | UNP | athletic | cbpc |
| BHUNP Curvy | BHUNP | curvy | smp+cbpc |
| BHUNP Slim | BHUNP | slim | smp+cbpc |

## Supported body types

**Female:** CBBE, 3BA, UNP, BHUNP, TBD, UBE  
**Male:** HIMBO, SAM, SOS  
**Custom:** any unrecognised body falls back to `CUSTOM` detection

## Vanilla armor database

The pipeline automatically identifies 33 canonical Skyrim armors (Iron, Steel, Elven, Glass, Daedric, Dragonplate, Nightingale, etc.) by matching mesh file tokens (stripped of `_0`/`_1` weight suffixes). When a match is found, the armor's recommended deformation profile is applied unless overridden by `--profile`.

```
vanilla-armor:Iron Armor,rec=curvy
```

Unrecognised armor files emit `vanilla-armor:unknown` in the pipeline steps.

## Voxel collision detection

After the auto-correction pass, each converted mesh is scanned with an 8×8×8 voxel grid. When the morph factor for a region exceeds the per-mesh-type threshold (e.g. plate ≥ 1.10, cloth ≥ 1.04) a push-out magnitude is computed (`excess × 8`) and emitted per affected region:

```
voxel-collision:penetrations=2,grid=8,torso=0.8,arms=0.4
```

If no penetrations are detected the step emits `voxel-collision:none`.



If the input is a directory or `.zip` archive, all `.nif` files are converted in one run and exported into per-armor output folders.
