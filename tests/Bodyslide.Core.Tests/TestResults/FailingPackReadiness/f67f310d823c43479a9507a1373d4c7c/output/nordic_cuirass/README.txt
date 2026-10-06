=============================================================
  SlideSmith Conversion Package — nordic_cuirass_0
  Generated: 2026-10-06 22:58 UTC
=============================================================

WHAT WAS CONVERTED
------------------
  Armor/clothing: nordic_cuirass_0
  Target body:    3BA
  Mesh strategy:  cage+rigid-islands+normal-preservation
  Meshes in:      2
  Textures in:    2
  Plugins in:     0

FILES GENERATED
---------------
  [Converted Meshes]
    nordic_cuirass_0.nif
    nordic_cuirass_1.nif
    nordic_cuirass_0.nif
    nordic_cuirass_1.nif
    nordic_cuirass_1stperson_0.nif
    nordic_cuirass_1stperson_1.nif
    nordic_cuirass_ground.nif
    femalebody_0.nif
    skeleton_female.nif
    nordic_cuirass_0.nif
    nordic_cuirass_1.nif
  [Plugin Files]
    SlideSmith_nordic_cuirass_0.esp
  [BodySlide Project]
    reference_body.osp
    nordic_cuirass_3BA.osp
  [BodySlide Slider Data]
    Belly.bsd
    Belly_1.bsd
    Butt.bsd
    Butt_1.bsd
    BreastsShape.bsd
    BreastsShape_1.bsd
    BreastsSmall.bsd
    BreastsSmall_1.bsd
    BreastsLarge.bsd
    BreastsLarge_1.bsd
    WaistWidth.bsd
    WaistWidth_1.bsd
    HipWidth.bsd
    HipWidth_1.bsd
    Thighs.bsd
    Thighs_1.bsd
    Calves.bsd
    Calves_1.bsd
    Arms.bsd
    Arms_1.bsd
    Shoulders.bsd
    Shoulders_1.bsd
    NarrowWaist.bsd
    NarrowWaist_1.bsd
    BreastsPhysics.bsd
    BreastsPhysics_1.bsd
    ButtPhysics.bsd
    ButtPhysics_1.bsd
    BellyPhysics.bsd
    BellyPhysics_1.bsd
  [Physics Configs]
    nordic_cuirass.xml
    cbpc-config.xml
    cbpc-config.xml
    smp-config.xml
    smp-config.xml
    nordic_cuirass_3BA.xml
    ModuleConfig.xml
    info.xml
  [FOMOD Installer]
    ModuleConfig.xml
    info.xml

HOW TO INSTALL
--------------
  OPTION A — Mod Manager FOMOD (recommended):
    Open your mod manager (Mod Organizer 2, Vortex, etc.) and install
    this output folder as a mod.  The included FOMOD installer will
    automatically place all files in the correct Data sub-folders.
    A mod manager also keeps the conversion isolated, makes rollback easy,
    and lets the generated meshes/plugins win conflicts without overwriting
    your base armor mod permanently.
    Place the SlideSmith output mod below the original armor/body mod so
    the converted meshes, physics XMLs, and generated plugins take priority.

  OPTION B — Manual (drop-in):
    The output folder is already structured as a Skyrim Data package.
    Copy the following sub-folders directly into your Skyrim Data\ folder:
      Data\meshes\slidesmith\3ba\  ← converted NIF meshes
      Data\CalienteTools\BodySlide\SliderSets\    ← BodySlide .osp project
      Data\CalienteTools\BodySlide\SliderGroups\  ← BodySlide batch-build/search groups
      Data\CalienteTools\BodySlide\ShapeData\nordic_cuirass_3BA\  ← .bsd sliders + source NIF
      Data\SKSE\Plugins\hdtSMP64\  ← staged SMP config
      Data\SKSE\Plugins\CBPCSystem\  ← staged CBPC config
    Also copy any generated plugin files (.esp/.esm/.esl) to Data\ root.
    Generated physics configs are already staged under SKSE\Plugins\.
    Root-level physics XML files are compatibility copies for inspection/manual relocation.

PLUGIN PATCH
------------
  No plugin was detected. Add the converted meshes to an existing .esp
  or create a new patch plugin in xEdit targeting 3BA.

BODYSLIDE
---------
  BodySlide project: nordic_cuirass_3BA
  Target body:       3BA
  Sliders included:  15
  Batch groups:      SlideSmith, 3BA, SlideSmith - 3BA

  To build in BodySlide:
    1. Open BodySlide and search for 'nordic_cuirass_3BA'.
    2. Use the '3BA' or 'SlideSmith - 3BA' group filter for batch builds.
    3. Select your body preset and click 'Build'.
    4. For physics sliders, also build the _1 (high-weight) variant.

