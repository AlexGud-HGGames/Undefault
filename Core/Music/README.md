# Core.Music

Contracts for a **future** safety-first dynamic music engine. Not the Tauon pause/resume MVP.

See [docs/product-pivot-2026-08-14.md](../../docs/product-pivot-2026-08-14.md). Specs:

- [Music safety state](../../docs/music-safety-state-spec.md)
- [Volume composition](../../docs/volume-composition-spec.md)
- [Stability / device layer](../../docs/stability-and-device-layer-spec.md)
- [Mixer contract](../../docs/mixer-contract-and-device-wiring.md)
- [Config schema v1](../../docs/music-engine-config-schema-v1.md)

`DefaultMusicMixer` is a **testable reference** implementation of the v1 gain formula. Host wiring must not send mixer output to a player in the Tauon MVP (one side-effect path: control-profile actions).
