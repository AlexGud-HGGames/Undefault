# Core.Music

Contracts and reference types for the **safety-first dynamic music** architecture described in:

- [Music safety state](../../docs/music-safety-state-spec.md)
- [Volume composition](../../docs/volume-composition-spec.md)
- [Stability / device layer](../../docs/stability-and-device-layer-spec.md)
- [Mixer contract](../../docs/mixer-contract-and-device-wiring.md)
- [Config schema v1](../../docs/music-engine-config-schema-v1.md)

`DefaultMusicMixer` is a **testable reference** implementation of the v1 gain formula; host wiring may delegate or replace it per DI.
