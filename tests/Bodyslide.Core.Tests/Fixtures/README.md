# Synthetic NIF test fixture provenance

The small NIF fixtures constructed by helpers in `SkyrimSseNifShapeReaderTests`
are generated on demand, entirely from scratch. They contain no copied game
assets or third-party binary data and are covered by the repository's MIT
license in `LICENSE.txt`. Their layout is based on the public NIF schema
documentation linked from the root README; they test parser behavior and are
not external compatibility evidence.

This notice applies only to those generated test fixtures. It does not change
the provenance or license of the separate committed mod-pack fixture folders.
