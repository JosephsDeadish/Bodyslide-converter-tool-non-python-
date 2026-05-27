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

# produce a mod-manager-ready ZIP instead of a bare output folder
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --output-zip

# show built-in presets
dotnet run --project src/Bodyslide.Standalone -- --list-presets
```

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

If the input is a directory, all `.nif` files are converted in one run and exported into per-armor output folders.