NOTES & MANUAL STEPS
--------------------
  * Vertex transforms are heuristic — inspect converted meshes in
    Outfit Studio or NifSkope and fix any clipping or floating geometry.
  * Physics configs use tuned defaults. Adjust spring/damping values
    in the .xml files to match the cloth simulation feel you want.
  * Validation summary:
    Status: high-risk (score 0)
    Gate: FAIL
    This loaded conversion output is blocked. Fix conversion issues, re-run, and validate the regenerated output again.
  * Review these files first:
    - preview-workbench.html
    - conversion-quality.json
    - skeleton-compatibility.json
    - in-game-validation.json
    - CalienteTools/BodySlide/ShapeData/
    - pose-simulation-report.json
    - race-compatibility.json
    - plugin-patches.json
  * Recovery checklist:
    1. Open preview-workbench.html, conversion-quality.json, skeleton-compatibility.json, and the generated ShapeData in Outfit Studio, then plan manual cleanup for extreme topology drift, sparse custom skeletons, or sensitive oral/genital/beast appendage regions before release.
    2. Open preview-workbench.html and conversion-quality.json, inspect the converted mesh in Outfit Studio for UV drift, missing geometry, seam splits, and hole/window loop shape drift, then plan manual cleanup if the source and target topologies differ too much.
    3. Open skeleton-compatibility.json, conversion-quality.json, and preview-workbench.html, treat the automatic skeleton remap as unsafe, and switch to a closer framework/profile or manual Outfit Studio cleanup before release.
    4. Open skeleton-compatibility.json, install the skeleton expected by the target body, and patch outfit weights/bone names for any unsupported custom-rig bones.
  * Top reported issues:
  * [HIGH] manual-cleanup-likely: Automatic conversion reached a high-risk combination of topology drift, sparse skeleton inference, or sensitive oral/genital/beast appendage coverage. Manual Outfit Studio cleanup is still likely before release.
    Next step: Open preview-workbench.html, conversion-quality.json, skeleton-compatibility.json, and the generated ShapeData in Outfit Studio, then plan manual cleanup for extreme topology drift, sparse custom skeletons, or sensitive oral/genital/beast appendage regions before release.
  * [HIGH] topology-mismatch-risk: Converted mesh topology or UV layout drifted significantly from the source. (island-routing-drift:s1->t1,loops=0->0,holes=0->0,boundary=2->2,interior=23->9,nonmanifold=0->0, island-routing-extra:t4, island-routing-extra:t5, island-routing-extra:t6, island-routing-extra:t7)
    Next step: Open preview-workbench.html and conversion-quality.json, inspect the converted mesh in Outfit Studio for UV drift, missing geometry, seam splits, and hole/window loop shape drift, then plan manual cleanup if the source and target topologies differ too much.
  * [HIGH] unsafe-skeleton-remap: Automatic skeleton remap safety is unsafe for 'aquatic-humanoid' → 'xpmsse-physics' (unsupported-bones:113/218, candidate-gap:0, unsupported-ratio:0.52, remap-safety:unsafe).
    Next step: Open skeleton-compatibility.json, conversion-quality.json, and preview-workbench.html, treat the automatic skeleton remap as unsafe, and switch to a closer framework/profile or manual Outfit Studio cleanup before release.
  * [HIGH] unsupported-bones: 113 source bone(s) had no target equivalent.
    Next step: Open skeleton-compatibility.json, install the skeleton expected by the target body, and patch outfit weights/bone names for any unsupported custom-rig bones.
  * [MEDIUM] incomplete-source-fallback: Source BodySlide assets were incomplete, so fallback slider reconstruction was used: morph-payloads. Inferred source body: CBBE.
    Next step: Locate the original BodySlide OSP/TRI/BSD/reference assets for this outfit, place them beside the mod or under BodySlide/ShapeData, then re-run so conversion-quality.json no longer reports source-asset fallback.
  * [MEDIUM] low-body-match: Generated morphs matched 70 % of the source body signature.
    Next step: Open conversion-quality.json and preview-workbench.html, confirm the detected/source body is correct, then re-run with an explicit source-body override or better reference assets if the armor was matched to the wrong body family.
  * [MEDIUM] low-detection-confidence: Detected source body confidence is only 53 %.
    Next step: Open conversion-quality.json and preview-workbench.html, confirm the detected/source body is correct, then re-run with an explicit source-body override or better reference assets if the armor was matched to the wrong body family.
  * [LOW] missing-normal-maps: 2 diffuse texture(s) were missing authored normal maps and needed generated stubs.
    Next step: Open texture-summary.json, restore or generate the missing normal maps in the staged texture paths, and verify the converted outfit no longer ships with flat or mismatched lighting.

=============================================================
  Generated by SlideSmith — https://github.com/JosephsDeadish/
  Bodyslide-converter-tool-non-python-
=============================================================
