# SlideSmith (Standalone, C#)

This repository contains the SlideSmith .NET conversion toolset (current version `0.1`) with both a Windows desktop GUI and a CLI app, bundling core conversion stages into one pipeline:

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
- **plugin scanning + rewrite mapping** — scans `.esp`/`.esm`/`.esl` sidecar files for ARMA mesh paths, generates rewrite mappings, and outputs an auto-rewrite xEdit script covering world + first-person model paths
- export package + manifest/log output
- conversion learning cache output (`.conversion-learning-cache.json`) for repeated runs
- live preview HTML output (`preview.html`) with region heatmap and conversion context
- optional ZIP output (`--output-zip`) for mod-manager-ready packages

## Projects

- `/src/Bodyslide.Core` - conversion pipeline + modules
- `/src/Bodyslide.Desktop` - Windows GUI app (drag/drop, preset or custom target mode, optional profile/source override, output-zip toggle, convert/cancel)
- `/src/Bodyslide.Standalone` - CLI app entry point
- `/tests/Bodyslide.Core.Tests` - focused orchestration and batch/preset tests

## Run

```bash
# build Windows desktop executable package (single-file, self-contained)
dotnet publish src/Bodyslide.Desktop/Bodyslide.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

# launch desktop GUI during development (Windows)
dotnet run --project src/Bodyslide.Desktop

# GUI features: drag/drop input, open selected input path, preset details panel, choose preset or custom target,
# optional profile/source override, optional output zip, cancel in-progress conversion, open output folder,
# embedded in-app preview pane for generated preview.html, and quick-open batch reports when available

# build CLI executable package (single-file, self-contained)
dotnet publish src/Bodyslide.Standalone/Bodyslide.Standalone.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

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
- **Every push/PR:** restore, build, test, build a Linux executable package, zip it, and upload it as an Actions artifact.
- **Push to `main`/`master` (post-merge):** build Windows desktop executable package, zip it, and upload as an Actions artifact.
- **Pull requests:** build Windows desktop executable package, zip it, and upload as an Actions artifact for testing.

These are CI build artifacts only (download from the Actions run page). No GitHub Release publishing is performed by this workflow.

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
| `<ArmorName>.nif` | Converted mesh with heuristic in-place vertex transform when a readable NIF vertex block is detected (falls back to safe copy when not detectable) |
| `<ArmorName>_0.nif` + `<ArmorName>_1.nif` | Low/high-weight variant pair; both halves written when detected, **missing half is auto-synthesised** (morph-delta scaled) when only one is present |
| `<stem>_n.dds` (stub) | Auto-generated flat tangent-space normal map for any diffuse that lacks a `_n.dds` companion; 4×4 uncompressed BGRA8 DDS, neutral outward-facing vector |
| `textures/...`, `materials/...` (`*.bgsm`/`*.bgem`), `*.xml`/`*.hkx`, `*.esp`/`*.esm`/`*.esl`, body refs (`*.tri`/`*.osp`) | Source support assets are copied into output with preserved relative paths so converted packages stay runnable |
| `<ArmorName>.osp` | BodySlide slider-set project (open in BodySlide Studio) |
| `cbpc-config.xml` | CBPC physics config (breast/butt/belly for female; pec/belly for male) |
| `smp-config.xml` | SMP physics config (NPC Breast01, NPC Belly, NPC Butt nodes, etc.) |
| `conversion-manifest.json` | Full conversion log with all pipeline steps |
| `dependency-map.json` | Per-mesh dependency map linking related textures, physics, body refs, and plugin mesh references |
| `plugin-patches.json` | Detected sidecar plugin mesh paths + structured rewrite mappings (`OriginalMeshPath` → `RewrittenMeshPath`) and per-mesh patch steps |
| `patch-armor.pas` | xEdit Pascal automation script (SSEEdit / TES5Edit): runs ARMA mesh-path rewriting directly inside the tool |
| `<PluginName>_patched.<ext>` | **Full-copy patched plugin** — a direct copy of the source `.esp`/`.esm`/`.esl` with every ARMA `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh-path subrecord that matched a converted NIF updated in-place; preserves the original plugin extension (`.esp`, `.esm`, or `.esl`), supports both Skyrim LE (20-byte record headers) and Skyrim SE / SSE (24-byte headers), and is only produced when at least one path was rewritten |
| `<PluginName>_SlidesmithPatch.esp` | **Minimal override patch ESP** — contains ONLY the patched ARMA records and lists the original plugin as its master; proper Bethesda override plugin safe to load after the original; only produced when at least one ARMA path matched the rewrite map |
| `README.txt` | Human-readable installation guide: lists all generated files, where to put them, how to apply the patch ESP, how to build BodySlide morphs, and any manual finishing steps required |
| `texture-summary.json` | Texture audit: DDS count per type (diffuse/normal/specular/glow/parallax/subsurface), missing normal maps |
| `preview.html` | Browser-openable live preview report with regional morph heatmap, pose-clipping summary, and interactive controls (swap body profile, rotate view, adjust sliders) |
| `pose-simulation-report.json` | Per-pose clipping-risk report used by preview HTML (T-pose, walk, run, idle, crouch, combat-idle, jump, sneak) |
| `batch-report.json` | Root batch summary when input is a folder or `.zip` (total/success/fail counts and per-armor results) |
| `.conversion-learning-cache.json` | Learning cache for faster repeated conversions |
| `fomod/ModuleConfig.xml` + `fomod/info.xml` | FOMOD metadata generated for mod manager packaging |

