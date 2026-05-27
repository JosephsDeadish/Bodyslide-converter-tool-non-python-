# SlideSmith (Standalone, C#)

This repository contains the SlideSmith standalone .NET conversion tool (current version `0.1`) that bundles core conversion stages into one app:

- import scan (single `.nif`, armor folder, or zipped archive)
- body signature detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE + CUSTOM fallback); bone-name scoring from physics XML
- mesh type analysis (cloth/leather/plate/skin-tight/physics-enabled/mixed)
- deformation cage generation
- mesh conversion strategy selection
- weight transfer + skeleton bone mapping (source → target, unsupported bone detection)
- morph generation with **11 regional fields** (chest, waist, pelvis, legs, shoulders, breasts, butt, belly, arms, thighs, calves) tuned per body type
- partition rebuilding (BSDismemberSkinInstance slot assignment)
- clipping detection + auto-correction pass
- physics profile generation (CBPC + SMP XML config file output)
- **vanilla armor database** — 65+ canonical Skyrim / DLC armors matched by mesh token for automatic profile recommendations
- **voxel collision detection** — 8×8×8 grid penetration scan after auto-correction; per-region push-out offsets logged per mesh type
- **deformation profile modifier** — fine-tunes regional morphs using named profiles (curvy, slim, petite, athletic, muscular, lean)
- **BodySlide `.osp` project generation** — outputs a valid BodySlide slider-set XML alongside each converted armor
- **texture analysis** — detects DDS textures, classifies diffuse / normal / specular / glow / parallax / subsurface, identifies missing normal maps
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

# show supported body types with detection tokens and vertex-count hints
dotnet run --project src/Bodyslide.Standalone -- --list-bodies
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
| `plugin-patches.json` | Detected mesh paths from sidecar plugins + structured xEdit patch guidance (`ProposedPatchSteps` with per-mesh actions, no ESP is written) |
| `texture-summary.json` | Texture audit: DDS count per type (diffuse/normal/specular/glow/parallax/subsurface), missing normal maps |
| `preview-renders.json` | Metadata-only preview descriptor: target body, mesh type, regional morph values, active physics nodes, slider list, and per-view capture hints (no actual render) |
| `.conversion-learning-cache.json` | Learning cache for faster repeated conversions |
| `fomod/ModuleConfig.xml` + `fomod/info.xml` | FOMOD metadata generated for mod manager packaging |

When a matching cache entry exists in the selected output folder for the same armor mesh + target body, the converter now reuses prior regional morphing data and marks `learning-cache:hit` / `learning-cache:reused` in pipeline steps.

## Issue #2 progress comparison

Implemented from issue scope:
- import scan across single mesh, folder, and zip archive
- body detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE, CUSTOM fallback); bone-name scoring from physics XML for higher confidence
- mesh analysis, cage/strategy stages, weight transfer, morph generation, partition rebuild, clipping detect/correct, physics configs
- plugin scan, texture summary (6 DDS categories), vanilla armor lookup (65+ entries), voxel collision pass, BodySlide OSP output, BSD/TRI slider data, learning cache reuse
- automatic CI builds with PR approval-gated Windows packaging and merge-time Windows `.exe` artifact publishing
- FOMOD metadata output (`fomod/ModuleConfig.xml`, `fomod/info.xml`)
- **output `.nif` file(s)** written to the output directory; `_0`/`_1` weight variant pairs detected and written as matched pairs
- **`--source` flag** to override auto-detected source body type (`--source CBBE`, etc.)
- **`--list-bodies` flag** to enumerate all supported body types with detection tokens
- **source→target relative delta conversion** — `StrategyMeshConversionService` now computes `targetField[region] / sourceField[region]` per region so converting e.g. CBBE→UNP applies only the directional difference rather than the full UNP field; emits `conversion-delta:CBBE→UNP` step
- **vanilla recommended profile auto-apply** — when the vanilla armor database identifies a match and no explicit `--profile` was provided, its `RecommendedProfile` is automatically applied (emits `vanilla-profile:<name>` step)
- **armor region binding by bone names** — new `IArmorRegionBindingService` / `BasicArmorRegionBindingService` detects which body regions (chest, waist, pelvis, legs, shoulders, arms, breasts, belly, butt) the armor covers by scoring physics-file bone name tokens, falling back to mesh filename keywords, then full-body default; emits `regions:<list>,method=<detection-method>` step
- **geometry signature scan** — lightweight NIF vertex-count/bounds sampling now feeds body detection evidence (`verts:<count>`) and adds a `spatial-geometry` fallback for armor region binding when readable mesh coordinates are available
- **batch summary report** — converting a directory or `.zip` now writes `batch-report.json` to the root output folder with total/success/fail counts, target body, timestamp, and per-armor result entries
- **live preview HTML** (`preview.html`) — self-contained browser-openable file with an inline SVG body silhouette where each region is colour-coded by morph factor (blue→green→yellow→orange→red scale), plus regional morphing table, BodySlide slider list, physics-node list, and pose-clipping risk summary; replaces the old metadata-only `preview-renders.json`
- **plugin xEdit automation script** (`patch-armor.pas`) — generated alongside `plugin-patches.json` whenever plugins are detected; a runnable Pascal (Delphi) script for SSEEdit/TES5Edit that iterates ARMA records, matches detected mesh paths, and emits placement instructions — drop it into the Edit Scripts folder and run via Tools → Apply Script
- **pose simulation** (`pose-simulation-report.json`) — `BasicPoseSimulationService` tests the converted mesh against 8 animation poses (T-pose, Walk, Run, Idle, Crouch, Combat-Idle, Jump, Sneak) using per-pose per-region stress amplifiers; regions where `morph_factor × pose_amplifier ≥ 1.10` are flagged as at-risk; report written as JSON and visualised in the preview HTML; emits `pose-simulation:tested=8,...` pipeline step

