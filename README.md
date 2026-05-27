# Bodyslide Converter Tool (Standalone, C#)

This repository contains a standalone .NET conversion tool that bundles core conversion stages into one app:

- import scan (single `.nif`, armor folder, or zipped archive)
- body signature detection
- mesh type analysis (cloth/leather/plate/skin-tight/mixed)
- deformation cage generation
- mesh conversion strategy selection
- weight transfer + morph generation
- clipping detection + auto-correction pass
- physics profile generation
- export package + manifest/log output

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

# show built-in presets
dotnet run --project src/Bodyslide.Standalone -- --list-presets
```

## Current built-in presets

- `3BA Curvy`
- `HIMBO Lean`
- `UNP Petite`

## Batch behavior

If the input is a directory, all `.nif` files are converted in one run and exported into per-armor output folders.