When a matching cache entry exists in the selected output folder for the same armor mesh + target body, the converter now reuses prior regional morphing data and marks `learning-cache:hit` / `learning-cache:reused` in pipeline steps.

## Issue #2 progress comparison

Implemented from issue scope:
- import scan across single mesh, folder, and zip archive
- body detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE, CUSTOM fallback); bone-name scoring from physics XML for higher confidence
- mesh analysis, cage/strategy stages, weight transfer, morph generation, partition rebuild, clipping detect/correct, physics configs
- plugin scan, texture summary (6 DDS categories), vanilla armor lookup (65+ entries), voxel collision pass, BodySlide OSP output, BSD/TRI slider data, learning cache reuse
- automatic CI builds with Linux/Windows executable zip artifacts uploaded in Actions for PR and merge testing (no release publishing)
- FOMOD metadata output (`fomod/ModuleConfig.xml`, `fomod/info.xml`)
- **output `.nif` file(s)** written to the output directory; `_0`/`_1` weight variant pairs detected and written as matched pairs
- **`--source` flag** to override auto-detected source body type (`--source CBBE`, etc.)
- **`--list-bodies` flag** to enumerate all supported body types with detection tokens
- **source→target relative delta conversion** — `StrategyMeshConversionService` now computes `targetField[region] / sourceField[region]` per region so converting e.g. CBBE→UNP applies only the directional difference rather than the full UNP field; emits `conversion-delta:CBBE→UNP` step
- **vanilla recommended profile auto-apply** — when the vanilla armor database identifies a match and no explicit `--profile` was provided, its `RecommendedProfile` is automatically applied (emits `vanilla-profile:<name>` step)
- **armor region binding by bone names** — new `IArmorRegionBindingService` / `BasicArmorRegionBindingService` detects which body regions (chest, waist, pelvis, legs, shoulders, arms, breasts, belly, butt) the armor covers by scoring physics-file bone name tokens, falling back to mesh filename keywords, then full-body default; emits `regions:<list>,method=<detection-method>` step
- **geometry signature scan** — lightweight NIF vertex-count/bounds sampling now feeds body detection evidence (`verts:<count>`) and adds a `spatial-geometry` fallback for armor region binding when readable mesh coordinates are available
- **batch summary report** — converting a directory or `.zip` now writes `batch-report.json` to the root output folder with total/success/fail counts, target body, timestamp, and per-armor result entries
- **live preview HTML** (`preview.html`) — self-contained browser-openable file with an inline SVG body silhouette where each region is colour-coded by morph factor (blue→green→yellow→orange→red scale), plus regional morphing table, BodySlide slider list, physics-node list, pose-clipping risk summary, and interactive controls to swap body profile, rotate view, and adjust sliders; replaces the old metadata-only `preview-renders.json`
- **plugin xEdit automation script** (`patch-armor.pas`) — generated alongside `plugin-patches.json` whenever plugins are detected; a runnable Pascal (Delphi) script for SSEEdit/TES5Edit that rewrites matching ARMA world + first-person mesh paths to the generated SlideSmith mesh targets (with rewrite logging)
- **pose simulation** (`pose-simulation-report.json`) — `BasicPoseSimulationService` tests the converted mesh against 8 animation poses (T-pose, Walk, Run, Idle, Crouch, Combat-Idle, Jump, Sneak) using per-pose per-region stress amplifiers; regions where `morph_factor × pose_amplifier ≥ 1.10` are flagged as at-risk; report written as JSON and visualised in the preview HTML; emits `pose-simulation:tested=8,...` pipeline step
- **deeper mesh/physics solver tuning** — strategy conversion now runs a region-adjacency smoothing solver with mesh-type-specific clamp/blend iterations, and physics XML generation now applies adaptive stiffness/offset/damping/restitution tuning (including reduced offsets when physics weights are missing) for more stable outputs
- **NIF block graph parsing for geometry nodes** — conversion now parses `Ni*` block/type spans first (e.g. `NiTriShapeData`) to locate real vertex streams before fallback heuristics, improving transform reliability on non-synthetic NIF layouts
- **support asset carry-forward** — export now copies scanned textures, material files (`.bgsm`/`.bgem`), physics files, plugin files, and body-reference files into the output tree using source-relative paths so converted packs include required sidecar assets

