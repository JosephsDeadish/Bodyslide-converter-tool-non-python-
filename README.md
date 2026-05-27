# Bodyslide Converter Tool (Standalone, C#)

This repository now contains a standalone .NET solution that bundles a full conversion pipeline surface in one app:

- NIF/armor import stage
- mesh analysis + conversion strategy
- weight transfer stage
- morph generation stage
- physics profile stage
- local export with conversion manifest

## Projects

- `/src/Bodyslide.Core` - conversion pipeline orchestration and built-in standalone modules
- `/src/Bodyslide.Standalone` - runnable app entry point (`install app -> provide armor path -> choose target body -> convert`)
- `/tests/Bodyslide.Core.Tests` - focused orchestration tests

## Run

```bash
dotnet run --project src/Bodyslide.Standalone -- "<armor path>" "<target body>" "<optional output directory>"
```
