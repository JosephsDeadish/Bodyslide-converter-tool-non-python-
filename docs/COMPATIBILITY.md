# Compatibility and readiness tracking

Evidence levels (never merge them in claims):

1. **Catalog/profile present** – body, skeleton, or race data exists in an embedded catalog.
2. **Automated check** – unit/synthetic or committed-fixture tests pass (`tests/Bodyslide.Core.Tests`). Synthetic fixtures exercise code paths only.
3. **Real-tool build** – output built/loaded in BodySlide/Outfit Studio. *Not yet recorded.*
4. **In-game result** – verified in Skyrim. *Not yet recorded.*

## Tested combinations

| Armor type | Source body | Target body | NIF layout | Level | Notes |
|---|---|---|---|---|---|
| RealisticModPack fixture (folder/ZIP) | see fixture | see fixture | Skyrim SE | 2 | Core tests |

Add a row only with the highest level actually demonstrated.

## Known gaps

- Shape-aware NIF read/write covers limited Skyrim SE layouts; unsupported layouts must fail closed, and each new layout needs a round-trip test.
- OSD/named TRI reuse is limited to exact, uniquely linked shape matches; other payloads are ineligible. Extend only with demonstrated shape identity and vertex correspondence.
- Preserved skin weights do not prove fit on a different body; retargeting and live deformation need separate validation.
- No permitted real-format fixtures yet; see `docs/TEST_CORPUS.md` for policy and candidates (unverified).
- Desktop UX: input/target/risk/output summary, progress/cancellation, accessibility, and preview need refinement.

## Morph correspondence policy (decided)

- Evidence tiers: `exact` (current, reuse-eligible) > `topology-only` (same block type, vertex count and triangle list; positions may differ) > anything else (ineligible).
- Uncertain or ambiguous matches are skipped and reported, never applied with a warning.
- `SkyrimSseNifShapeCorrespondence.TryVerifyTopologyOnlyMatch` exists but is **not yet wired** into reuse. Eligibility is gated in `ConversionPipeline.cs` (`HasExactShapeAndVertexOrder` / `IsExactShapeCorrespondenceEvidence`), which require identical vertex-order fingerprints. Enabling the topology-only tier needs a new eligibility level, its own report label, and real-fixture evidence that per-index deltas are valid on moved vertices.
- Spatial retargeting between different topologies stays disabled until real fixtures demonstrate it is safe.

## Proximity conform (not yet wired)

`ProximityConform`, `ProximityWeightTransfer` and `ReferenceNifConform` implement BodySlide-style
reference-body conforming and weight transfer. They are unit-tested on synthetic meshes only, fail
closed on mismatched reference pairs, and are **not** called by the conversion pipeline yet. They
have not been validated against real CBBE/3BA/BHUNP bodies, BodySlide builds or in-game.

Opt-in: set `SLIDESMITH_REFERENCE_SOURCE_NIF` and `SLIDESMITH_REFERENCE_TARGET_NIF` to a source and
target reference body NIF to use proximity conform in place of the heuristic transform. Off by
default; when the pair or the outfit geometry is ineligible it falls back to the heuristic and
records `nif-conform:heuristic-fallback`. Successful runs record `nif-conform:proximity-unvalidated`.

## Real-file reader findings (BDE pack, Skyrim SE, Outfit Studio exports)

Checked against 15 real NIFs from a Nexus armor pack (kept local, not committed): the reader
previously rejected **all** of them. It now reads all 15 (opt-in corpus tests:
`SLIDESMITH_LOCAL_CORPUS=<dir> SLIDESMITH_LOCAL_CORPUS_EXPECT_ALL=1`). Fixes:
- real header layout (author/process/export short strings, 16-bit block type count);
- skinned shapes whose vertices live in `NiSkinPartition` (shape block has no vertex data);
- `hasFaces` flag and shape-wide triangle indices in multi-partition `NiSkinPartition`.

The vertex transform now rewrites the partition vertex stream for these files; output stays readable
with identical vertex counts. Not yet verified: that results look right in BodySlide/in-game.

## Real CBBE reference body (user-provided, kept local)

`femalebody_0/_1.nif` (13554 verts, one `CBBE` shape) parse with the NIF reader, and `femalebody.tri`
parses to 97 CBBE sliders with a matching vertex count. Conforming the body to itself plus its largest
morph with `ProximityConform` reproduces the morph (max error < 0.05; observed ~0.001). This validates
the conform math on real geometry. It does **not** validate CBBE -> 3BA/BHUNP fit: no 3BA/BHUNP body
mesh has been supplied, and nothing has been checked in BodySlide or in game.

## Real 3BA reference body (user-provided, kept local)

The `femalebody_0/_1.nif` on the uploads branch is now a 3BA body (shapes `3BA`, `3BA_Vagina`,
`3BA_Anus`, skinned BSTriShape, partition-embedded vertices). The NIF reader parses it, and its TRI
vertex count matches the main shape; proximity conform reproduces its largest morph. Also uploaded:
feet, hands and Argonian hands NIFs. Still missing: a BHUNP (source) body, so a BHUNP -> 3BA conform
cannot be validated; no BodySlide or in-game results.
