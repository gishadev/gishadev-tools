## [1.2.1] - 2026-26-07
- Added read-only volume getters to `IAudioManager`: `MasterVolumePercentage`, `MusicVolumePercentage`, `SFXVolumePercentage`; volumes are set via `Set*Volume` methods
- Added `GetEffectiveVolume(AudioData)` to `IAudioManager`

## [1.2.0] - 2026-26-07
### Master volume & SFXEmitter volume support
- Added master volume — scales all audio relative to SFX/music volumes (`InitialVolume × typeVolume × masterVolume`)
- Added `VolumeChanged` event to `IAudioManager`, fired on any volume setter
- `SFXEmitter` now applies SFX and master volume to emitted pooled sounds (previously ignored AudioManager volumes) and updates already-playing ones on `VolumeChanged`
- Added `SFXBaseVolume` component caching the authored volume of pooled SFX instances so reuse doesn't compound scaling

## [1.1.4] - 2026-26-07
### Audio fixes
- Music with `IsFade = false` now plays instead of staying silent
- SFX and non-fade music apply `InitialVolume` (and volume percentage) when starting
- Fixed music fades: volume percentage now sets the fade target, not the fade speed (no more ignored volume, overshoot, or skipped stop)
- Music fades now react to volume changes mid-fade

## [1.1.3] - 2026-21-07
- Added Modern WEBGL Template

## [1.1.2] - 2026-17-05
### Minor fixes
- Virtual fields Enter and Exit in Page.cs

## [1.1.1] - 2026-17-05
### Minor fixes
- PoolManager fixes
- SFX Emitter & DisableSFXOnComplete fixes for longer SFX

## [1.1.0] - 2025-25-08
### Workflow improvements and fixes
- Pool emitters improvements
- Audio refactor
- Added icons for Scriptable Objects!

## [1.0.0] - 2025-07-02
### First Release
- Include SFX, VFX controllers
- Includes Scenes and UI managers
- Bonus scripts to clean up yout game!