- **animation-driven geometry solver** — `AnimationDrivenGeometrySolver` applies linear-blend skinning (LBS) across 8 canonical poses using anatomically-derived per-region bone rotations (sagittal Z-Y plane), computing per-region body-envelope penetration depth; `AnimationDrivenPoseSimulationService` reads source mesh vertices from NIF files, runs the solver, and feeds push-out corrections back into the vertex transform pass; heuristic fallback used when no mesh data is available — all vertex transformations are now pose-informed rather than purely morph-threshold based

- **true binary plugin record rewriting** — `BinaryPluginRewriteService` directly parses the Bethesda ESP/ESM/ESL binary format (handles both Skyrim LE 20-byte and SSE 24-byte record headers), walks the GRUP/record structure, locates every ARMA (ArmorAddon) record, and rewrites `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh-path subrecords in-place; produces a `<name>_patched.esp` file in the output directory that the user can drop straight into their Skyrim `Data` folder without running xEdit; the `patch-armor.pas` xEdit script and `plugin-patches.json` are still generated as supplementary reference

- **structured binary ARMA record analysis** — `BinaryArmaParser` now walks the full binary ARMA record to extract `FormID` (uint32 from record header), `EditorId` (EDID subrecord null-terminated string), `BipedSlots` (decoded from BOD2/BODT 32-bit slot flags: each set bit maps to slot 30+i), and all four mesh paths (MOD2/MOD3/MOD4/MOD5); `BasicPluginAnalysisService` upgraded from regex path-scan to `BinaryArmaParser`; each `PluginArmorAddon` now carries `FormId`, `EditorId`, and `BipedSlots` alongside the detected mesh paths

- **minimal override patch ESP** — in addition to the full-copy `_patched.esp`, export now generates `<name>_SlidesmithPatch.esp`: a proper Bethesda override plugin that lists the original ESP as its sole master file (`MAST`+`DATA` subrecords in TES4) and contains **only** the ARMA records that had paths rewritten; uses the same FormIDs as the originals so the engine treats them as overrides; can be dropped into the Data folder after the original without replacing any unrelated records; only generated when at least one ARMA record path matched the rewrite map

- **generated README.txt** — `ConversionReadmeGenerator` now writes a `README.txt` inside every output package describing: what was converted, all files generated with their purpose, step-by-step manual installation instructions, plugin patch usage (patch ESP or xEdit script fallback), BodySlide build instructions, and notes on manual finishing steps required; satisfies the Stage 7 README requirement from the issue spec

- **weight-variant synthesis** — when only one half of a `_0`/`_1` pair is present (e.g. only `armor_0.nif` without `armor_1.nif`, or vice versa), the missing variant is now **auto-generated** rather than skipped; regional morph factors are weight-scaled (×1.5 delta for the high-weight `_1`, ×0.5 delta for the low-weight `_0`) so the game engine can interpolate body weight without mesh collapse, visible clipping, or NPC weight-breaking; emits `weight-variants:synthesized=N` in `conversion.log`

- **flat normal map stub generation** — when a diffuse texture (e.g. `iron_d.dds`) has no matching `_n.dds` companion, a minimal **4×4 flat tangent-space normal map stub** is now auto-generated alongside it; the stub uses an uncompressed BGRA8 DDS (magic + 124-byte DDS_HEADER, all 16 pixels set to the neutral normal vector RGB(128,128,255) pointing straight outward) so surfaces render correctly in-game with no purple-tint artefacts; can be replaced by a baked normal map at any time; emits `normal-stubs:generated=N` in `conversion.log`

- **non-stub BodySlide morph payloads** — generated `.bsd` and `.tri` files now include populated vertex counts and deterministic per-vertex delta payloads for each slider/weight variant instead of header-only stub files, so exports are immediately consumable by BodySlide tooling

Issue #2 baseline coverage has been expanded substantially (import/dependency scan, body detection, mesh strategy, plugin rewriting, patch generation, output packaging, and morph payload export), with additional quality passes still being iterated.

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
