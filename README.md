# SlideSmith (Standalone, C#)

This repository contains the SlideSmith .NET conversion toolset (current version `1.0`) with both a Windows desktop GUI and a CLI app, bundling core conversion stages into one pipeline:

- import scan (single `.nif`, armor folder, or archive input: `.zip` / `.7z` / `.tar` / `.tar.gz` / `.tgz`)
- body signature detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE + CUSTOM fallback); bone-name scoring from physics XML
- custom body profile loading via `*.slidesmith-body.json` files placed beside the input assets, enabling named custom bodies with their own detection tokens, morph field, sliders, gender, and physics settings
- mesh type analysis (cloth/leather/plate/skin-tight/physics-enabled/mixed) with headgear sub-type classification (full-helmet/hood/face-mask/circlet)
- deformation cage generation
- mesh conversion strategy selection
- weight transfer + skeleton bone mapping (source → target, unsupported bone detection)
- morph generation with **11 regional fields** (chest, waist, pelvis, legs, shoulders, breasts, butt, belly, arms, thighs, calves) tuned per body type
- partition rebuilding (BSDismemberSkinInstance slot assignment): body/hands/feet for standard armor; full-helmet → slots 30+31 (Head+Hair); hood → slot 31 (Hair); face-mask → slot 30 (Head); circlet/crown/hat → slot 42 (Circlet)
- clipping detection + auto-correction pass (including explicit armpit risk surfacing in pose simulation output)
- physics profile generation (CBPC + SMP XML config file output)
- physics profile selection override (`auto`, `none`, `cbpc`, `smp`, `smp+cbpc`) with built-in per-target defaults for direct body conversions
- **vanilla armor database** — 65+ canonical Skyrim / DLC armors matched by mesh token for automatic profile recommendations
- **voxel collision detection** — 8×8×8 grid penetration scan after auto-correction; per-region push-out offsets logged per mesh type
- **deformation profile modifier** — fine-tunes regional morphs using 8 named profiles (balanced, curvy, slim, petite, athletic, muscular, lean, anime)
- **BodySlide `.osp` project generation** — outputs a valid BodySlide slider-set XML alongside each converted armor when slider export is enabled
- **texture analysis** — detects DDS textures, classifies diffuse / normal / specular / glow / parallax / subsurface, identifies missing normal maps
- **plugin scanning + rewrite mapping** — scans `.esp`/`.esm`/`.esl` sidecar files for ARMA mesh paths, generates rewrite mappings, and outputs an auto-rewrite xEdit script covering world + first-person model paths
- export package + manifest/log output
- conversion learning cache output (`.conversion-learning-cache.json`) for repeated runs
- live preview HTML output (`preview.html`) with region heatmap and conversion context
- dropped-item/world-object physics guidance export (`world-physics.json`) describing static vs rigid-proxy behavior for generated meshes
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
# optional preset-batch / target-batch comma-separated lists for one-run multi-body conversions,
# optional profile/source/physics override, optional BodySlide export toggle, optional output zip,
# cancel in-progress conversion, open output folder,
# embedded in-app preview pane for generated preview.html, "Load result..." button to browse and reload
# any previous output folder's preview, and quick-open batch reports when available

# build CLI executable package (single-file, self-contained)
dotnet publish src/Bodyslide.Standalone/Bodyslide.Standalone.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

# simple positional mode
dotnet run --project src/Bodyslide.Standalone -- "<armor path>" "<target body>" "<optional output directory>"

# named mode with preset support
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path|folder|zip>" --preset "3BA Curvy" --output "<optional output directory>"

# convert one armor to multiple target bodies in one run
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --targets "CBBE,3BA,HIMBO" --output "<optional output directory>"

# convert one armor to every built-in body type in one run
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "all" --output "<optional output directory>"

# convert one armor to multiple built-in presets in one run
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --presets "3BA Curvy,HIMBO Lean,UNP Petite" --output "<optional output directory>"

# apply a deformation profile (overrides the preset's built-in profile)
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --profile curvy

# override the auto-selected physics profile
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --physics none

# skip BodySlide slider/project export for a lighter output package
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "3BA" --build-sliders false

# override the auto-detected source body type
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --source "UNP" --target "3BA"

# produce a mod-manager-ready ZIP instead of a bare output folder
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --output-zip

# override the global learning-cache location (default: %APPDATA%\SlideSmith\ on Windows)
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "3BA" --cache-path "D:\MySlidesmithCache\.conversion-learning-cache.json"

# show built-in presets
dotnet run --project src/Bodyslide.Standalone -- --list-presets

# show available deformation profiles
dotnet run --project src/Bodyslide.Standalone -- --list-profiles

