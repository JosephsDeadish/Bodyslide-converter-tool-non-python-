# SlideSmith (Standalone, C#)

This repository contains the SlideSmith .NET conversion toolset (current version `1.0`) with both a Windows desktop GUI and a CLI app, bundling core conversion stages into one pipeline:

- import scan (single `.nif`, plugin (`.esp`/`.esm`/`.esl`), armor folder, or archive input: `.zip` / `.7z` / `.tar` / `.tar.gz` / `.tgz`)
- body signature detection (CBBE, UNP, UNPB, UUNP, COCO CBBE, COCO UUNP, HIMBO, BHUNP, 3BA, TBD, SAM, SAM Light, SOS, TNG, UBE, Vanilla Beast, Goat Humanoid, Hagraven, Spriggan + CUSTOM fallback); bone-name scoring from physics XML
- built-in target body aliases for common ecosystem names such as `3BBB` → `3BA`, `TNG Extended` → `TNG`, `Touched By Dibella` → `TBD`, `Shape Atlas for Men` → `SAM`, and `Beast Vanilla` → `Vanilla Beast`
- custom body profile loading via `*.slidesmith-body.json` files placed beside the input assets, enabling named custom bodies with their own detection tokens, morph field, sliders, gender, and physics settings
- mesh type analysis (cloth/leather/plate/skin-tight/physics-enabled/mixed) with headgear sub-type classification (full-helmet/hood/face-mask/circlet)
- deformation cage generation
- mesh conversion strategy selection
- weight transfer + skeleton bone mapping (source → target, unsupported bone detection)
- morph generation with **11 regional fields** (chest, waist, pelvis, legs, shoulders, breasts, butt, belly, arms, thighs, calves) tuned per body type
- partition rebuilding (BSDismemberSkinInstance slot assignment): body/hands/feet for standard armor; full-helmet → slots 30+31 (Head+Hair); hood → slot 31 (Hair); face-mask → slot 30 (Head); circlet/crown/hat → slot 42 (Circlet)
- clipping detection + auto-correction pass (including explicit armpit risk surfacing in pose simulation output)
- physics profile generation (CBPC + SMP XML config file output, including extra target-specific secondary/genital bones when the target ecosystem exposes them)
- physics profile selection override (`auto`, `none`, `cbpc`, `smp`, `smp+cbpc`) with built-in per-target defaults for direct body conversions (common aliases like `soft-body`, `full-soft-body`, `hdt-smp`, `fsmp`, and `cbp` are accepted and normalized automatically)
- **vanilla armor database** — 65+ canonical Skyrim / DLC armors matched by mesh token for automatic profile recommendations
- **voxel collision detection** — 8×8×8 grid penetration scan after auto-correction; per-region push-out offsets logged per mesh type
- **deformation profile modifier** — fine-tunes regional morphs using 8 named profiles (balanced, curvy, slim, petite, athletic, muscular, lean, anime)
- **BodySlide `.osp` project generation** — outputs a valid BodySlide slider-set XML alongside each converted armor when slider export is enabled
- incomplete-source BodySlide fallback recovery that can infer likely source-body slider families plus fallback deformation-profile hints from nearby reference/body asset names when OSP/TRI/BSD support files are missing
- source TRI/BSD/OSD payloads are recorded as candidates with asset-name provenance; parsed candidates remain ineligible for reuse until both target-shape identity and vertex-order correspondence are verified, and topology retargeting additionally requires an explicit verified map
- **texture analysis** — detects DDS textures, classifies diffuse / normal / specular / glow / parallax / subsurface, identifies missing normal maps
- **plugin scanning + rewrite mapping** — scans `.esp`/`.esm`/`.esl` sidecar files for ARMA mesh paths, generates rewrite mappings, and outputs an auto-rewrite xEdit script covering world + first-person model paths, including modular device-style armor packs that split body/head/world variants
- export package + manifest/log output
- conversion learning cache output (`.conversion-learning-cache.json`) for repeated runs
- real 3D preview/workbench output (`preview-workbench.html`) rendered from converted mesh vertices, plus diagnostics report (`preview.html`)
- dropped-item/world-object physics guidance export (`world-physics.json`) describing static vs rigid-proxy behavior for generated meshes
- optional ZIP output (`--output-zip`) for mod-manager-ready packages
- runtime readiness self-checks in both CLI and desktop GUI so users can verify the executable, pipeline init, cache path, scratch-write access, and preview/runtime availability before converting anything
- armor-pack validation reporting with per-conversion readiness summaries and batch-level pack risk rollups
- batch `regression-failure-matrix.json` groups quality-report issue codes and review requirements by source body, target body, mesh type, and support tier; sample IDs replace mesh names and output paths
- source BodySlide discovery records `DiscoveryMilliseconds` and `DiscoveredFileCount` in source-asset quality metrics, supports cancellation during traversal, skips directory-link cycles, and bounds retained project caches

## Projects

- `/src/Bodyslide.Core` - conversion pipeline + modules
- `/src/Bodyslide.Desktop` - Windows GUI app (drag/drop, preset or manual destination mode, explicit source-body override, output-zip toggle, convert/cancel)
  - supports startup result loading via `--load-result`, `--result`, `--output`, or an existing output-path argument (useful for MO2 launcher entries)
- `/src/Bodyslide.Standalone` - CLI app entry point
- `/tests/Bodyslide.Core.Tests` - focused orchestration and batch/preset tests

### Readiness and regression coverage

Explicit CLI commands and conversions take precedence over MO2/Vortex environment
variables, managed paths, and launcher switches. Launcher-only invocations still
open the desktop app; use `--load-result` or manager-specific result/path options
for desktop result loading rather than CLI conversion flags.

The standalone CLI accepts named options as `--name value`, `--name=value`, or
`--name:value`. Inline commands such as `--self-check=true` use the same command
names as startup routing; inline conversion options are not mistaken for launcher
metadata. This parser/routing coverage does not replace real Windows manager
handoff testing.
EXE and DLL desktop handoff preserve the caller's working directory when it
exists, keeping relative result/diagnostic paths anchored to the manager launch
context rather than silently resolving them beside the desktop binary.

The regression suite includes synthetic artifact-tampering, mixed launcher,
catalog-invariant, discovery-cancellation, and failure-matrix cases alongside the
existing realistic pack fixtures. No new real-user failure packs were supplied.
To reproduce a reported failure, reduce it to a redistributable fixture, remove
identifying paths and plugin/mesh names, record the source/target body, skeleton,
physics and archive layout, and assert the expected issue codes and output
artifacts in a repeatable test. The batch failure matrix helps prioritize those
cases but does not itself prove a conversion works in game.

`FailingPackReadinessTests` exercises existing local synthetic, redistributable
fixtures for unsupported NIF layouts (including NiLines), unresolved cross-plugin
ties, and oral topology review. It checks issue codes, review gates, nonempty
guidance artifacts, and batch `regression-failure-matrix.json` against the pack
validation rollup. `Fixtures/RealUserFailingPackIntake` contains only intake
instructions, not an actual real-user reproduction. This coverage is not an
“any armor” guarantee; real load orders and in-game deformation still need proof.

`advanced-review-required` and `experimental-manual-cleanup` remain intentional
safety gates. Unverified custom rigs, topology changes and external game/UI
checks must not be relabeled automatic merely to improve readiness counts.

### Compatibility boundaries and evidence

The body names below are **built-in conversion profiles**, not a claim that every
release of each body mod, outfit, skeleton, or physics stack has passed an
end-to-end test. Automated tests use synthetic/redistributable fixtures and verify
specific parsers, reports, and safety gates; they do not establish that a package
works in every Skyrim installation.

| Area | Implemented / automated evidence | Not established by those tests |
|---|---|---|
| Inputs | Single NIF, plugin sidecars, folders, and ZIP/7z/TAR-family archives are handled by the import/conversion paths. Folder and ZIP batch flows have end-to-end fixture coverage. | Every archive variant/layout, installed mod, or MO2 virtualized file view. |
| NIF meshes | Geometry-family readers, topology/partition checks, weight variants, explicit unsupported-layout diagnostics, and reporting of textual/binary header versions plus user-version fields have synthetic regression coverage. Per-mesh reports label this evidence `automated-file-inspection-only` and external compatibility `untested`. Unknown or unsupported layouts are review cases, not safe conversions. | An exhaustive NIF-version/block-type matrix or every real-world mesh exporter. A parser `supported` status or recognized header does not establish installed-tool or in-game compatibility; no Skyrim edition is certified by the synthetic fixtures alone. |
| Body profiles | Embedded profiles provide aliases, detection/reference tokens, slider names, transformations, skeleton framework labels, physics defaults, and expected support regions. Catalog integrity is checked in tests/readiness. | Exact values for every installed body-mod release, slider range, reference mesh, morph payload, or real outfit fit. Profile presence is not a real-asset compatibility test. |
| BodySlide | OSP/ShapeData generation, source-project discovery, TRI/BSD parsing and provenance labeling, and report-level output checks have regression coverage. | Parsed source morph payloads are not reused based on vertex count alone; successful BodySlide/Outfit Studio builds and in-game slider/morph behavior remain unverified. |
| Plugins and packaging | Synthetic plugin parsing/rewrite, ambiguity gates, Data-relative output, ZIP packaging, and MO2 metadata have regression coverage. | Correctness against every load order, installer choice, plugin combination, or MO2/Vortex installation. |
| Skeleton and physics | Catalog bone resolution, mapping diagnostics, generated CBPC/SMP configuration, and readiness reports are checked by tests. | That a particular game installation has the required skeleton/runtime, or that the generated weights/chains simulate correctly in live gameplay. |
| Desktop / MO2 / game | Startup routing and launcher argument behavior have automated tests; external proof harnesses describe Windows/UI/runtime/game checks. | A passing Windows desktop flow through MO2, live-game animation/collision testing, or proof for a specific load order until external evidence is imported. |

The current compatibility matrix has **no externally verified game/body/runtime
combination**. Build and regression coverage is not an installed-game compatibility
claim:

