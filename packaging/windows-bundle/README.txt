SlideSmith Windows Bundle
=========================

This bundle is intended for SkyrimSE Mod Organizer 2 / Vortex managed installs.

Included:
- CalienteTools/SlideSmith/SlideSmith.exe (desktop launcher after install)
- Desktop runtime DLLs alongside CalienteTools/SlideSmith/SlideSmith.exe
- CalienteTools/SlideSmith/cli/SlideSmith-CLI.exe
- CalienteTools/SlideSmith/meta/slidesmith-bundle.json
- CalienteTools/SlideSmith/meta.ini
- fomod/ metadata (installer mapping)

Quick start:
1) Install this bundle as a mod/tool package in your mod manager.
2) In MO2, add an executable that points to:
   - Binary: <MO2 mods folder>\SlideSmith Windows Bundle\CalienteTools\SlideSmith\SlideSmith.exe
   - Start in: <MO2 mods folder>\SlideSmith Windows Bundle\CalienteTools\SlideSmith
   - Arguments: leave empty for a normal GUI launch; --mo2-launcher is optional context information
3) Keep the complete desktop folder together. This bundle's desktop is self-contained
   without single-file extraction; copying only its EXE will not work.
   You can directly launch CalienteTools/SlideSmith/SlideSmith.exe (GUI) or CalienteTools/SlideSmith/cli/SlideSmith-CLI.exe (CLI).
4) Keep this package enabled as a separate managed entry.

Startup diagnostics are recorded automatically in
%LOCALAPPDATA%\SlideSmith\startup-launch-diagnostics.log. The log distinguishes
process entry, window construction, and window display. If no new entry appears,
failure occurred before managed startup (or the log directory was unwritable).
This packaging removes the native self-extraction step, but live MO2/USVFS
compatibility must still be checked on Windows.

Acceptance checks before trusting a converted pack
-------------------------------------------------
Passing the executable self-check or CI does not prove a conversion looks good
in Skyrim. Review/manual-cleanup warnings must remain until the specific issue
is resolved and the result has been tested.

1) Use a permitted, representative source pack. Record the source/target body,
   skeleton, physics setup, game version and launch arguments. Keep personal
   paths out of shared logs. Do not redistribute assets without permission.
2) Launch the GUI through MO2 and, separately, Vortex. Confirm assets visible
   only through the active managed setup can be inspected and converted.
   Verify relative input paths and output placement; do not assume an ordinary
   desktop launch proves MO2 virtual filesystem visibility.
3) Run the CLI with explicit --input, --target and --output arguments from the
   managed environment. It must perform the conversion without opening the GUI.
   Repeat from a working directory different from the executable directory.
4) Install the converted package in a disposable test profile. In BodySlide,
   locate each generated project and slider group, check previews at low/high
   weights and slider extremes, test applicable zaps, and build the outputs.
   Confirm the builds update the mesh paths referenced by the generated plugin.
   Check MO2's Data view for the winning OSP, ShapeData NIF/OSD and textures.
   Record where BodySlide writes its builds (Overwrite or a configured output
   mod), move only those observed generated files into the selected output mod,
   and confirm that mod wins the final mesh paths. Do not assume that a file
   existing on disk means it is the active profile's provider.
5) In xEdit, check generated plugin records, masters, model paths and partitions.
   Use either the full-copy patched plugin or the minimal override as instructed
   by the conversion reports; do not enable both alternatives blindly.
6) In Skyrim, inspect seams, clipping, proportions, textures and skin weights
   at both weight endpoints. Test movement/poses, first-person views, dropped
   armor, heels and physics as applicable. Record screenshots and runtime logs;
   preview success alone is not a live-game pass.
7) Measure import and conversion on a representative large pack with both cold
   and warm caches. Test cancellation in a disposable output location and verify
   a subsequent clean retry does not reuse incomplete output as a ready package.
   Preserve batch-performance.json from the output folder alongside per-item
   timing/inventory reports; the final batch report is not inside the ZIP.

Each conversion exports proof-harness-bundle.json as its external-validation
entrypoint. Follow its scenario/probe requirements and evidence locations.
Write only actually observed results to proof-result-bundle.json, then reload
the conversion output in SlideSmith/Desktop review. Do not mark unexecuted
BodySlide, xEdit, manager or game checks as passed.

For a failure report, provide a minimal permitted reproduction, target body,
launch mode/arguments, error logs and screenshots, and whether failure occurred
during launch, inspect, convert, build, installation or gameplay.

BodySlide "Unable to load input nif" troubleshooting
--------------------------------------------------
- A path containing ShapeData\CalienteTools\BodySlide\ShapeData indicates an old
  generated project repeating the BodySlide-relative root. Regenerate with the
  updated app into a clean output folder; disable/remove only the old conversion
  mod in a disposable test profile so its OSP cannot win a file conflict.
- Current generated version-1 projects use DataFolder = project folder name and
  SourceFile = NIF filename. Verify that exact NIF exists under
  CalienteTools\BodySlide\ShapeData\<project folder> in MO2's active Data view.
- Missing PlateDancersEdge_BHUNP or CBBE input files are separate provider issues:
  check the selected original mod/body installation, active profile and file
  conflict winner. The converter cannot manufacture their authored source NIFs.
- Keep source OSP, linked ShapeData (including OSD), and slider-group files
  together. If a current regenerated project still fails, provide that OSP and
  its referenced NIF, the BodySlide version/log, and MO2 file-provider details.