# show supported body types with detection tokens and vertex-count hints
dotnet run --project src/Bodyslide.Standalone -- --list-bodies

# show available physics profiles
dotnet run --project src/Bodyslide.Standalone -- --list-physics

# inspect the learning cache — prints all cached entries with target body, mesh type, strategy, and regional morphs
dotnet run --project src/Bodyslide.Standalone -- --export-cache

# inspect the learning cache at a custom location
dotnet run --project src/Bodyslide.Standalone -- --export-cache --cache-path "D:\MySlidesmithCache\.conversion-learning-cache.json"

# target a custom body profile discovered from a nearby *.slidesmith-body.json file
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "MyFollowerBody"
```

## Downloading pre-built executables

**The easiest way to get SlideSmith is from the [GitHub Releases page](../../releases):**

| File | Platform | What it is |
|---|---|---|
| `SlideSmith.exe` | Windows | Desktop GUI — double-click to open, drag-and-drop armor |
| `SlideSmith-CLI.exe` | Windows | Command-line tool — run from a terminal with `--help` |
| `slidesmith-linux-x64.zip` | Linux | Single CLI binary |

Every push to `main` automatically updates the **"SlideSmith — latest build"** pre-release entry on the Releases page. Versioned releases are published by pushing a `v*` tag.

## GitHub Actions (CI)

This repository includes `.github/workflows/build.yml`, which runs automatically on pushes to `main`/`master` and on pull requests.

What it does:
- **Every push/PR:** restore, build, test, publish a single-file Linux CLI binary, and upload it as a temporary Actions artifact.
- **Push to `main`/`master` (post-merge):** publish clean single-file Windows executables (Desktop GUI + CLI), create or update the rolling **"SlideSmith — latest build"** GitHub Release entry, and attach `SlideSmith.exe` and `SlideSmith-CLI.exe` directly.
- **Pull requests:** publish Windows desktop app, zip just the `.exe`, upload as a temporary PR artifact.

All published executables are self-contained single files — no installer, no extra DLLs, no debug symbols.

If the app seems to "do nothing", run it from a terminal with `--help` first. The CLI expects arguments (`--input`, `--target`/`--preset`, optional `--output`) and prints usage when required arguments are missing.

## Deformation profiles

Pass `--profile <name>` to scale regional morphs toward or away from the neutral body shape. The amplifier scales `(value − 1)` so a value of 1.0 (no change) is always preserved.

| Profile | Amplifier | Effect |
|---|---|---|
| balanced | 1.00 | Pure pass-through — delta unchanged (default for Vanilla presets) |
| curvy | 1.15 | Amplifies curves beyond the base shape |
| athletic | 1.08 | Subtle amplification with a toned look |
| lean | 0.88 | Reduces bulk while keeping proportions |
| slim | 0.82 | Visibly slimmer than the neutral body |
| petite | 0.75 | Smallest overall body dimensions |
| muscular | 1.25 | Strongest amplification of all dimensions |
| anime | 1.45 | Heavily amplified stylised anime proportions |

## Custom body profiles

Place a `*.slidesmith-body.json` file anywhere beside the input mesh/folder/archive contents to register a named custom body for that conversion run. Supported fields include:

- `name`
- `detectionTokens`
- `textureTokens`
- `physicsTokens`
- `vertexCountMin` / `vertexCountMax`
- `transformationField` (`chest`, `waist`, `pelvis`, `legs`, `shoulders`, `breasts`, `butt`, `belly`, `arms`, `thighs`, `calves`)
- `sliderNames`
- `physicsBones`
- `physicsProfile` (`none`, `cbpc`, `smp`, `smp+cbpc`)
- `gender` (`female` or `male`)
- `bodyOutputPath`

## Output files

Each successful conversion produces a **Data-relative package** in the output directory. The folder structure maps directly to Skyrim's `Data\` folder so mod managers and manual installs both work without any re-pathing:

```
output/
  meshes/
    slidesmith/<body>/
      <ArmorName>_0.nif          ← converted mesh (low-weight)
      <ArmorName>_1.nif          ← converted mesh (high-weight)
      <ArmorName>_ground.nif     ← ground/loot mesh
      <ArmorName>_1stperson.nif  ← first-person fallback (scratch-plugin)
  CalienteTools/
    BodySlide/
      SliderSets/
        <ArmorName>.osp          ← BodySlide slider-set project (when slider export is enabled)
      ShapeData/<ArmorName>/
        <ArmorName>.nif          ← BodySlide source-shape reference mesh (when enabled)
        <Slider>.bsd             ← low-weight slider morph (one per slider, when enabled)
        <Slider>_1.bsd           ← high-weight slider morph (one per slider, when enabled)
        <ArmorName>.tri          ← low-weight TRI morph for RaceMenu (when enabled)
        <ArmorName>_1.tri        ← high-weight TRI morph (when enabled)
  textures/...                   ← source textures (preserved relative paths)
  <PluginName>_patched.esp       ← full-copy patched plugin (if source ESP found)
  <PluginName>_SlidesmithPatch.esp ← minimal override patch ESP (ARMA-only)
  fomod/
    info.xml
    ModuleConfig.xml             ← FOMOD with <files> entries for meshes/ + CalienteTools/
  cbpc-config.xml                ← CBPC physics XML (when selected physics profile includes CBPC)
  smp-config.xml                 ← SMP physics XML (when selected physics profile includes SMP)
  conversion-manifest.json       ← full pipeline log
  README.txt                     ← user-facing installation guide
  preview.html                   ← interactive body heatmap + morph preview
  ...                            ← additional metadata/diagnostic files (see table below)
