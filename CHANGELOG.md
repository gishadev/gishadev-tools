## [1.4.0] - 2026-27-07
### Changed
- Renamed generated pool enums for brevity: `SoundEffectsEnum` → `SFXPoolEnum`, `VisualEffectsEnum` → `VFXPoolEnum` (matches `OtherPoolEnum` and the existing `SFXEmitter`/`VFXEmitter` naming)
- Moved `GenerateExtensionsClass` out of `CodeGenerator` into `unity-setup` — it's static, one-time output tied to project setup rather than to `PoolDataSO`/`AudioMasterSO` changes, so it doesn't belong in the runtime package's per-edit codegen path
### Added
- `ISFXEmitter`/`IVFXEmitter`/`IOtherEmitter.EmitAt` now take `rotation` as optional (`Quaternion? rotation = null`, defaults to `Quaternion.identity`) since it's almost always left at identity

## [1.3.1] - 2026-27-07
### Fixed
- `ScreenFader` no longer forces `DontDestroyOnLoad` on every instance — ad-hoc faders now die with their scene like any other GameObject instead of leaking, and defaulting `dontDestroyOnLoad` to `false`. Only `SceneLoader` opts in (`dontDestroyOnLoad: true`), since it needs to survive the scene swap it's fading over

## [1.3.0] - 2026-27-07
### Extensions, Pooling & SceneLoader refactor
- Reorganized `Extensions` into focused files (`GameObjectExtensions`, `CollectionExtensions`, `TransformExtensions`, `VectorExtensions`, `ColorExtensions`, `CancellationTokenSourceExtensions`) and fixed the namespace (was `gishadev.tools.Core`, now `gishadev.tools.Extensions`)
- Added `GetRandomElement`, `IsNullOrEmpty`, `HasComponent`, `DestroyChildren`, `ResetLocal`, `WithAlpha`, `Renew`, and `Vector3`/`Vector2.With`
- `PoolManager<T>`: fixed a null-check ordering bug in `TryInstantiate`, removed dead reflection-based type switching in favor of the existing `PoolObjectsCollection`, simplified pool reset/activation logic
- Split fade logic out of `SceneLoader` into a standalone `ScreenFader` you can use on its own (optional color/sorting order); `ISceneLoader.AsyncSceneLoad(string)` replaced by `ISceneLoader.LoadScene(string, bool fade = true)` (now awaitable, fade can be skipped)
- Fixed a `NullReferenceException` in `DisableSFXOnComplete` when a pooled SFX has no clip assigned; `SFXEmitter`/`SFXPlayer`/`MusicPlayer` no longer clear an already-assigned clip when no pool clips are configured

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