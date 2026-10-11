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
- No permitted real-format fixtures for varied armor types, body profiles, NIF variants, or failure modes yet.
- Desktop UX: input/target/risk/output summary, progress/cancellation, accessibility, and preview need refinement.
