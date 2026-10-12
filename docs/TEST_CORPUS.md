# Real-armor test corpus: policy and manifest

Permission to download or use a mod is not permission to redistribute its meshes in this repository. No third-party file is committed until its row below is `Cleared` with evidence for the exact files. Nexus Mods rules: if the author states no reuse permissions, ask; permissions apply per asset.

## Admission rules

1. Record per file: source URL, author, licence/permission text (quote or link), date checked, file hashes, and any third-party components.
2. Status is one of `Candidate`, `Asked`, `Cleared (redistribute)`, `Local-only` (authorized local testing, never committed), `Rejected`.
3. Only `Cleared` files go under `tests/Bodyslide.Core.Tests/Fixtures/real/<id>/` with an attribution and licence file alongside. ShareAlike terms (e.g. CC BY-SA 4.0) must be checked for compatibility with this repo's licence before committing.
4. `Local-only` sources are used via an environment-configured path and tests skip when absent.
5. Corpus results feed `docs/COMPATIBILITY.md`; a passing automated check is not a BodySlide build or in-game result.

## Candidates (unverified; not legal clearance)

| Id | Mod | Stated terms (per author page, to confirm) | Bodies | Status | To verify |
|---|---|---|---|---|---|
| elmlock | Elmlock Armor and Blade | CC BY-SA 4.0, attribution | CBBE, HIMBO | Candidate | Licence compatibility; third-party assets |
| ranger | Daring Ranger Armor | Reuse/conversion with credit | Vanilla, HIMBO | Candidate | Provenance of every file |
| smp-vanilla | HDT-SMP Vanilla Armors | Reuse with credit; includes others' assets | Vanilla, CBBE 3BA, HIMBO | Candidate | Per-component permission |
| duathfel | Duathfel Resource Pack SE | Asset use with credit; no re-upload of original file | n/a | Local-only | Never redistribute |

## Coverage matrix to fill

- Armor types: heavy, light, clothing, robes, helmets, gauntlets, boots, shields, layered outfits.
- Body profiles: vanilla, CBBE/3BA, HIMBO, other supported profiles.
- NIF variants: SE/AE, differing skin partitions, weighted meshes, multiple armor addons, alternate paths, unfamiliar blocks (must fail closed).
- Edge cases: differing/missing/extra bone weights, multiple texture sets, transparency, complex shader properties.
- Failure cases: corrupt, truncated, unsupported-layout files (can be authored by us as synthetic fixtures).

Each case should record expected outcome (converted / fail-closed with diagnostic) and the evidence level from `docs/COMPATIBILITY.md`.

## Research note (unverified web summary; confirm with authors)

- Armor meshes derived from vanilla Skyrim or from body mods (CBBE, 3BA, HIMBO) generally cannot be relicensed or redistributed in a public repo, even if the armor author grants reuse. Bethesda's assets remain Bethesda's, and body-mod permissions are separate. This likely affects all four candidates above, so `Cleared` needs written confirmation that covers the base-body and vanilla-derived content.
- No known open-licensed (CC0 / CC BY-SA) Skyrim SE armor NIFs built on original, non-derived geometry were found.
- Practical route: (1) keep `Local-only` real mods out of the repo, driven by a configurable path with tests that skip when absent; (2) author original, non-derived NIFs (own geometry, own skeleton data) as redistributable fixtures; (3) ask authors in writing for permission covering exact files.