```

| File | Description |
|---|---|
| `<ArmorName>.nif` (root) | Original-filename copy of the converted mesh (workspace artifact) |
| `<ArmorName>_0.nif` + `<ArmorName>_1.nif` (root) | Low/high-weight variant pair at output root; **missing half is auto-synthesised** when only one is present |
| `meshes/slidesmith/<body>/<ArmorName>.nif` | Data-relative staged mesh; pointed to by the generated plugin |
| `meshes/slidesmith/<body>/<stem>_ground.nif` | Ground/loot mesh companion for every converted NIF variant |
| `CalienteTools/BodySlide/SliderSets/<ArmorName>.osp` | BodySlide slider-set project (open in BodySlide Studio) — written only when slider export is enabled |
| `CalienteTools/BodySlide/ShapeData/<ArmorName>/<ArmorName>.nif` | BodySlide source-shape reference mesh; required for the slider editor to display the base mesh — written only when slider export is enabled |
| `CalienteTools/BodySlide/ShapeData/<ArmorName>/<Slider>.bsd` + `<Slider>_1.bsd` | Per-slider vertex-displacement morphs for BodySlide (low + high weight) — written only when slider export is enabled |
| `CalienteTools/BodySlide/ShapeData/<ArmorName>/<ArmorName>.tri` + `<ArmorName>_1.tri` | TRI morph files for in-game RaceMenu morph interpolation — written only when slider export is enabled |
| `fomod/ModuleConfig.xml` | FOMOD installer with populated `<files>` entries mapping `meshes/` and `CalienteTools/` to Data sub-folders; mod managers (MO2, Vortex) read this to install all files correctly |
| `fomod/info.xml` | FOMOD package metadata (name, version, author) |
| `cbpc-config.xml` | CBPC physics config (breast/butt/belly for female; pec/belly for male) |
| `smp-config.xml` | SMP physics config (NPC Breast01, NPC Belly, NPC Butt nodes, etc.) |
| `conversion-manifest.json` | Full conversion log with all pipeline steps |
| `dependency-map.json` | Per-mesh dependency map linking related textures, physics, body refs, plugin mesh references, **detected source body**, **ARMA FormIDs**, and **source skeleton** |
| `skeleton-compatibility.json` | Full bone-mapping report: source skeleton name, target skeleton name, every mapped bone pair, and the list of unsupported bones that have no target equivalent |
| `conversion-quality.json` | Machine-readable quality metrics: body-detection confidence + evidence, strategy used, per-region morphing, clipping regions, correction method, voxel penetration count, skeleton names, mapped + unsupported bone counts, and ISO-8601 generation timestamp |
| `world-physics.json` | Dropped-item/world-object physics guidance: selected world mode (`static` or `rigid-proxy`), collision-shape recommendation, whether source/equipped physics were detected, ground-mesh availability, and practical install/runtime recommendations |
| `plugin-patches.json` | Detected sidecar plugin mesh paths + structured rewrite mappings (`OriginalMeshPath` → `RewrittenMeshPath`) and per-mesh patch steps |
| `patch-armor.pas` | xEdit Pascal automation script (SSEEdit / TES5Edit): runs ARMA mesh-path rewriting directly inside the tool |
| `<PluginName>_patched.<ext>` | **Full-copy patched plugin** — a direct copy of the source `.esp`/`.esm`/`.esl` with every ARMA `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh-path subrecord updated in-place; supports both Skyrim LE and SE record header formats |
| `<PluginName>_SlidesmithPatch.esp` | **Minimal override patch ESP** — contains ONLY the patched ARMA records and lists the original plugin as its master; safe to load after the original |
| `README.txt` | Human-readable installation guide with FOMOD and manual install instructions |
| `preview.html` | Browser-openable live preview report with regional morph heatmap, pose-clipping summary, and interactive controls |
| `batch-report.json` | Root batch summary when input is a folder or archive (total/success/fail counts and per-armor results) |
| `.conversion-learning-cache.json` | Learning cache for faster repeated conversions |