| Matrix dimension | Automated scope currently recorded | External verification status |
|---|---|---|
| Skyrim edition/runtime | No game executable or runtime version is part of the automated conversion fixtures. | Skyrim SE, AE, and VR edition/version combinations remain unverified. |
| NIF version/layout | Synthetic reader and failure fixtures cover selected supported structures and explicit unsupported cases such as `NiLines`. | No exhaustive NIF version/block-type/exporter matrix has passed an external tool or game check. |
| Outfit construction | Fixture coverage exercises selected clothing/armor meshes, partitions, linked plugin references, and `_0`/`_1` pairs. | No garment category, modular set, or real outfit/body pair is generally certified. |
| Body and skeleton | Built-in profiles and framework catalogs are checked for internal metadata consistency. | Catalog profiles, including CBBE, 3BA, BHUNP, HIMBO, SOS, and special frameworks, are not versioned proofs of installed reference meshes or skeletons. |
| Physics | Generated CBPC/SMP configuration and declared bone mappings have synthetic checks. | No CBPC/SMP runtime version, target skeleton installation, or live simulation/collision combination is certified. |
| Inputs and manager | File, folder, selected archive formats, plugin, and BodySlide parsing/export paths have automated coverage. | Real installed mods, MO2 virtual filesystem behavior, load-order conflicts, and actual BodySlide/Outfit Studio builds remain unverified. |

### External BodySlide and Skyrim build gate

Do not treat generated OSP/ShapeData as build-ready based on the automated tests
alone. For supported Skyrim SE unskinned `BSTriShape` inputs, generation can
inspect shape names and vertex order and emit per-shape `<Shape>` declarations.
It currently withholds OSD files and non-zap `<Data>` links because parsed source
morph payloads do not yet prove association with the intended shape and vertex
order, and synthetic fallback deltas do not establish authored deformation
semantics. Conversion quality reports the high-severity
`bodyslide-osd-morphs-withheld` issue and the withheld-record count. The project is
not build-ready; missing, unreadable, unsupported, or unverified source data must
not be guessed into a successful OSD build.
Matched source OSP settings that the exporter reconstructs differently—including
custom slider-set/path options, source output-path relocation, defaults, zaps,
weight-output mode and nested `<Low>`/`<High>` slider ranges, references, and
seam/normal flags—are listed in
`SourceAssetSupport.UnsupportedOspSemantics` and produce the medium-severity
`source-osp-semantics-not-preserved` validation issue. These diagnostics identify
settings needing review; they do not claim those settings were preserved.
TRI files are a separate in-game morph format, not substitutes for OSD shape
deltas. The converter intentionally withholds generated TRI files because its
current morph path cannot prove target-shape identity and vertex-order
correspondence. This avoids presenting project-wide vertex-count estimates or
synthetic deltas as usable in-game morphs. Conversion quality reports the
high-severity `bodyslide-tri-payload-withheld` issue, so this output is not
reported as ready. BodySlide may generate TRI files during its own validated
build; inspect and test those files independently before packaging.
Source TRI/BSD/OSD parsing records candidate payloads, but does not establish
which NIF shape or vertex order they belong to. Exact reuse requires verified statuses, matching named shapes, non-empty shape
correspondence evidence, and matching vertex-order fingerprints. Topology retarget
additionally requires an explicit verified map method, at least 0.95 confidence,
and non-empty map/correspondence evidence. `PayloadReuse.RetargetEvidence` records
the source/target shape names, method, confidence, and evidence used for an
accepted retarget. The current source
discovery path does not produce that verified mapping, so parsed payloads remain
ineligible by default. Synthetic OSD fallback remains separately labeled and
does not count as recovered source morph data.

On a Windows test host:

1. Use a disposable MO2 profile and a version-matched Skyrim, BodySlide, Outfit
   Studio, body, skeleton, and armor stack. Install the converter output as its
   own enabled test mod. Keep the profile's saves and Overwrite isolated from
   the normal profile.
2. Before building, inspect each generated OSP: every non-zap `<Slider>` must
   contain one `<Data>` entry per declared target shape; each entry's `target` must match a
   declared `<Shape target="...">`; and the final component of its text path
   must equal its `name` and identify a record in the referenced OSD. Confirm
   all referenced ShapeData files resolve from the installed test mod through
   MO2. A missing link, unknown shape, missing record, or unresolved path is a
   failed preflight, not a reason to guess a target.
3. Record the BodySlide version and game-data configuration. From MO2, launch
   the installed `BodySlide.exe` with the generated outfit name, a known
   compatible preset, an empty temporary target directory, and TRI generation
   enabled. Current upstream builds expose `--build`, `--preset`, `--targetdir`,
   and `--trimorphs`; verify the syntax supported by the installed version
   before running. For example:

   ```text
   BodySlide.exe --build "<generated outfit name>" --preset "<test preset>" --targetdir "<empty temp output>" --trimorphs
   ```

   Save the exact arguments, process exit code, BodySlide log, MO2 `usvfs` /
   `mo_interface` logs, and a file listing of the output directory. Confirm
   both expected weight meshes are produced, BodySlide generates the TRI when
   requested, and the build does not silently copy an unchanged source mesh.
4. In Outfit Studio, open the generated project and verify the expected shape
   names and every slider. Move representative sliders to both endpoints and
   confirm the intended shape deforms; exercise each zap on and off, check
   weight variants, then save and reopen the project to test persistence.
5. Install the built meshes as a separate test mod in that disposable MO2
   profile. In a disposable game session/save, test low and high body weights,
   the corresponding in-game morph sliders, and zapped shapes. Check for
   missing shapes, bad topology, clipping, and crashes. Do not save over a
   normal playthrough. Retain the Skyrim edition/runtime, enabled-mod list,
   BodySlide/Outfit Studio versions, screenshots, output mesh hashes, and logs
   with the review evidence.

This procedure describes the required external evidence; it is not evidence
that the current generated projects pass. No BodySlide, MO2, or Skyrim runtime
is available in this Linux validation environment.
Batch armor-pack reports now keep otherwise-ready items at `needs-review` until
the imported proof report marks runtime automation, Desktop E2E, and live-game
execution complete. A partial `executed-pass` is not sufficient; the report
lists pending proof status per item and includes it in pack-level blockers.
Built-in compatibility catalog entries are likewise guidance, not tested
combinations. Until the exact body/skeleton/physics/armor/game/runtime stack has
external evidence, conversion quality emits
`catalog-target-support-unverified` and downgrades readiness to `needs-review`.

`TargetBodySupport` in conversion reports now labels metadata as `embedded-built-in-catalog`,
`custom-profile`, or `unavailable` and separately reports whether it was externally
verified. A complete or internally consistent profile is still configured data, not
proof that its reference mesh, slider ranges, skeleton, or physics runtime matches an
installed mod version. `SourceAssetSupport` separately reports the count and provenance
of slider names parsed from discovered OSP/TRI/BSD/OSD data; parsing is not version
validation, and profile defaults or path-based inference remain labeled separately.
The `direct` metadata-reliability signal describes profile completeness; it does not
certify a real Skyrim/body combination.

The converter accepts armor and clothing as mesh assets; it classifies mesh behavior
(including cloth, leather, plate, skin-tight, and physics-enabled) and headgear
subtypes to guide conversion and partition handling. Those classifications do not
replace inspecting the actual garment coverage, topology, weights, and target-body
reference. A correct cross-body conversion must use the source body/reference to
interpret the outfit, transfer shape and skin weights to the target’s body regions
and skeleton, preserve valid partitions and plugin paths, and check both weight
endpoints. Merely renaming a body, copying a preset, or exporting a nonempty NIF is
not evidence of a correct fit.

Body-family distinctions currently recorded in the built-in catalog are useful
conversion hints, not independently verified mod specifications:

| Catalog profile | Configured distinction |
|---|---|
| CBBE | Female baseline profile; no physics is selected by default, although physics bones are listed for an explicit override. |
| 3BA | Catalog describes it as CBBE-topology with extended physics weighting; SMP+CBPC default and additional physics sliders/bones. |
| BHUNP | Catalog describes it as UUNP-family with advanced physics; SMP+CBPC default and its own reference/detection tokens and slider list. |
| HIMBO | Male profile with pec-related physics bones; SMP default. |
| SOS | Male profile with SOS-named genital physics bones; SMP default. |

These records do not currently store authoritative slider ranges or versioned
provenance for every reference mesh and morph. When source OSP/TRI/BSD assets are
available, the converter can use them; inferred/fallback data must remain marked
as such. For an unfamiliar or changed body release, provide the matching installed
BodySlide assets and treat detection, build output, skeleton/physics reports, and
in-game fit as separate checks.

Automated failure fixtures currently include unsupported NIF layouts (including
NiLines), ambiguous plugin links, and topology-review cases. To move a combination
from “profile/configured” to “verified,” add a redistributable, anonymized fixture
and regression for it, then record separate Windows BodySlide-build, MO2 install,
and live-game evidence. Until that evidence exists, review the generated readiness
and remaining-gaps reports and do not treat success status as universal support.

Batch conversion pairs `_0`/`_1` meshes only within the same source directory.
Distinct armor folders sharing a mesh name are converted separately, with
deterministic, collision-free per-armor output folder names. Unique names retain
their existing output layout.
Colliding plugin-free armor also receives distinct scratch-plugin names and
game-relative world/first-person/ground mesh namespaces, preventing installation
of separate packages from overwriting each other's generated plugin references.
Their BodySlide project/ShapeData identities are distinct, and their OSP build
paths match those namespaced plugin mesh references. Folder batches discover
source plugins once at the pack root; shared exports are serialized and retain
the accumulated mesh rewrites rather than overwriting earlier armor mappings.
Plugin references are resolved against the complete batch mesh set before each
item stages only its owned mappings. BodySlide names are allocated after
sanitization and body qualification, including natural body-suffixed names.