Remaining gap versus full issue vision:
- deformation/physics logic is rule-based with mesh-type-aware parameter tuning (cloth→softer, plate→stiffer), not a full geometry/animation engine with real vertex transformation

## Current built-in presets (27 total)

| Preset | Target Body | Deformation | Physics |
|---|---|---|---|
| 3BA Curvy | 3BA | curvy | smp+cbpc |
| 3BA Slim | 3BA | slim | smp+cbpc |
| 3BA Athletic | 3BA | athletic | smp+cbpc |
| BHUNP Curvy | BHUNP | curvy | smp+cbpc |
| BHUNP Slim | BHUNP | slim | smp+cbpc |
| BHUNP Athletic | BHUNP | athletic | smp+cbpc |
| CBBE Curvy | CBBE | curvy | none |
| CBBE Slim | CBBE | slim | none |
| CBBE Athletic | CBBE | athletic | none |
| CBBE Petite | CBBE | petite | none |
| UNP Petite | UNP | petite | cbpc |
| UNP Athletic | UNP | athletic | cbpc |
| UNP Curvy | UNP | curvy | cbpc |
| UNP Slim | UNP | slim | cbpc |
| TBD Lean | TBD | lean | cbpc |
| TBD Curvy | TBD | curvy | cbpc |
| TBD Athletic | TBD | athletic | cbpc |
| SAM Athletic | SAM | athletic | smp |
| SAM Lean | SAM | lean | smp |
| SAM Muscular | SAM | muscular | smp |
| SOS Lean | SOS | lean | smp |
| SOS Athletic | SOS | athletic | smp |
| UBE Petite | UBE | petite | none |
| UBE Curvy | UBE | curvy | none |
| HIMBO Lean | HIMBO | lean | smp |
| HIMBO Muscular | HIMBO | muscular | smp |
| HIMBO Athletic | HIMBO | athletic | smp |

## Supported body types

**Female:** CBBE, 3BA, UNP, BHUNP, TBD, UBE  
**Male:** HIMBO, SAM, SOS  
**Custom:** any unrecognised body falls back to `CUSTOM` detection

Use `--list-bodies` to see detection tokens and vertex-count hints for each body type.

## Vanilla armor database

The pipeline automatically identifies 65+ canonical Skyrim / DLC armors (Iron, Steel, Elven, Glass, Daedric, Dragonplate, Nightingale, Orcish, Stalhrim, Nordic Carved, Bonemold, Dawnguard, Dragonborn DLC sets, etc.) by matching mesh file tokens (stripped of `_0`/`_1` weight suffixes). When a match is found, the armor's recommended deformation profile is applied unless overridden by `--profile`.

```
vanilla-armor:Iron Armor,rec=curvy
```

Unrecognised armor files emit `vanilla-armor:unknown` in the pipeline steps.

## Texture classification

The texture analysis pass classifies each valid DDS file into one of six categories based on its filename suffix:

| Category | Suffixes |
|---|---|
| Diffuse | (everything else) |
| Normal | `_n`, `_normal` |
| Specular | `_s`, `_spec`, `_specular` |
| Glow / Emissive | `_g`, `_glow`, `_em` |
| Parallax / Height | `_p`, `_parallax`, `_h` |
| Subsurface | `_sk`, `_subsurface`, `_sss` |

Counts for all six types are written to `texture-summary.json` and `conversion-manifest.json`.

## Voxel collision detection

After the auto-correction pass, each converted mesh is scanned with an 8×8×8 voxel grid. When the morph factor for a region exceeds the per-mesh-type threshold (e.g. plate ≥ 1.10, cloth ≥ 1.04) a push-out magnitude is computed (`excess × 8`) and emitted per affected region:

```
voxel-collision:penetrations=2,grid=8,torso=0.8,arms=0.4
```

If no penetrations are detected the step emits `voxel-collision:none`.



If the input is a directory or `.zip` archive, all `.nif` files are converted in one run and exported into per-armor output folders.
