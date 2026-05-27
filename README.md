# Bodyslide Converter Tool (Standalone, C#)

This repository contains a standalone .NET conversion tool that bundles core conversion stages into one app:

- import scan (single `.nif`, armor folder, or zipped archive)
- body signature detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE + CUSTOM fallback)
- mesh type analysis (cloth/leather/plate/skin-tight/physics-enabled/mixed)
- deformation cage generation
- mesh conversion strategy selection
- weight transfer + skeleton bone mapping (source → target, unsupported bone detection)
- morph generation
- partition rebuilding (BSDismemberSkinInstance slot assignment)
- clipping detection + auto-correction pass
- physics profile generation
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
# simple positional mode
dotnet run --project src/Bodyslide.Standalone -- "<armor path>" "<target body>" "<optional output directory>"

# named mode with preset support
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path|folder|zip>" --preset "3BA Curvy" --output "<optional output directory>"

# apply a deformation profile (overrides the preset's built-in profile)
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --profile curvy

# produce a mod-manager-ready ZIP instead of a bare output folder
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --output-zip

# show built-in presets
dotnet run --project src/Bodyslide.Standalone -- --list-presets

# show available deformation profiles
dotnet run --project src/Bodyslide.Standalone -- --list-profiles
```

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
| `<ArmorName>.nif` | Converted mesh |
| `<ArmorName>.osp` | BodySlide slider-set project (open in BodySlide Studio) |
| `conversion-manifest.json` | Full conversion log with all pipeline steps |
| `dependency-map.json` | Per-mesh dependency map linking related textures, physics, body refs, and plugin mesh references |
| `plugin-patches.json` | Mesh path references found in sidecar plugins + patch guidance |
| `texture-summary.json` | Texture audit: DDS count, missing normal maps, unrecognised files |
| `preview-renders.json` | Preview metadata for downstream rendering integration |
| `.conversion-learning-cache.json` | Learning cache for faster repeated conversions |

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

## Batch behavior

If the input is a directory or `.zip` archive, all `.nif` files are converted in one run and exported into per-armor output folders.