Both desktop and CLI startup preserve a valid caller working directory, including
the game/Data directory selected by a mod manager. Relative input and launch paths
are no longer silently rebased to the executable directory. Explicit CLI options
still take precedence over desktop handoff.
Desktop readiness/proof scans now run in the background after the window opens,
not in its constructor. Ordinary GUI launches automatically record startup phases
in `%LOCALAPPDATA%\SlideSmith\startup-launch-diagnostics.log` (bounded to roughly
1 MB); Windows CLI-to-desktop handoffs now record these diagnostics automatically
too, including candidate discovery and immediate process failures.
`--startup-diagnostics <path>` selects another location. Windows smoke
checks show the window and run the message loop before closing.

For a launch that appears to do nothing, compare the timestamp of a **fresh**
startup log with the attempted launch. No new entry does not establish a managed
application bug: collect MO2's `mo_interface`/USVFS log and the Windows application
error (if present) to distinguish a missing runtime, native host/injection failure,
or an incorrect executable path. A `window shown` entry instead points to a
different failure than a log ending before window construction. Test the same
complete desktop folder directly and through MO2; do not disable Windows security
or bypass MO2 injection as a substitute for verifying virtual asset visibility.

For duplicate-key or unexpectedly slow/large conversions, retain the complete
exception and `conversion-timings.json`, and record the source/output byte sizes,
mesh count, target body, and whether the input is an installed mod or an uninstalled
FOMOD archive. Alternative installer folders may contain conflicting versions of
the same plugin; select the intended installed variant rather than combining those
plugins. Supply a minimal permitted reproduction and launcher diagnostics before
claiming the original real-world failure has been resolved.
Conversion now stops before mesh/morph processing when multiple paths supply the
same plugin identity (for example four installer variants of `BDE_Armor.esp`).
No variant is selected automatically. Install the archive with MO2/Vortex first,
then convert the chosen installed mod, or prepare a folder containing one selected
plugin/body variant and its shared assets. Archive extraction still occurs before
this check; this is not a FOMOD choice interpreter.
The desktop now presents **Select installed mod folder...** for this condition
instead of a generic conversion-failed dialog. Select the individual installed
source mod (for example its BHUNP variant), including shared textures and BodySlide
assets; keep the desired destination body (for example 3BA) in the conversion
settings. Review the setup and start conversion again. Cancelling the selection
leaves the original input unchanged, and the next run repeats all safety checks.
The input and output paths use full-width stacked sections. Source and destination
body labels are aligned consistently. **Show advanced settings and tools** reveals
shape/physics overrides, skeleton support, cache configuration and custom-profile
tools; hiding them preserves their values and leaves source-body selection visible.
Start conversion, Cancel, Load result and output options stay outside the scrolling
setup, above progress and results. Report, file, preview, cache and diagnostic
actions are grouped in their corresponding tabs instead of one crowded toolbar;
custom-profile actions stay beside the profile list. Result tab headers wrap.
Source/destination terminology is shared by selectors, summaries and inspection
guidance. Setup sections and action rows wrap at narrower widths; the minimum
window is 800×640, with scrolling for setup rather than hiding conversion actions.
The Windows layout smoke check verifies control bounds at minimum/default sizes
in both basic/advanced and preset/manual modes, including when setup is scrolled.
Extraction also rejects duplicate normalized file destinations instead of silently
overwriting an earlier mesh, texture or plugin. Separator, dot-segment and
case-only aliases are treated as the same game path, including on Linux.
Partially extracted workspaces are removed after failure; select a single installed
variant or repair the source archive rather than relying on entry order.
Constant missing-texture fallbacks use compact 4×4 DDS maps instead of expanding
every channel to the diffuse texture's resolution. Detail-derived maps retain
their detail; a compact neutral fallback is not a substitute for authored textures.
Optional auxiliary-map derivation accepts only BGRA8, ordinary 2D DDS sources
up to 16,777,216 pixels (64 MiB of base-level pixels). Larger or unsupported
sources use the neutral fallback without loading their pixel payload for derivation;
original textures are still copied unchanged. Derived DDS files contain a valid
single-level header, and derivation checks cancellation during processing.
Morph diagnostics in `morphs.json` and the conversion manifest describe parsed
source payloads as candidates, recording the source asset filename and explicit
shape-identity/vertex-order verification states without duplicating per-vertex
arrays. The current source readers mark shape identity unresolved and vertex order
unverified, so exact vertex-count matches are not enough to reuse a payload. Sparse
TRI/OSD payloads retain only their represented index span; nearby NIFs are not used
to inflate that span, and it must not be mistaken for a complete shape vertex count.
Topology retargeting is also withheld unless an explicit retarget map is verified.
Generated BodySlide OSD morph records are currently withheld because authored
deformation provenance, shape identity, and vertex order are not verified. Any
synthesized morph deltas used elsewhere remain unverified and are not authored
BodySlide OSD data; generated TRI files are withheld for the same provenance gap.
Readable BodySlide projects no longer automatically force every pack texture into
each armor export: their resolved input NIFs participate in shader dependency
checks, including `ShapeData`/`DataFolder` references. Morph-only OSD files do not
redirect textures. Missing, unreadable, ambiguous multi-folder project inputs,
unknown NIF layouts, materials, scripts, and plugin texture swaps still retain the
conservative full texture set. Companion selection uses hashed filename prefixes
instead of comparing every texture against every referenced family.
Source morph discovery also respects authored `DataFolder` paths: a missing
reference NIF or morph file must not be replaced by a same-named file in
`SliderSets`, the BodySlide root, or a folder named after the project. Discovery
and cache invalidation use the authored ShapeData folder, even when its name
differs from the SliderSet name.
Export retains discovered source OSD files and BodySlide support XML alongside
copied source projects; copying the OSP without those dependencies can leave
original sliders/groups incomplete. This preserves discovered files, not missing
provider assets, and does not prove the copied projects build successfully.
OSP slider and zap names are selected per SliderSet using its declared output
mesh stem (including `_0`/`_1` normalization), so unrelated armor projects imported
from the same pack do not add their sliders. Legacy sets without output metadata
retain their existing behavior. When a source mesh is under a `meshes` root,
declared `OutputPath` must match its game-relative directory, distinguishing
same-named armor in different folders. This does not yet prove shape-specific
OSD/TRI payload ownership; loose files without a known `meshes` root retain
filename-only compatibility.
Morph import rejects non-finite displacements, out-of-range vertex indexes,
overflowing counts and malformed trailing TRI bytes. Reads are capped at 64 MiB
per BSD/TRI/OSD file; dense expansion is capped at 8,388,608 deltas per payload
and 250,000 vertices. Oversized/unreadable sources stay on the existing missing
source-data/review path rather than allocating gigabytes or claiming reusable
morph support. OSD parsing tries exact 16/32-bit layouts before padded layouts,
preventing 32-bit indexes and deltas from being silently read as 16-bit data.
BodySlide TRI files may include an optional UV morph section; its structure is
validated without treating UV offsets as position displacements. Legacy files
ending after position morphs remain supported. Body TRI position morphs are
retained per declared shape with separate vertex counts; duplicate shape names
are rejected because they make shape association ambiguous. A declared name
does not by itself verify source-to-target shape identity or vertex order, so
TRI morphs remain ineligible for reuse until correspondence is established.

Completed per-item conversions write `output-size-inventory.json` beside their
timing reports. This metadata-only inventory separates physical mesh, texture,
morph, plugin, report, archive and other bytes; a sibling ZIP is counted in
addition to unpacked files, not as a duplicate-free install size. The scan checks
cancellation, skips links and stops after 100,000 entries. `Complete=false` and
warnings identify partial inventories. The inventory excludes itself and is
written after ZIP closure, so it is not included in that ZIP or completed pipeline
timing.

Batch runs also write `batch-performance.json` at the requested output root (or
the first generated variant root) after packaging and archive cleanup. It records
total wall time, extraction/discovery/conversion/packaging/cleanup phase totals,
output-category bytes, process allocation bytes and sampled peak working set.
Discovery means the initial mesh scan; conversion includes item export and
internal dependency scans, including ZIP creation for a single input. The
250 ms working-set samples include concurrent process activity and can miss short
peaks. Byte totals cover existing output contents plus sibling ZIPs, not source
sizes or newly written bytes. Inventory skips links and stops at 100,000 entries;
incomplete inventories are explicitly marked. Final inventory/report writing is
outside measured wall time, and the final report is outside the completed ZIP.
Failure/cancellation reporting is best-effort and never replaces the conversion
exception.

Enable **Compact diagnostics** in the desktop conversion options, or pass
`--compact-diagnostics true` to the CLI, to minify diagnostic JSON and replace
large interactive preview/workbench pages with concise, non-interactive review
summaries. Default exports remain unchanged. Report names and JSON schemas,
conversion quality, actionable validation evidence, physics/dependency records
and required BodySlide/install assets remain available. This reduces report
byte overhead, not the number of JSON files or the actual mesh, texture or morph
payload sizes. Required evidence files are retained; compact mode is not a
minimal-report/export mode.

Issue #8 reporting/performance corrections:
- Morph generation and BodySlide preparation share one immutable source-resolution
  result per conversion/target. There is no global payload cache or cross-pack reuse.
- Linked-source cache stamp checks reuse bounded directory snapshots rather than
  recounting the whole directory for every referenced asset. Cancellation is
  checked on this path, including cached lookups.
- Root reports remain the authoritative diagnostics; exports no longer copy them
  into a sibling `.reports` folder. Existing folders from older runs are not removed.
  Required OSP/OSD/TRI/ShapeData files are unaffected.
- ZIPs retain required diagnostic/install JSON, including dependency, quality,
  skeleton, physics and plugin evidence. Completed-after-packaging timing and
  inventory JSON, learning-cache state and legacy `.reports` copies stay outside
  the distributable archive; compact mode does not remove required evidence.
- Final timing JSON, pipeline profile and conversion log use the same completed
  measurement (including export queue wait and ZIP preparation). The final ZIP
  includes the corrected log, not the pre-export snapshot. Final report writing and
  ZIP central-directory closure are outside that measurement.
- Fallback semantic profiles require exact identifiers or distinctive observed
  anchors. Generic short tokens cannot select Spriggan for a 3BA breastplate.
  Catalog physics bones/sliders are expectations, not observed mesh evidence.