When a matching cache entry exists for the same armor mesh + target body, the converter reuses prior regional morphing data and marks `learning-cache:hit` / `learning-cache:reused` in pipeline steps. The cache is written to both the local output folder (`.conversion-learning-cache.json`) **and** a shared global location (`%APPDATA%\SlideSmith\` on Windows, `~/.config/slidesmith/` on Linux/macOS) so the tool learns from all prior conversions across different armor packs. Use `--cache-path` to specify a custom global cache location.

When `--targets` / `--presets` (or the desktop batch-entry boxes) are used, each requested body/preset is exported into its own subfolder under the selected output root so multiple conversions never overwrite each other.

## Issue #2 progress comparison

Implemented from issue scope:
- import scan across single mesh, folder, and archive input (`.zip`, `.7z`, `.tar`, `.tar.gz`, `.tgz`)
- batch mesh discovery now skips support/body-reference NIFs (e.g., skeleton and body base/reference files) so only convertible armor/clothing meshes are processed
- body detection (CBBE, UNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE, CUSTOM fallback); bone-name scoring from physics XML for higher confidence
- body detection reference comparison now scores body-reference asset names (`*.tri`, `*.osp`, reference mesh names) against known body templates as additional evidence
- body detection UV-signature evidence now samples mesh UV coverage/aspect ranges from readable NIF geometry and factors it into confidence scoring (`uv:u=... ,v=...`)
- mesh analysis, cage/strategy stages, weight transfer, morph generation, partition rebuild, clipping detect/correct, physics configs
- plugin scan, texture summary (6 DDS categories), vanilla armor lookup (105+ entries across base game + Dawnguard/Dragonborn DLC), voxel collision pass, BodySlide OSP output, BSD/TRI slider data, learning cache reuse
- automatic CI builds with Linux/Windows executable zip artifacts uploaded in Actions for PR and merge testing (no release publishing)
- FOMOD metadata output (`fomod/ModuleConfig.xml`, `fomod/info.xml`)
- **output `.nif` file(s)** written to the output directory; `_0`/`_1` weight variant pairs detected and written as matched pairs
- **`--source` flag** to override auto-detected source body type (`--source CBBE`, etc.)
- **`--list-bodies` flag** to enumerate all supported body types with detection tokens
- **`--export-cache` flag** — prints all learning-cache entries (target body, mesh type, strategy, per-region morphs, last conversion timestamp) to standard output for inspection/debugging; accepts an optional `--cache-path` to read from a custom location
- **source→target relative delta conversion** — `StrategyMeshConversionService` now computes `targetField[region] / sourceField[region]` per region so converting e.g. CBBE→UNP applies only the directional difference rather than the full UNP field; emits `conversion-delta:CBBE→UNP` step
- **vanilla recommended profile auto-apply** — when the vanilla armor database identifies a match and no explicit `--profile` was provided, its `RecommendedProfile` is automatically applied (emits `vanilla-profile:<name>` step)
- **armor region binding by bone names** — new `IArmorRegionBindingService` / `BasicArmorRegionBindingService` detects which body regions (chest, waist, pelvis, legs, shoulders, arms, breasts, belly, butt) the armor covers by scoring physics-file bone name tokens, falling back to mesh filename keywords, then full-body default; emits `regions:<list>,method=<detection-method>` step
- **geometry signature scan** — lightweight NIF vertex-count/bounds sampling now feeds body detection evidence (`verts:<count>`) and adds a `spatial-geometry` fallback for armor region binding when readable mesh coordinates are available
- **batch summary report** — converting a directory or archive input (`.zip`, `.7z`, `.tar`, `.tar.gz`, `.tgz`) now writes `batch-report.json` to the root output folder with total/success/fail counts, target body, timestamp, and per-armor result entries
- **live preview HTML** (`preview.html`) — self-contained browser-openable file with an inline SVG body silhouette where each region is colour-coded by morph factor (blue→green→yellow→orange→red scale), plus regional morphing table, BodySlide slider list, physics-node list, pose-clipping risk summary, and interactive controls to swap body profile, rotate view, and adjust sliders; also includes **Auto-Correction Pass**, **Texture Analysis**, and **World/Dropped-Item Physics** panels; `armpits` now has a dedicated SVG shape and is highlighted as a high-risk region in the subtitle badge when flagged; replaces the old metadata-only `preview-renders.json`
- **plugin xEdit automation script** (`patch-armor.pas`) — generated alongside `plugin-patches.json` whenever plugins are detected; a runnable Pascal (Delphi) script for SSEEdit/TES5Edit that rewrites matching ARMA world + first-person mesh paths to the generated SlideSmith mesh targets (with rewrite logging)
- **pose simulation** (`pose-simulation-report.json`) — `BasicPoseSimulationService` tests the converted mesh against 8 animation poses (T-pose, Walk, Run, Idle, Crouch, Combat-Idle, Jump, Sneak) using per-pose per-region stress amplifiers; regions where `morph_factor × pose_amplifier ≥ 1.10` are flagged as at-risk; report written as JSON and visualised in the preview HTML; emits `pose-simulation:tested=8,...` pipeline step
- **deeper mesh/physics solver tuning** — strategy conversion now runs a region-adjacency smoothing solver with mesh-type-specific clamp/blend iterations, and physics XML generation now applies adaptive stiffness/offset/damping/restitution tuning (including reduced offsets when physics weights are missing) for more stable outputs
- **NIF block graph parsing for geometry nodes** — conversion now parses `Ni*` block/type spans first (e.g. `NiTriShapeData`) to locate real vertex streams before fallback heuristics, improving transform reliability on non-synthetic NIF layouts
- **support asset carry-forward** — export now copies scanned textures, material files (`.bgsm`/`.bgem`), physics files, plugin files, and body-reference files (`.tri`/`.osp` plus skeleton `.nif`) into the output tree using source-relative paths so converted packs include required sidecar assets
- **external custom body profiles** — import now auto-loads nearby `*.slidesmith-body.json` files so conversions can target named custom bodies with custom detection tokens, transformation fields, slider sets, male/female BodySlide metadata, and per-body physics defaults instead of falling back to a generic `CUSTOM` output
- **profile system controls** — direct target-body runs now resolve sensible built-in default physics per body (`CBBE/UBE/Vanilla` → `none`, `UNP/TBD` → `cbpc`, `3BA/BHUNP` → `smp+cbpc`, `HIMBO/SAM/SOS` → `smp`), while CLI/desktop users can explicitly override physics and disable BodySlide slider export for lighter packages

- **animation-driven geometry solver** — `AnimationDrivenGeometrySolver` applies linear-blend skinning (LBS) across 8 canonical poses using anatomically-derived per-region bone rotations (sagittal Z-Y plane), computing per-region body-envelope penetration depth; `AnimationDrivenPoseSimulationService` reads source mesh vertices from NIF files, runs the solver, and feeds push-out corrections back into the vertex transform pass; heuristic fallback used when no mesh data is available — all vertex transformations are now pose-informed rather than purely morph-threshold based

- **true binary plugin record rewriting** — `BinaryPluginRewriteService` directly parses the Bethesda ESP/ESM/ESL binary format (handles both Skyrim LE 20-byte and SSE 24-byte record headers), walks the GRUP/record structure, locates every ARMA (ArmorAddon) record, and rewrites `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh-path subrecords in-place; produces a `<name>_patched.esp` file in the output directory that the user can drop straight into their Skyrim `Data` folder without running xEdit; the `patch-armor.pas` xEdit script and `plugin-patches.json` are still generated as supplementary reference

- **structured binary ARMA record analysis** — `BinaryArmaParser` now walks the full binary ARMA record to extract `FormID` (uint32 from record header), `EditorId` (EDID subrecord null-terminated string), `BipedSlots` (decoded from BOD2/BODT 32-bit slot flags: each set bit maps to slot 30+i), all four mesh paths (MOD2/MOD3/MOD4/MOD5), and **race FormID** (`RNAM` subrecord); `BinaryArmaParser` also parses ARMO records for **keyword FormIDs** (`KWDA` — array of 4-byte FormIDs, one per keyword for slot/behaviour filtering) and **race FormID** (`RNAM`); each `PluginArmorAddon` now carries `RaceFormId` and each `PluginArmorRecord` carries `KeywordFormIds` and `RaceFormId`

- **SMP source bone extraction** — `BasicWeightTransferService` now reads SMP physics XML files bundled with the source armor (from `ImportedArmor.PhysicsFiles`) and parses `<bone name="...">` elements to extract distinct bone names; these are surfaced as `WeightedMesh.SourceSmpBones` (alphabetically sorted) and logged as `smp-bones:...` in `conversion.log`, enabling accurate SMP weight transfer between source and target body physics configs

- **"balanced" deformation profile** — the `balanced` profile (amplifier 1.00, neutral pass-through) is now a registered entry in `DeformationProfileModifier.ProfileAmplifiers`; previously the "Vanilla Balanced" preset silently no-oped because "balanced" was absent from the amplifier table

- **"anime" deformation profile + presets** — a new `anime` profile (amplifier 1.45 — strongly amplified proportions for stylised anime aesthetics) is registered, along with four new presets: `CBBE Anime`, `3BA Anime`, `BHUNP Anime`, and `UNP Anime`

- **Vanilla conversion presets** — four new presets enable direct Vanilla→mod-body conversion: `Vanilla to CBBE`, `Vanilla to 3BA`, `Vanilla to HIMBO`, `Vanilla to UNP` (all balanced deformation; physics profile matches the target body)

- **minimal override patch ESP** — in addition to the full-copy `_patched.esp`, export now generates `<name>_SlidesmithPatch.esp`: a proper Bethesda override plugin that lists the original ESP as its sole master file (`MAST`+`DATA` subrecords in TES4) and contains **only** the ARMA records that had paths rewritten; uses the same FormIDs as the originals so the engine treats them as overrides; can be dropped into the Data folder after the original without replacing any unrelated records; only generated when at least one ARMA record path matched the rewrite map

- **generated README.txt** — `ConversionReadmeGenerator` now writes a `README.txt` inside every output package describing: what was converted, all files generated with their purpose, step-by-step manual installation instructions, plugin patch usage (patch ESP or xEdit script fallback), BodySlide build instructions, and notes on manual finishing steps required; satisfies the Stage 7 README requirement from the issue spec

- **weight-variant synthesis** — when only one half of a `_0`/`_1` pair is present (e.g. only `armor_0.nif` without `armor_1.nif`, or vice versa), the missing variant is now **auto-generated** rather than skipped; regional morph factors are weight-scaled (×1.5 delta for the high-weight `_1`, ×0.5 delta for the low-weight `_0`) so the game engine can interpolate body weight without mesh collapse, visible clipping, or NPC weight-breaking; emits `weight-variants:synthesized=N` in `conversion.log`

- **flat/aux texture stub generation** — when a diffuse texture (e.g. `iron_d.dds`) has no matching `_n.dds` companion, a flat tangent-space normal map stub is auto-generated at the **same pixel dimensions** as the source diffuse (falling back to 4×4 if dimensions can't be read); missing auxiliary maps (`_s` specular, `_p` parallax/height, `_g` glow, `_r` roughness) are also generated at matching dimensions; when a companion specular map exists, roughness is **derived** from it via BT.601 luminance inversion (bright specular → low roughness) rather than always using a neutral grey stub; when a normal map exists, the parallax/height map is **derived** from it via per-scanline X-gradient integration (Frankot–Chellappa approximation) rather than always using a flat-black stub; all stubs use uncompressed BGRA8 DDS payloads and can be replaced by authored maps at any time; emits `normal-stubs:generated=N` and `aux-stubs:...` entries in `conversion.log`

- **skeleton NIF parsing** — `BasicSkeletonMappingService` now reads any `skeleton*.nif` files from the armor's body reference list and parses their string table to extract actual bone names; the source skeleton label (`xpmsse-vanilla`, `xpmsse-physics`, or `fo4-biped`) is inferred from physics-marker bones (`NPC *Breast*`, `*Butt*`, `*Belly*`, `*Pec*`, `*Lat*`) and Bip01 prefixes, giving accurate bone-mapping reports in `skeleton-compatibility.json` without relying solely on hardcoded lists

- **non-stub BodySlide morph payloads** — generated `.bsd` and `.tri` files now include populated vertex counts and deterministic per-vertex delta payloads for each slider/weight variant instead of header-only stub files, so exports are immediately consumable by BodySlide tooling

- **ARMO ground-model synthesis** — when an ARMO record has rewritten `MOD2`/`MOD3` world model paths but no `MODL` ground mesh subrecord, plugin rewrite now auto-appends `MODL` using the rewritten world model path so dropped-item world meshes stay aligned with converted armor outputs

- **ARMO world-model completion from `MODL`** — when an ARMO record includes a rewritten `MODL` ground mesh but is missing `MOD2` and/or `MOD3`, plugin rewrite now auto-appends the missing world model subrecord(s) from that rewritten `MODL` path so inventory/world model lookups stay valid for both male and female model entries

- **ARMA first-person completion from rewritten third-person paths** — when an ARMA record is rewritten but missing `MOD4` and/or `MOD5`, plugin rewrite now auto-appends the missing first-person subrecord(s) from the rewritten `MOD2`/`MOD3` paths so first-person model lookups remain populated after conversion

- **morph-aware clipping region detection** — clipping detection now evaluates per-region morph intensity with mesh-type thresholds (instead of mesh-type-only flags), normalizes equivalent regions (e.g. hips→pelvis), and auto-flags armpit risk when shoulder/arm/chest pressure is high; this produces more realistic region highlights before auto-correction

- **shrinkwrap clearance projection pass** — NIF vertex transformation now enforces a per-region body-envelope minimum radius with adaptive clearance after animation-driven push-out, so near-surface vertices are projected outward instead of lingering on the clipping boundary

- **headgear sub-type classification and head/hair partition assignment** — the mesh analysis stage now classifies all headgear into one of four sub-types: `full-helmet` (full head-covering piece; keywords: helmet, greathelm, warhelm, sallet, barbute, bascinet), `hood` (cloth/leather hair-covering; keywords: hood, cowl, coif, veil, shroud), `face-mask` (partial face covering; keywords: mask, visor, blindfold, eyepatch, facecover), or `circlet` (small accessory worn over hair; keywords: circlet, crown, diadem, tiara, hat, cap); partition rebuilding then assigns the correct Skyrim `BSDismemberSkinInstance` skin-partition IDs per sub-type — full-helmet → slots 30 (Head) + 31 (Hair), hood → slot 31 (Hair), face-mask → slot 30 (Head), circlet → slot 42 (Circlet) — preventing invisible head parts, hair z-fighting, and circlet/helmet slot conflicts in-game

- **ground mesh NIF generation** — export now calls `IGroundMeshGeneratorService` to produce a `<stem>_ground.nif` alongside each converted armor mesh (stored under `meshes/slidesmith/<body>/`); when a real source NIF is available its bytes are proxy-copied so the dropped-item world representation exactly matches the worn mesh; when no source bytes are available a minimal valid Gamebryo 20.2.0.7 stub (zero-block) is synthesized so Skyrim can parse the record without crashing; the ground mesh relative path is also threaded into the scratch plugin generator so standalone exports carry a complete MODL entry

- **biped slot passthrough from plugin BOD2/BODT** — after partition rebuilding, `ConversionOrchestrator` now reads all decoded `BipedSlots` from the scanned plugin's ARMA records and merges any slots not already covered by the rebuilt partitions into the final partition list using `KnownPartitionSlotNames` labels; emits a `biped-slots-passthrough:<slot1>,<slot2>,...` pipeline step when the source plugin contains at least one slot that was absent from the rebuilt set, preventing mods from losing their original slot assignments

- **standalone (scratch) plugin generation** — when no source plugin was found among the input assets, export now calls `IScratchPluginGeneratorService` to produce a self-contained ESL-flagged `.esp` directly in the output directory; the plugin contains a complete TES4 record that declares `Skyrim.esm` as master (required for DefaultRace lookups), an ARMO record (FormID 0x801) with `OBND` (object bounds), `BOD2` (body-slot mask), world model `MOD2`/`MOD3` paths, `DNAM` (armor rating), and an `ARMA` subrecord linking to the armor addon, and an ARMA record (FormID 0x802) with `OBND`, `BOD2`, `RNAM` pointing to DefaultRace (0x000013), `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh paths; the resulting ESP is immediately loadable in Skyrim Special Edition without manual xEdit editing

- **first-person mesh paths in scratch plugin** — the scratch plugin generator now produces distinct MOD4 (female first-person) and MOD5 (male first-person) subrecords whose paths use a `_1stperson` stem suffix (e.g. `meshes/slidesmith/3ba/iron_1stperson_0.nif`) instead of repeating the third-person MOD2/MOD3 path; this matches the vanilla Skyrim ARMA record convention where first-person arms have a separate, lighter NIF that the game loads during first-person camera mode

- **rigid island detection** — `BasicRigidIslandDetectionService` analyses each converted mesh by armour type and subdivides it into rigid attachment zones that a physics solver must treat as non-deformable; plate armour is segmented into 10 anatomical islands (cuirass-front, cuirass-back, pauldron-L/R, gauntlet-L/R, greave-L/R, sabaton-L/R) via `plate-anatomy-clustering`; leather armour yields 5 accent islands (buckle-front, stud-L/R, tasset-L/R) via `material-zone-clustering`; mixed (plate+leather) armour yields 3 hybrid zones via `hybrid-zone-clustering`; cloth, skin-tight, and headgear meshes return zero islands; the `ConversionOrchestrator` calls the service after mesh conversion and records island count, plate coverage, and detection method in a `rigid-islands:` pipeline step

- **target physics bone injection** — `BasicWeightTransferService` now auto-populates `WeightedMesh.TargetPhysicsBones` from a per-body-type map so downstream physics and BodySlide steps know exactly which bones the target skeleton drives; 3BA and BHUNP targets receive 9 female SMP bones (NPC L/R Breast01–03, L/R Butt, Belly); UNP and TBD targets receive 5 CBPC bones (NPC L/R Breast01, L/R Butt, Belly); HIMBO, SAM, and SOS targets receive 3 male SMP bones (NPC L/R Pec, Belly); headgear meshes always suppress physics bone injection; unknown body types return a null `TargetPhysicsBones` list; the orchestrator logs a `physics-injection:` step immediately after weight transfer

- **scratch-plugin mesh staging** — when no source plugin exists, export now stages converted meshes into the exact `meshes/slidesmith/<body>/...` paths referenced by the generated standalone ESP and also writes fallback `_1stperson.nif` copies so the MOD2/MOD3/MOD4/MOD5 paths all resolve without any manual file moves or extra mesh authoring
- **scratch-plugin MODL fallback** — standalone scratch ESP generation now always writes an ARMO `MODL` world/inventory model path; when a dedicated `<stem>_ground.nif` is unavailable, `MODL` automatically falls back to the primary converted mesh path so dropped-item lookups never end up blank
- **single-armor multi-target batch conversion** — CLI now supports `--targets "<body1,body2,...>"` and `--presets "<preset1,preset2,...>"`, and the desktop app exposes matching batch-entry fields for preset/target mode; one armor (or one folder/archive batch) can now be converted into multiple body outputs in a single run, with each target/preset written into its own output subfolder to avoid collisions
- **all-body target alias** — `--target all` / `--target any` / `--target *` (and the same tokens inside `--targets`) now expand to every supported body type automatically, so one command can export a full multi-body conversion pack without manually listing each body name
- **topology + UV mismatch diagnostics** — export now compares source vs converted mesh signatures and writes `TopologyMismatchRisk`, `VertexCountDeltaRatio`, `UvCoverageDeltaRatio`, `UvAspectRatioDelta`, and `QualityWarnings` into `conversion-quality.json`; large drift thresholds flag likely topology/UV mismatch risks early (addressing a major “common failure point” from issue #2)
- **physics-bone fallback remapping for skeleton compatibility** — skeleton mapping now aligns UNP/TBD targets with their CBPC support set (`NPC L/R Breast01`, `NPC L/R Butt`, `NPC Belly`) and auto-remaps unsupported higher-order source physics bones (e.g. `NPC L/R Breast02/03`) to the best available target equivalent before marking them unsupported, reducing conversion drop-off when source and target skeleton physics depth differ

Issue #2 baseline coverage has been expanded substantially (import/dependency scan, body detection, mesh strategy, plugin rewriting, patch generation, output packaging, morph payload export, headgear sub-type/partition handling, ground mesh NIF output, biped slot passthrough, scratch plugin generation for plugin-free inputs, first-person mesh paths, rigid island detection, and target physics bone injection).

## Current built-in presets (36 total)

| Preset | Target Body | Deformation | Physics |
|---|---|---|---|
| 3BA Curvy | 3BA | curvy | smp+cbpc |
| 3BA Slim | 3BA | slim | smp+cbpc |
| 3BA Athletic | 3BA | athletic | smp+cbpc |
| 3BA Anime | 3BA | anime | smp+cbpc |
| BHUNP Curvy | BHUNP | curvy | smp+cbpc |
| BHUNP Slim | BHUNP | slim | smp+cbpc |
| BHUNP Athletic | BHUNP | athletic | smp+cbpc |
| BHUNP Anime | BHUNP | anime | smp+cbpc |
| CBBE Curvy | CBBE | curvy | none |
| CBBE Slim | CBBE | slim | none |
| CBBE Athletic | CBBE | athletic | none |
| CBBE Petite | CBBE | petite | none |
| CBBE Anime | CBBE | anime | none |
| UNP Petite | UNP | petite | cbpc |
| UNP Athletic | UNP | athletic | cbpc |
| UNP Curvy | UNP | curvy | cbpc |
| UNP Slim | UNP | slim | cbpc |
| UNP Anime | UNP | anime | cbpc |
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
| Vanilla Balanced | Vanilla | balanced | none |
| Vanilla to CBBE | CBBE | balanced | none |
| Vanilla to 3BA | 3BA | balanced | smp+cbpc |
| Vanilla to HIMBO | HIMBO | balanced | smp |
| Vanilla to UNP | UNP | balanced | cbpc |
| HIMBO Lean | HIMBO | lean | smp |
| HIMBO Muscular | HIMBO | muscular | smp |
| HIMBO Athletic | HIMBO | athletic | smp |

## Supported body types

**Female:** CBBE, 3BA, UNP, BHUNP, TBD, UBE  
**Male:** HIMBO, SAM, SOS, Vanilla  
**Custom:** any unrecognised body falls back to `CUSTOM` detection

Use `--list-bodies` to see detection tokens and vertex-count hints for each body type. Use `all`, `any`, or `*` as a target alias to convert to every listed body in one run.

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



If the input is a directory or archive (`.zip`, `.7z`, `.tar`, `.tar.gz`, `.tgz`), all `.nif` files are converted in one run and exported into per-armor output folders.