- `BodySlideCompatible` denotes generated scaffold compatibility, not a successful
  external build; `SourceBodyMatchRatio` is heuristic confidence, not fit accuracy.
- Generated version-1 OSPs now use `DataFolder` with the project folder name and a
  leaf `SourceFile`. BodySlide already prefixes ShapeData; repeating the full path
  caused the missing-input errors visible in issue #8. Regenerate old conversions.
- Generated projects declare one extension-free `OutputFile`, using `GenWeights`
  only for a complete weight pair. BodySlide adds `.nif` or `_0.nif`/`_1.nif`
  itself; orphan weight meshes preserve their original stem without inventing a pair.

The uploaded timing report records 813,629 ms for one item: 259,537 ms for morph
generation, 248,249 ms for BodySlide preparation and 291,905 ms for export.
Extraction is not measured by that per-item import timing. The supplied diagnostic
files total roughly 1.4 MB, so a large output requires a byte inventory of the
actual NIF, texture, morph and archive payloads before attributing it to JSON.

Linked OSP discovery now selects matching output projects before resolving their
DataFolders and morph payloads, with project-scoped cache keys. This prevents
cross-project payload reuse but does not establish per-shape ownership within a
selected project. Original multi-project support assets remain preserved.
Matched source OSPs are audited for nonzero slider defaults, inversion/UV flags,
source slider-data links, shape/reference and zap mappings, custom base shapes,
non-current OSP versions, external DataFolders, seam/lock-normal settings,
output options, weight-variant mode, and unrecognized set/slider fields. These
settings are not silently copied: `SourceAssetSupport.UnsupportedOspSemantics` records the detected categories,
and `conversion-quality.json` adds `source-osp-semantics-not-preserved` for review.
Detection is evidence that a setting was present, not proof the generated project
preserves its behavior; absence of a category is not a general OSP compatibility claim.

Remaining issue #8 acceptance work: structural NIF support does not prove all UV/skin/partition/material
relationships, runtime physics linkage/conflicts, or shape-specific source morph
association. Empty texture lists do not prove textures are unnecessary. A permitted
sample is also needed to verify shape-specific OSP slider data links and exported
morph formats against an actual BodySlide build; the input/output path corrections
alone do not establish usable sliders or correct mesh deformation. Original
BHUNP/CBBE project input errors may instead reflect missing or disabled providers.
Provide the affected source OSP and complete linked ShapeData, generated OSP/NIF/
OSD/TRI files, BodySlide version/build log, MO2 provider/overwrite information,
source/output byte inventories and current batch timings. A permitted
source/output sample is needed to distinguish external textures from missed
discovery and to regenerate the earlier dependency/FOMOD/3BA-physics fixes. Test
the selected variant in BodySlide, MO2, xEdit and Skyrim before treating it as ready.

Packaging validation accepts the generated `dependency-map.json` mesh-entry
array as well as legacy object reports; malformed/empty arrays remain invalid.
FOMOD includes `conversion-quality.json` even though it is generated after the
installer manifest. Default runtime physics configurations use framework-supported
target-body metadata when no explicit bone set is supplied, so 3BA thigh coverage
is not lost to generic five-node defaults. Explicit bone sets remain authoritative;
configuration coverage alone does not prove mesh weights or in-game physics.

`manual-cleanup-likely`, `pose-risk`, `extreme-topology-adaptation`, and
`low-body-match` are not packaging errors and are not suppressed by these fixes.
For an affected outfit, provide its `conversion-quality.json`,
`topology-correspondence.json`, `pose-simulation-report.json`,
`skeleton-compatibility.json`, and a permitted source/ShapeData reproduction.
Confirm the actual source body and skeleton, inspect the listed morphs and hot
regions in Outfit Studio, and rebuild/test in BodySlide and Skyrim before release.
Plugin verification accepts repeated normalized source paths and keeps an
unsupported report when duplicate reports disagree, rather than throwing a
duplicate-key exception or hiding the unsupported mesh.

Body catalogs describe supported names, sliders, rig families and physics
expectations; they are not a bundled set of real reference bodies or proven
deformation data for every armor. Missing readable references or matching morph
payloads still require review. Adding aliases or inferred bone names cannot prove
fit, functioning zaps, valid weights, or in-game physics. These need actual
BodySlide builds and game validation using permitted source assets.
`CatalogIntegrityTests` checks every declared body alias, skeleton-foundation
resolution and physics-bone uniqueness. These checks validate metadata consistency,
not real-world compatibility. Custom-profile fields and built-in catalog fields
are separate supported schemas, not interchangeable copies of one another.

Body detection ignores GUID-shaped path components and leaf names as opaque
workspace identifiers. Accidental body-name substrings inside those identifiers
cannot outweigh BodySlide metadata; meaningful body-named folders remain signals.
BodySlide fallback inference also excludes these identifiers from individual and
condensed path evidence so they cannot inject unrelated built-in slider families.

Physics readiness counts a generated CBPC/SMP config only when its XML has the
expected root and nonempty named bone entries. Empty, malformed, or unrelated
XML cannot satisfy a requested runtime config, including one missing half of a
hybrid profile. On-disk package validation applies the same XML checks to every
root and staged runtime config, even for an unsupported target or a lone file,
and rejects DTD/entity declarations. Staged XML must match its exported root,
including bone names and solver values; formatting and attribute quote differences
do not count as drift. Physics coverage is read from parsed elements, not regex
matches in comments or broken XML. Unclassified custom bones can still be emitted for SMP, but do
not produce an empty CBPC config or silently acquire human fallback bones.
Support-file import includes source BodySlide projects, SliderGroups XML and OSD
assets without treating ShapeData reference meshes as conversion inputs; explicit
directory exclusions and generated-output markers still apply.

Folder/archive/plugin-root import discovers meshes and support files from one
per-import directory snapshot instead of two independent walks. Support discovery
keeps its depth-16 boundary; mesh discovery still reaches deeper armor folders,
without descending into excluded BodySlide reference trees beyond that support
boundary. Direct NIF import still selects only its weight pair and uses a bounded
support scan. Snapshots are not a global cache: subsequent imports see added or
removed files.

`ImportDiscoverySnapshotTests` compares the snapshot against independent scans
and records observational scan/import timings for 100- and 5,000-mesh synthetic
packs with matching texture counts. Run it with the existing `dotnet test`
runner and a TRX logger to retain the timing output. These discovery-only samples
are not real-user packs, cold-cache benchmarks, or end-to-end conversion proof.

Extension-specific batch/export scans retain and sort only matching files rather
than all source assets. Export plugin/material classification is case-insensitive
and still performs a fresh scan, including assets added since import; output and
shared-package exclusions remain in effect. Sparse synthetic support-scan tests
record observational timings, not real-pack or cold-cache guarantees. Cross-stage
snapshot reuse is not enabled because it could hide changed assets or change scan
depth coverage.
BodySlide package validation reuses one local ShapeData payload listing for
BSD/TRI/OSD checks, retaining the existing top-directory scope and refreshing it
for every validation; recursive staged-mesh discovery remains separate.

Local Release verification is separate from external coverage evidence: the
Linux packaged CLI can be self-checked here, but Windows desktop/MO2/Vortex click
paths, BodySlide builds and live-game physics must be validated on the target
Windows mod stack with redistributable real-user reproductions. CI runs marked
`action_required` have not executed their jobs and are not passing build evidence.

## Run

```bash
# build Windows desktop executable package (single-file, self-contained)
dotnet publish src/Bodyslide.Desktop/Bodyslide.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

# launch desktop GUI during development (Windows)
dotnet run --project src/Bodyslide.Desktop

# load an existing output folder directly in the Desktop app
dotnet run --project src/Bodyslide.Desktop -- --load-result "<output folder|preview html|fomod\\ModuleConfig.xml>"

# GUI features: drag/drop input (accepts .nif, plugin .esp/.esm/.esl, archive, or folder),
# separate **"File..."** and **"Folder..."** browse buttons for the input field (no more double-dialog),
# explicit FROM/TO wording in the conversion options (FROM = source armor body, TO = destination body),
# inspect selected input before conversion (detected body, mesh type, skeleton compatibility, custom-body count), open selected input path, preset details panel, choose preset or custom target,
# preset panel tip that every supported body has a matching `<Body> Zeroed` preset,
# optional preset-batch / target-batch comma-separated lists for one-run multi-body conversions,
# **"Convert to All Bodies" button** — one click sets the target batch to every supported body type,
# optional profile/source/physics/world-drop-mode override, optional BodySlide export toggle, optional output zip,
# **optional Skeleton NIF path** — point to your installed skeleton.nif (e.g. XPMSSE) for accurate bone mapping without auto-discovery,
# optional global learning-cache path override and in-app learning-cache inspector,
# built-in **Run self-check** action + **Readiness** tab for first-run executable validation,
# cancel in-progress conversion, open output folder,
# embedded in-app preview pane for generated preview-workbench.html (with preview.html fallback), "Load result..." button to browse and reload
# any previous output folder's preview, quick-open batch reports when available, an in-app files tab
# that lists generated outputs with double-click/open-button launch, and a catalog tab that lists
# all built-in presets, supported body signatures, deformation profiles, physics profiles, and
# `all/any/*` target aliases in one place,
# "Load Custom Profile..." button (multi-select *.json) to inject extra body profile definitions into
# the conversion without placing them next to the input files, a loaded-profile list with open/remove/clear
# actions so active custom bodies are visible in the GUI, and "Save Profile..." to export the current
# target body as a reusable *.json profile with transformation field / sliders / gender / output-path data

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

# override dropped-item world mode generation in world-physics.json
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --world-mode rigid-proxy

# skip BodySlide slider/project export for a lighter output package
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "3BA" --build-sliders false

# override the auto-detected source body type
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --source "UNP" --target "3BA"

# produce a mod-manager-ready ZIP instead of a bare output folder
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --output-zip

# override the global learning-cache location (default: %APPDATA%\SlideSmith\ on Windows)
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "3BA" --cache-path "D:\MySlidesmithCache\.conversion-learning-cache.json"

# supply a skeleton NIF for accurate bone mapping (e.g. XPMSSE installed via mod manager)
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "CBBE" --skeleton-nif "C:\Modlist\XPMSSE\meshes\actors\character\character assets\skeleton.nif"
# You can also point --skeleton-nif at an XP32/XPMSSE mod folder or a related .pex file from the same mod;
# SlideSmith will resolve the matching skeleton .nif automatically.

# show built-in presets
dotnet run --project src/Bodyslide.Standalone -- --list-presets

# show available deformation profiles
dotnet run --project src/Bodyslide.Standalone -- --list-profiles

# show supported body types with detection tokens, vertex-count hints,
# skeleton foundation, semantic/collision support regions, and soft-body physics coverage expectations
dotnet run --project src/Bodyslide.Standalone -- --list-bodies

# show deep reference info for one body (canonical names and common aliases both work),
# including semantic regions, collision focus, bilateral expectations, and minimum physics coverage
dotnet run --project src/Bodyslide.Standalone -- --body-reference "3BBB"

# show available physics profiles
dotnet run --project src/Bodyslide.Standalone -- --list-physics

# show a practical conversion guide (body/physics/skeleton workflow + recommended command shape)
dotnet run --project src/Bodyslide.Standalone -- --conversion-guide

# inspect the learning cache — prints all cached entries with target body, mesh type, strategy, and regional morphs
dotnet run --project src/Bodyslide.Standalone -- --export-cache

# inspect the learning cache at a custom location
dotnet run --project src/Bodyslide.Standalone -- --export-cache --cache-path "D:\MySlidesmithCache\.conversion-learning-cache.json"

# verify the executable/runtime before attempting a conversion
dotnet run --project src/Bodyslide.Standalone -- --self-check

# target a custom body profile discovered from a nearby *.slidesmith-body.json file
dotnet run --project src/Bodyslide.Standalone -- --input "<armor path>" --target "MyFollowerBody"
```

## Downloading pre-built executables

**The easiest way to get SlideSmith is from the [GitHub Releases page](../../releases):**

| File | Platform | What it is |
|---|---|---|
| `SlideSmith.exe` | Windows | Desktop GUI — double-click to open, drag-and-drop armor |
| `SlideSmith-CLI.exe` | Windows | Command-line tool — run from a terminal with `--help` |
| `slidesmith-win-x64-bundle.zip` | Windows | MO2/Vortex-installable SkyrimSE bundle; installs the desktop EXE and runtime files under `SlideSmith`, with `cli/SlideSmith-CLI.exe`, `README.txt`, `meta/slidesmith-bundle.json` and `meta.ini`; archive also contains FOMOD metadata |
| `slidesmith-linux-x64.zip` | Linux | Single CLI binary |

Every push to `main` automatically updates the **"SlideSmith — latest build"** pre-release entry on the Releases page. Versioned releases are published by pushing a `v*` tag.

For Mod Organizer 2, set Binary to the installed physical path
`<mod>\SlideSmith\SlideSmith.exe` and Start in to its containing
folder. Leave Arguments empty for a normal GUI launch; `--mo2-launcher` only adds
context information and does not enable or repair MO2's VFS/USVFS hook.
Keep every desktop runtime file alongside the EXE, and launch from MO2 when
testing managed asset visibility.

If `SlideSmith-CLI.exe` is launched with **no arguments**, it can auto-open the
desktop in the same folder, a sibling `desktop/` folder (unpacked bundle), or
the installed parent folder above `cli/`. The parent-folder shared
`SlideSmith.exe` name is accepted only when its runtime configuration identifies
Windows Desktop, so unrelated CLI builds are not treated as a GUI. Explicit
CLI conversion/list/help switches still bypass GUI handoff.

## GitHub Actions (CI)

This repository includes `.github/workflows/build.yml`, which runs automatically on pushes to `main`/`master` and on pull requests.

What it does:
- **Every push/PR:** restore, build, test, publish a single-file Linux CLI binary, and upload it as a temporary Actions artifact.
- **Push to `main`/`master` (post-merge):** publish clean single-file Windows executables (Desktop GUI + CLI), create/update a Windows bundle zip with FOMOD + `meta.ini` metadata for MO2/Vortex installs, create or update the rolling **"SlideSmith — latest build"** GitHub Release entry, and attach all three Windows artifacts.
- **Pull requests:** publish both Windows executables, package them as one MO2/Vortex-friendly bundle zip artifact, and upload it for startup/packaging verification.

Standalone executable downloads remain self-contained single files. The Windows
mod-manager bundle uses a self-contained folder-based desktop build without native
self-extraction; its installer puts the EXE and all runtime DLLs together in
`SlideSmith` at the Skyrim Data root. Do not copy only the bundle's desktop EXE. Windows CI
checks dependencies and smoke-tests the extracted, installed desktop layout.

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
- `aliases`
- `referenceTokens`
- `vertexCountMin` / `vertexCountMax`
- `heightToWidthRatioMin` / `heightToWidthRatioMax`
- `depthToWidthRatioMin` / `depthToWidthRatioMax`
- `transformationField` (`chest`, `waist`, `pelvis`, `legs`, `shoulders`, `breasts`, `butt`, `belly`, `arms`, `thighs`, `calves`)
- `sliderNames`
- `zapSliderNames`
- `physicsBones`
- `physicsProfile` (`none`, `cbpc`, `smp`, `smp+cbpc`)
- `expectedSemanticRegions`
- `expectedCollisionRegions`
- `expectedBilateralRegions`
- `minimumPhysicsSlotCount`
- `minimumPhysicsChainDepth`
- `minimumPhysicsFamilyCount`
- `collisionComplexity` (`none`, `minimal`, `standard`, `extended`)
- `gender` (`female` or `male`)
- `bodyOutputPath`
- `skeletonFoundation`
- `skeletonFramework`

When SlideSmith detects an unknown/incomplete target body or a low-confidence/custom detected source body, it now also writes starter templates such as `target-body-template.slidesmith-body.json` and `detected-source-body-template.slidesmith-body.json` into the output folder so you can refine and reuse them. Those starter templates now include semantic-region, collision-region, bilateral-region, collision-complexity, and minimum physics coverage hints so support quality can be strengthened instead of only naming the body and sliders.

## Support tiers

SlideSmith now emits a graded support tier in its validation outputs so the app does not pretend every successful file export is equally safe:

- `mainstream-automatic` — strong body/skeleton/topology evidence; conversion, physics, and safe-animation signals all look good
- `advanced-review-required` — conversion is viable, but runtime/body-fit/topology review is still required before release
- `experimental-manual-cleanup` — conversion can proceed, but sparse skeleton evidence, heuristic-heavy topology, unsupported physics, or extreme body differences still make manual cleanup likely

The generated JSON reports also separate:

- `CanConvert`
- `CanPhysicsConvert`
- `CanSafelyAnimate`

Use those fields together with `SupportTier` instead of treating every successful conversion as universally install-ready.

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
      SliderGroups/
        <ArmorName>.xml          ← BodySlide batch-build/search groups (when slider export is enabled)
      ShapeData/<ArmorName>/
        <ArmorName>.nif          ← BodySlide source-shape mesh (when enabled)
        <ArmorName>.osd          ← per-shape sparse slider payload, only for supported NIFs
        (no generated TRI)       ← withheld until shape and vertex-order mapping is verified
  textures/...                   ← source textures (preserved relative paths)
  <PluginName>_patched.esp       ← full-copy patched plugin (if source ESP found)
  <PluginName>_SlidesmithPatch.esp ← minimal override patch ESP (ARMA-only)
  fomod/
    info.xml
    ModuleConfig.xml             ← FOMOD with <files> entries for meshes/ + CalienteTools/ + SKSE/
  SKSE/Plugins/CBPCSystem/
    cbpc-config.xml              ← staged CBPC physics XML for mod-manager/manual Data installs
  SKSE/Plugins/hdtSMP64/
    smp-config.xml               ← staged SMP physics XML for mod-manager/manual Data installs
  cbpc-config.xml                ← compatibility/root copy of generated CBPC physics XML
  smp-config.xml                 ← compatibility/root copy of generated SMP physics XML
  meta.ini                       ← neutral MO2 package metadata
  conversion-manifest.json       ← full pipeline log
  README.txt                     ← user-facing installation guide
  preview-workbench.html         ← real 3D point-cloud workbench from converted mesh vertices
  preview.html                   ← interactive body heatmap + morph diagnostics report
  ...                            ← additional metadata/diagnostic files (see table below)
```

| File | Description |
|---|---|
| `<ArmorName>.nif` (root) | Original-filename copy of the converted mesh (workspace artifact) |
| `<ArmorName>_0.nif` + `<ArmorName>_1.nif` (root) | Low/high-weight variant pair at output root; **missing half is auto-synthesised** when only one is present |
| `meshes/slidesmith/<body>/<ArmorName>.nif` | Data-relative staged mesh; pointed to by the generated plugin |
| `meshes/slidesmith/<body>/<stem>_ground.nif` | Ground/loot mesh companion for every converted NIF variant |
| `CalienteTools/BodySlide/SliderSets/<ArmorName>.osp` | BodySlide slider-set project (open in BodySlide Studio) — written only when slider export is enabled |
| `CalienteTools/BodySlide/SliderGroups/<ArmorName>.xml` | BodySlide group definitions so converted single-piece and batch outputs show up under predictable SlideSmith/body filters for search and Batch Build |
| `CalienteTools/BodySlide/ShapeData/<ArmorName>/<ArmorName>.nif` | BodySlide source-shape reference mesh; required for the slider editor to display the base mesh — written only when slider export is enabled |
| `CalienteTools/BodySlide/ShapeData/<ArmorName>/<ArmorName>[_setN].osd` | Native sparse Outfit Studio records linked per shape/slider from the OSP — emitted only when named shapes can be read and fit OSD's 16-bit limits |
| `CalienteTools/BodySlide/ShapeData/<ArmorName>/*.tri` | No TRI files are generated by the converter until shape identity and vertex order can be verified; a separately validated BodySlide build may create TRI output |
| `fomod/ModuleConfig.xml` | FOMOD installer with populated `<files>` entries mapping `meshes/`, `CalienteTools/`, and `SKSE/` to Data sub-folders; mod managers (MO2, Vortex) read this to install all files correctly |
| `fomod/info.xml` | FOMOD package metadata (name, version, author) |
| `meta.ini` | Neutral Mod Organizer 2 metadata for the packaged output so the installed mod folder/archive keeps basic name/version/author context without depending on manual tagging |
| `SKSE/Plugins/CBPCSystem/cbpc-config.xml` | Data-relative staged CBPC physics config for direct installation into Skyrim's SKSE plugin layout |
| `SKSE/Plugins/hdtSMP64/smp-config.xml` | Data-relative staged SMP physics config for direct installation into Skyrim's SKSE plugin layout |
| `cbpc-config.xml` | Compatibility/root copy of the generated CBPC physics config for inspection or manual relocation |
| `smp-config.xml` | Compatibility/root copy of the generated SMP physics config for inspection or manual relocation |
| `conversion-manifest.json` | Full conversion log with all pipeline steps |
| `dependency-map.json` | Per-mesh dependency map linking related textures, physics, body refs, plugin mesh references, **detected source body**, **ARMA FormIDs**, and **source skeleton** |
| `skeleton-compatibility.json` | Full bone-mapping report: source skeleton name, target skeleton name, every mapped bone pair, unsupported bones, source skeleton reliability, physics compatibility, and graded `SupportTier` / `CanConvert` / `CanPhysicsConvert` / `CanSafelyAnimate` signals |
| `conversion-quality.json` | Machine-readable quality metrics: body-detection confidence + evidence, strategy used, per-region morphing, clipping/voxel/pose risk, topology drift warnings, BodySlide compatibility, graded support tier/readiness fields, and ISO-8601 generation timestamp |
| `in-game-validation.json` | Runtime review plan summary: validation gate, support tier, conversion-readiness fields, scenario matrix, caveats, topology correspondence, and checklist guidance for live animation/body-fit review |
| `topology-correspondence.json` | Dedicated topology review artifact: correspondence classification/confidence, semantic-vertex-matching status, unmatched focus regions, semantic anchor evidence, and review artifacts for manual cleanup decisions |
| `conversion-matrix-proof.json` | Cross-axis proof summary showing which body-support, topology, skeleton, plugin/mod-stack, runtime, live-game, and Desktop E2E axes are or are not strictly proven yet, plus the remaining blocking gaps and review artifacts |
| `runtime-validation-plan.json` | Release-gate execution plan derived from the in-game validation report; documents what still needs a live game harness or manual runtime pass before the output is truly trusted |
| `runtime-validation-harness.json` | External-harness contract for runtime validation automation: dispatch actions, expected assertions, failure signals, and which probes require full-load-order launches or manual observation |
| `proof-harness-bundle.json` | Canonical external-proof entrypoint linking the Desktop UI flow, runtime harness, live-game scenario catalog, explicit Windows host requirements, replayable evidence layout, import targets, and the `proof-result-bundle.json` contract |
| `mod-stack-cross-validation.json` | Mixed plugin/race/body-family review summary: plugin/master counts, distinct mesh families, skeleton reliability, race warnings, and recommended runtime/load-order scenarios for large real mod stacks |
| `desktop-workflow-automation.json` | Shared-output contract for Desktop/UI workflow coverage: preview/report state, artifact inventory, suggested GUI flow, and automation limitations for result-reload/report-rendering paths |
| `proof-result-bundle.json` | External-harness result import written back into the output root; records host details, per-axis pass/fail status, scenario coverage, probe coverage, evidence references, and any missing proof inputs |
| `proof-evidence-integrity.json` | Optional inventory from `scripts/Validate-ExternalProofEvidence.ps1`; records referenced evidence files, byte lengths, and SHA-256 hashes |
| `schemas/proof-result-bundle.schema.json` | JSON Schema 2020-12 shape contract for imported external proof results |

The optional output ZIP mirrors the installable game files only; the JSON review artifacts stay in the folder output so they remain easy to inspect without bloating the packaged mod archive.
| `armor-pack-validation.json` | Batch-only pack validation rollup: per-item readiness status/score, dominant issue codes, and pack-level ready/review/high-risk counts for real armor-pack runs |
| `world-physics.json` | Dropped-item/world-object physics guidance: selected world mode (`static` or `rigid-proxy`), collision-shape recommendation, whether source/equipped physics were detected, ground-mesh availability, and practical install/runtime recommendations |
| `plugin-patches.json` | Detected sidecar plugin mesh paths + structured rewrite mappings (`OriginalMeshPath` → `RewrittenMeshPath`) and per-mesh patch steps |
| `patch-armor.pas` | xEdit Pascal automation script (SSEEdit / TES5Edit): runs ARMA mesh-path rewriting directly inside the tool |
| `<PluginName>_patched.<ext>` | **Full-copy patched plugin** — a direct copy of the source `.esp`/`.esm`/`.esl` with every ARMA `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh-path subrecord updated in-place; supports both Skyrim LE and SE record header formats |
| `<PluginName>_SlidesmithPatch.esp` | **Minimal override patch ESP** — contains ONLY the patched ARMA records and lists the original plugin as its master; safe to load after the original |
| `README.txt` | Human-readable installation guide with FOMOD and manual install instructions |
| `preview-workbench.html` | Browser-openable real 3D workbench rendered from converted mesh vertex data, with drag/zoom controls |
| `preview.html` | Browser-openable live preview report with regional morph heatmap, pose-clipping summary, and interactive controls |
| `batch-report.json` | Root batch summary when input is a folder or archive (total/success/fail counts and per-armor results) |
| `.conversion-learning-cache.json` | Learning cache for faster repeated conversions |

When a matching cache entry exists for the same armor mesh + target body, the converter reuses prior regional morphing data and marks `learning-cache:hit` / `learning-cache:reused` in pipeline steps. The cache is written to both the local output folder (`.conversion-learning-cache.json`) **and** a shared global location (`%APPDATA%\SlideSmith\` on Windows, `~/.config/slidesmith/` on Linux/macOS) so the tool learns from all prior conversions across different armor packs. Use `--cache-path` to specify a custom global cache location.

When `--targets` / `--presets` (or the desktop batch-entry boxes) are used, each requested body/preset is exported into its own subfolder under the selected output root so multiple conversions never overwrite each other.

### Supported external Windows proof workflow

1. Run a conversion and treat `proof-harness-bundle.json` as the single canonical entrypoint for external proof execution.
2. On the Windows harness host, open the bundle manifest and use `ArtifactEntrypoints`, `ScenarioCatalog`, `ReplayableEvidence`, and `ImportTargets` to collect the referenced runtime, live-game, Desktop UI, and observation/template artifacts instead of discovering them ad hoc.
3. Execute the required Desktop UI flows, runtime probes, and live-game scenarios on the target Windows/mod-stack/game install while writing evidence to the machine-readable locations declared in `proof-harness-bundle.json` (for example `proof-evidence/screenshots/`, `proof-evidence/runtime-logs/`, `proof-evidence/step-traces/`, `proof-evidence/probe-observations/`, and `proof-evidence/scenario-observations/`).
4. Write the completed version 1.2 `proof-result-bundle.json` back into the output root with host details, per-axis status, executed flows/probes/scenarios, missing items, and evidence references keyed to the exported `ScenarioMatrix`/`ScenarioCatalog` names. Include exactly one passing `ValidationObservations` record for each required type, with evidence under `proof-evidence/validation/bodyslide-build/`, `proof-evidence/validation/output-inspection/`, and `proof-evidence/validation/deformation-observation/`. Each record must include the type-specific `Details` fields required by the schema: exact build context and output hashes, inspected-file hashes and check results, or shape/slider low/high endpoint values and evidence.
5. Reload that output directory in SlideSmith/Desktop review. The app will re-ingest `proof-result-bundle.json`, refresh the files listed under `ImportTargets`, and surface the updated planned-vs-executed/imported proof state in reports/guidance.
6. Run `pwsh -NoProfile -File .\scripts\Validate-ExternalProofEvidence.ps1 -OutputDirectory "<conversion output>"` from the repository root before importing. Every observation must identify the tool/version/time and reference at least one artifact in its matching category folder. The checker rejects missing or unsafe references, validates required detail fields, checks reported build/inspection file sizes and SHA-256 values against the files, and writes a SHA-256 inventory. Use a JSON Schema 2020-12 validator with `schemas/proof-result-bundle.schema.json` to validate the canonical PascalCase result-bundle structure. Passing these checks validates observation metadata and evidence-file integrity only—it does not independently verify that a build, inspection, or deformation observation is truthful or correct.

The script proves only that referenced evidence files resolve inside the output directory and records their sizes and hashes at check time. It does not authenticate who produced them, interpret their contents, execute BodySlide/MO2/Skyrim, or prove a slider deforms correctly. A successful script run is not external proof completion and does not bypass SlideSmith's imported-proof readiness gate.

## Issue #2 progress comparison

Implemented from issue scope:
- import scan across single mesh, folder, and archive input (`.zip`, `.7z`, `.tar`, `.tar.gz`, `.tgz`)
- batch mesh discovery now skips support/body-reference NIFs (e.g., skeleton and body base/reference files) so only convertible armor/clothing meshes are processed
- body detection (CBBE, UNP, UUNP, COCO CBBE, COCO UUNP, HIMBO, BHUNP, 3BA, TBD, SAM, SOS, UBE, CUSTOM fallback); bone-name scoring from physics XML for higher confidence
- body detection reference comparison now scores body-reference asset names (`*.tri`, `*.osp`, reference mesh names) against known body templates as additional evidence
- body detection UV-signature evidence now samples mesh UV coverage/aspect ranges from readable NIF geometry and factors it into confidence scoring (`uv:u=... ,v=...`)
- mesh analysis, cage/strategy stages, weight transfer, morph generation, partition rebuild, clipping detect/correct, physics configs
- plugin scan, texture summary (6 DDS categories), vanilla armor lookup (105+ entries across base game + Dawnguard/Dragonborn DLC), voxel collision pass, BodySlide OSP output, OSD slider data and TRI input parsing, learning cache reuse
- automatic CI builds with Linux/Windows executable zip artifacts uploaded in Actions for PR and merge testing (no release publishing)
- FOMOD metadata output (`fomod/ModuleConfig.xml`, `fomod/info.xml`)
- **output `.nif` file(s)** written to the output directory; `_0`/`_1` weight variant pairs detected and written as matched pairs
- **`--source` flag** to override auto-detected source body type (`--source CBBE`, etc.)
- **`--list-bodies` flag** to enumerate all supported body types with detection tokens
- **`--export-cache` flag** — prints all learning-cache entries (target body, mesh type, strategy, per-region morphs, last conversion timestamp) to standard output for inspection/debugging; accepts an optional `--cache-path` to read from a custom location
- **runtime readiness self-checks** — CLI now exposes `--self-check`, and the desktop GUI includes a **Run self-check** button plus a **Readiness** tab so users can validate the EXE path, pipeline initialization, cache location, scratch-write access, and WebView2/browser preview readiness before running conversions
- **source→target relative delta conversion** — `StrategyMeshConversionService` now computes `targetField[region] / sourceField[region]` per region so converting e.g. CBBE→UNP applies only the directional difference rather than the full UNP field; emits `conversion-delta:CBBE→UNP` step
- **vanilla recommended profile auto-apply** — when the vanilla armor database identifies a match and no explicit `--profile` was provided, its `RecommendedProfile` is automatically applied (emits `vanilla-profile:<name>` step)
- **armor region binding by bone names** — new `IArmorRegionBindingService` / `BasicArmorRegionBindingService` detects which body regions (chest, waist, pelvis, legs, shoulders, arms, breasts, belly, butt) the armor covers by scoring physics-file bone name tokens, falling back to mesh filename keywords, then full-body default; emits `regions:<list>,method=<detection-method>` step
- **geometry signature scan** — lightweight NIF vertex-count/bounds sampling now feeds body detection evidence (`verts:<count>`) and adds a `spatial-geometry` fallback for armor region binding when readable mesh coordinates are available
- **batch summary report** — converting a directory or archive input (`.zip`, `.7z`, `.tar`, `.tar.gz`, `.tgz`) now writes `batch-report.json` to the root output folder with total/success/fail counts, target body, timestamp, and per-armor result entries
- **armor-pack validation rollup** — batch conversions now also write `armor-pack-validation.json`, aggregating each armor's validation status/score plus pack-wide ready/needs-review/high-risk counts and top issue codes so full packs can be triaged faster
- **real 3D preview/workbench + live diagnostics HTML** (`preview-workbench.html`, `preview.html`) — export now writes a browser-openable 3D workbench that renders sampled vertex data from converted NIF output (drag to rotate, wheel/slider to zoom, optional auto-spin) and links directly to the diagnostics preview report; `preview.html` keeps the regional morph heatmap, BodySlide/physics tables, pose-clipping risk summary, and interactive control panels
- **plugin xEdit automation script** (`patch-armor.pas`) — generated alongside `plugin-patches.json` whenever plugins are detected; a runnable Pascal (Delphi) script for SSEEdit/TES5Edit that rewrites matching ARMA world + first-person mesh paths to the generated SlideSmith mesh targets (with rewrite logging)
- **pose simulation** (`pose-simulation-report.json`) — `BasicPoseSimulationService` tests the converted mesh against 8 animation poses (T-pose, Walk, Run, Idle, Crouch, Combat-Idle, Jump, Sneak) using per-pose per-region stress amplifiers; regions where `morph_factor × pose_amplifier ≥ 1.10` are flagged as at-risk; report written as JSON and visualised in the preview HTML; emits `pose-simulation:tested=8,...` pipeline step
- **deeper mesh/physics solver tuning** — strategy conversion now runs a region-adjacency smoothing solver with mesh-type-specific clamp/blend iterations, and physics XML generation now applies adaptive stiffness/offset/damping/restitution tuning (including reduced offsets when physics weights are missing) for more stable outputs
- **NIF block graph parsing for geometry nodes** — conversion now parses `Ni*` block/type spans first (e.g. `NiTriShapeData`) to locate real vertex streams before fallback heuristics, improving transform reliability on non-synthetic NIF layouts
- **NIF backend strategy** — the shape reader is gated to Skyrim SE 20.2.0.7 / user version 12 / stream version 100 and reads named, unskinned `BSTriShape` position/index data. Supported parsed shape streams now feed the writer directly as one combined geometry instead of using heuristic vertex-block discovery. Legacy `NiTriShape`, skinned shapes, dynamic shapes, and other block layouts remain outside this exact writer path; topology-aware handling of separate shape islands is still incomplete. Parser/writer fixtures are synthetic and do not establish external format compatibility. No permitted real-format fixture with clear redistribution provenance has been established for this path. NiflySharp is GPL-3.0; this repository has no root license, so do not add it or upstream NIF code until the project licensing decision and distribution/compliance review are complete.
- **support asset carry-forward** — export now copies scanned textures, material files (`.bgsm`/`.bgem`), physics files, plugin files, and body-reference files (`.tri`/`.osp` plus skeleton `.nif`) into the output tree using source-relative paths so converted packs include required sidecar assets
- **external custom body profiles** — import now auto-loads nearby `*.slidesmith-body.json` files so conversions can target named custom bodies with custom detection tokens, transformation fields, slider sets, male/female BodySlide metadata, and per-body physics defaults instead of falling back to a generic `CUSTOM` output; explicit paths can also be supplied programmatically via `ConversionRequest.CustomProfilePaths` (or the GUI's **Load Custom Profile…** button) and are merged on top of any auto-discovered profiles so ad-hoc profiles work without being placed next to the input
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

- **flat/aux texture stub generation** — missing normal and auxiliary maps use compact 4×4 neutral BGRA8 DDS fallbacks. Supported BGRA8 sources within the auxiliary derivation budget can supply roughness from specular luminance, approximate height from normal-map gradients, or heuristic glow from bright diffuse pixels. Nonconstant derived maps retain source dimensions but contain only their base level; constant results are compacted. These heuristics do not recover authored surface detail or prove that bright pixels should glow. Original textures remain unchanged; generated maps can be replaced by authored maps. Emits `normal-stubs:generated=N` and `aux-stubs:...` entries in `conversion.log`.

- **skeleton NIF parsing** — `BasicSkeletonMappingService` now reads any `skeleton*.nif` files from the armor's body reference list and parses their string table to extract actual bone names; the source skeleton label (`xpmsse-vanilla`, `xpmsse-physics`, or `fo4-biped`) is inferred from physics-marker bones (`NPC *Breast*`, `*Butt*`, `*Belly*`, `*Pec*`, `*Lat*`) and Bip01 prefixes, giving accurate bone-mapping reports in `skeleton-compatibility.json` without relying solely on hardcoded lists

- **BodySlide project preflight** — generated `.tri` and OSD payloads are withheld until shape identity, vertex order, and source-morph provenance are verified. Supported unskinned Skyrim SSE `BSTriShape` NIFs may provide named shape declarations, but non-zap OSP data links are absent and readiness is blocked. Source morph reuse, zap target semantics, and other NIF layouts remain unsupported or unverified. A real BodySlide/Outfit Studio build check is still required before generated projects can be treated as verified.

- **ARMO ground-model synthesis** — when an ARMO record has rewritten `MOD2`/`MOD3` world model paths but no `MODL` ground mesh subrecord, plugin rewrite now auto-appends `MODL` using the rewritten world model path so dropped-item world meshes stay aligned with converted armor outputs

- **ARMO world-model completion from `MODL`** — when an ARMO record includes a rewritten `MODL` ground mesh but is missing `MOD2` and/or `MOD3`, plugin rewrite now auto-appends the missing world model subrecord(s) from that rewritten `MODL` path so inventory/world model lookups stay valid for both male and female model entries

- **ARMA first-person completion from rewritten third-person paths** — when an ARMA record is rewritten but missing `MOD4` and/or `MOD5`, plugin rewrite now auto-appends the missing first-person subrecord(s) from the rewritten `MOD2`/`MOD3` paths so first-person model lookups remain populated after conversion

- **morph-aware clipping region detection** — clipping detection now evaluates per-region morph intensity with mesh-type thresholds (instead of mesh-type-only flags), normalizes equivalent regions (e.g. hips→pelvis), and auto-flags armpit risk when shoulder/arm/chest pressure is high; this produces more realistic region highlights before auto-correction

- **shrinkwrap clearance projection pass** — NIF vertex transformation now enforces a per-region body-envelope minimum radius with adaptive clearance after animation-driven push-out, so near-surface vertices are projected outward instead of lingering on the clipping boundary

- **headgear sub-type classification and head/hair partition assignment** — the mesh analysis stage now classifies all headgear into one of four sub-types: `full-helmet` (full head-covering piece; keywords: helmet, greathelm, warhelm, sallet, barbute, bascinet), `hood` (cloth/leather hair-covering; keywords: hood, cowl, coif, veil, shroud), `face-mask` (partial face covering; keywords: mask, visor, blindfold, eyepatch, facecover), or `circlet` (small accessory worn over hair; keywords: circlet, crown, diadem, tiara, hat, cap); partition rebuilding then assigns the correct Skyrim `BSDismemberSkinInstance` skin-partition IDs per sub-type — full-helmet → slots 30 (Head) + 31 (Hair), hood → slot 31 (Hair), face-mask → slot 30 (Head), circlet → slot 42 (Circlet) — preventing invisible head parts, hair z-fighting, and circlet/helmet slot conflicts in-game

- **ground mesh NIF generation** — export now calls `IGroundMeshGeneratorService` to produce a `<stem>_ground.nif` alongside each converted armor mesh (stored under `meshes/slidesmith/<body>/`); when a real source NIF is available its bytes are proxy-copied so the dropped-item world representation exactly matches the worn mesh; when no source bytes are available a minimal valid Gamebryo 20.2.0.7 stub (zero-block) is synthesized so Skyrim can parse the record without crashing; the ground mesh relative path is also threaded into the scratch plugin generator so standalone exports carry a complete MODL entry

- **biped slot passthrough from plugin BOD2/BODT** — after partition rebuilding, `ConversionOrchestrator` now reads all decoded `BipedSlots` from the scanned plugin's ARMA records and merges any slots not already covered by the rebuilt partitions into the final partition list using `KnownPartitionSlotNames` labels; emits a `biped-slots-passthrough:<slot1>,<slot2>,...` pipeline step when the source plugin contains at least one slot that was absent from the rebuilt set, preventing mods from losing their original slot assignments

- **standalone (scratch) plugin generation** — when no source plugin was found among the input assets, export now calls `IScratchPluginGeneratorService` to produce a self-contained ESL-flagged `.esp` directly in the output directory; the plugin contains a complete TES4 record that declares `Skyrim.esm` as master (required for DefaultRace lookups), an ARMO record (FormID 0x801) with `OBND` (object bounds), `BOD2` (body-slot mask + armor-type code), `KWDA` (keyword array with appropriate Skyrim.esm category keyword), world model `MOD2`/`MOD3` paths, `DNAM` (armor rating), and an `ARMA` subrecord linking to the armor addon, and an ARMA record (FormID 0x802) with `OBND`, `BOD2`, `RNAM` pointing to DefaultRace (0x000013), `MOD2`/`MOD3`/`MOD4`/`MOD5` mesh paths; the resulting ESP is immediately loadable in Skyrim Special Edition without manual xEdit editing

- **armor-type and keyword injection in scratch plugin** — `IScratchPluginGeneratorService.Generate()` now accepts an optional `meshType` parameter; the generator maps it to a BOD2 armor-type code (0 = light armor for leather/soft/skin-tight/default, 1 = heavy armor for plate/rigid, 2 = clothing for cloth/robe) and injects the matching Skyrim.esm keyword FormID into a `KWDA` subrecord on the ARMO record: `ArmorClothing` (0x0006BBE8) for cloth, `ArmorHeavy` (0x0007E8C4) for plate/rigid, `ArmorLight` (0x000A8669) for all other types; both the ARMA and ARMO `BOD2` records carry the same armor-type code so SkyUI, vendor filters, and Creation Kit categorisation are all correct out-of-the-box

- **first-person mesh paths in scratch plugin** — the scratch plugin generator now produces distinct MOD4 (female first-person) and MOD5 (male first-person) subrecords whose paths use a `_1stperson` stem suffix (e.g. `meshes/slidesmith/3ba/iron_1stperson_0.nif`) instead of repeating the third-person MOD2/MOD3 path; this matches the vanilla Skyrim ARMA record convention where first-person arms have a separate, lighter NIF that the game loads during first-person camera mode

- **rigid island detection** — `BasicRigidIslandDetectionService` analyses each converted mesh by armour type and subdivides it into rigid attachment zones that a physics solver must treat as non-deformable; plate armour is segmented into 10 anatomical islands (cuirass-front, cuirass-back, pauldron-L/R, gauntlet-L/R, greave-L/R, sabaton-L/R) via `plate-anatomy-clustering`; leather armour yields 5 accent islands (buckle-front, stud-L/R, tasset-L/R) via `material-zone-clustering`; mixed (plate+leather) armour yields 3 hybrid zones via `hybrid-zone-clustering`; cloth, skin-tight, and headgear meshes return zero islands; the `ConversionOrchestrator` calls the service after mesh conversion and records island count, plate coverage, and detection method in a `rigid-islands:` pipeline step

- **target physics-bone selection and diagnostics** — the weight-transfer stage selects available physics bones from the built-in target-body profile, resolves/remaps them through the declared skeleton framework, and records mapped and unsupported names in conversion steps and reports; headgear does not receive target physics bones. Bone lists differ by profile (for example, CBBE, 3BA, BHUNP, HIMBO, and SOS each have different catalog entries); consult `built-in-bodies.json` rather than assuming one fixed count per family. This metadata and reporting do **not** prove that exported vertex weights, the user's installed skeleton, CBPC/SMP runtime, or in-game simulation are correct; those require separate mesh inspection and runtime validation.

- **scratch-plugin mesh staging** — when no source plugin exists, export now stages converted meshes into the exact `meshes/slidesmith/<body>/...` paths referenced by the generated standalone ESP and also writes fallback `_1stperson.nif` copies so the MOD2/MOD3/MOD4/MOD5 paths all resolve without any manual file moves or extra mesh authoring
- **scratch-plugin MODL fallback** — standalone scratch ESP generation now always writes an ARMO `MODL` world/inventory model path; when a dedicated `<stem>_ground.nif` is unavailable, `MODL` automatically falls back to the primary converted mesh path so dropped-item lookups never end up blank
- **single-armor multi-target batch conversion** — CLI now supports `--targets "<body1,body2,...>"` and `--presets "<preset1,preset2,...>"`, and the desktop app exposes matching batch-entry fields for preset/target mode; one armor (or one folder/archive batch) can now be converted into multiple body outputs in a single run, with each target/preset written into its own output subfolder to avoid collisions
- **desktop output-files tab** — GUI now includes a dedicated **Files** tab that enumerates generated output artifacts (`.nif`, plugin patches, JSON reports, BodySlide files, etc.) for both fresh and reloaded conversion folders, with single-click selection + **Open file** action (or double-click) so users can launch any artifact directly without manually browsing the output directory
- **all-body target alias** — `--target all` / `--target any` / `--target *` (and the same tokens inside `--targets`) now expand to every supported body type automatically, so one command can export a full multi-body conversion pack without manually listing each body name
- **topology + UV mismatch diagnostics** — export now compares source vs converted mesh signatures and writes `TopologyMismatchRisk`, `VertexCountDeltaRatio`, `UvCoverageDeltaRatio`, `UvAspectRatioDelta`, and `QualityWarnings` into `conversion-quality.json`; large drift thresholds flag likely topology/UV mismatch risks early (addressing a major “common failure point” from issue #2)
- **physics-bone fallback remapping for skeleton compatibility** — skeleton mapping now aligns UNP/TBD targets with their CBPC support set (`NPC L/R Breast01`, `NPC L/R Butt`, `NPC Belly`) and auto-remaps unsupported higher-order source physics bones (e.g. `NPC L/R Breast02/03`) to the best available target equivalent before marking them unsupported, reducing conversion drop-off when source and target skeleton physics depth differ
- **preview GUI fallback + safer saved target profiles** — when embedded WebView2 preview is unavailable (or fails), the desktop app now automatically opens `preview-workbench.html` (or `preview.html` fallback) in the system default browser so preview access is never blocked by missing runtime dependencies; “Save profile...” also now preserves typed target text (fallback `CUSTOM`) instead of relying only on selected dropdown items
- **desktop conversion catalog tab** — GUI now includes a **Catalog** tab that exposes all built-in presets (target/deformation/physics), supported body detection tokens + vertex ranges, deformation profiles, physics profiles, and `all/any/*` target aliases so CLI discovery flags have an in-app equivalent
- **desktop reports tab** — GUI now includes a native **Reports** tab plus **Open report** action that surfaces key fields from `batch-report.json`, `conversion-quality.json`, `dependency-map.json`, `skeleton-compatibility.json`, `texture-summary.json`, `pose-simulation-report.json`, `world-physics.json`, and `plugin-patches.json` for both fresh conversions and reloaded output folders, so users do not have to dig through raw JSON to inspect converter diagnostics
- **custom profile management GUI** — the desktop app now shows every loaded custom body profile in a dedicated list with open/remove/clear actions, and “Save profile...” writes a reusable full custom-body payload (transformation field, slider names, gender, output path, detection tokens, physics profile) instead of a minimal stub
- **learning-cache GUI tab** — the desktop app now includes a dedicated **Cache** tab that lists loaded conversion-learning-cache entries (key, target body, mesh type, strategy, clipping/correction status, timestamp, and regional morph factors); **Inspect cache** now populates this tab and auto-focuses it while still logging cache details
- **world drop-mode override controls** — CLI/GUI now expose world dropped-item mode override (`auto` / `static` / `rigid-proxy`) so users can force `world-physics.json` behavior even when source physics heuristics would choose a different default; override state is recorded in conversion steps and world-physics recommendations

Issue #2 baseline coverage has been expanded substantially (import/dependency scan, body detection, mesh strategy, plugin rewriting, patch generation, output packaging, morph payload export, headgear sub-type/partition handling, ground mesh NIF output, biped slot passthrough, scratch plugin generation for plugin-free inputs, first-person mesh paths, rigid island detection, target physics bone injection, and armor-type/keyword injection in scratch plugins).

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

## Built-in body profiles

The current catalog contains these 28 configured profiles:

- CBBE-family: CBBE, 3BA, COCO CBBE
- UNP-family: UNP, UNPB, UUNP, BHUNP, COCO UUNP, TBD
- Male: HIMBO, SAM, SAM Light, SOS, TNG
- Other/special-framework profiles: UBE, Vanilla, Vanilla Beast, Serpentine Humanoid, Goat Humanoid, Hagraven, Spriggan, Equine Humanoid, Avian Humanoid, Feline Humanoid, Canine Humanoid, Draconic Humanoid, Insectoid Humanoid, Aquatic Humanoid

Any unrecognized source body can be classified as `CUSTOM`; that fallback is not
itself a target-body profile or proof of compatibility. Use `--list-bodies` as the
runtime source of truth for profile names, aliases, detection tokens, and
vertex-count hints. `all`, `any`, and `*` request all available targets; they do
not imply that every requested conversion will pass its readiness/review gates.
See [Compatibility boundaries and evidence](#compatibility-boundaries-and-evidence)
for the distinction between catalog presence and tested support.

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
