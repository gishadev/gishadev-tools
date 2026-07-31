## [1.7.0] - 2026-31-07
### Fixed
- `MenuController.PopPage(PopupPage)` called `Stack<T>.Pop(page)`, which doesn't exist — the UI namespace didn't compile. The page stack is now backed by a `List<PopupPage>` so a page can be removed from any position, not just the top
- `PopPage()` popped the stack itself and then passed the page to `PopPage(PopupPage)`, which popped again — two pages disappeared per call
- `PopAllPages` incremented its index while `Count` shrank, so it stopped halfway and left roughly half the stack open
- Clicking during a transition could softlock the menu: the exit sequence's `OnComplete` unconditionally deactivated its page, so a page re-entered while its old exit was still running got deactivated by that stale callback — stack says a page is open, nothing on screen. Transitions now cancel the running sequence before starting a new one (PrimeTween's `Stop()` skips callbacks), and the enter path restores alpha/position/scale itself instead of relying on the exit completing
- An exiting page kept receiving clicks for the whole transition. `blocksRaycasts` is now off while a page exits and restored to its previous value afterwards
- Pages destroyed while still in the stack left dangling entries; `PopupPage.OnDestroy` now tells its menu to drop it, without running exit transitions on a dead object
- `ExitOnNewPagePush` was ignored on a single-page stack — the old page stayed visible under the new one, because the push routed through the guard that keeps the stack non-empty
- Pushing the same page twice ran `Enter()` and `Exit()` back to back on it, firing both transitions and two `Changed` events
- Slide transitions set their start value on `localPosition` but animated `anchoredPosition`; on pages with stretched anchors the start was effectively ignored. Both now use `anchoredPosition`
- Null/`EventSystem.current` guards in `PushPage`, `IsPageInStack`, `IsPageOnTopOfStack` and `Start`

### Changed
- `MenuController.OnCancel` was dead code — Unity delivers that message only to the selected GameObject, never to the controller. Replaced by a public `Cancel()`, which you call from whatever input layer you use; the package stays input-system agnostic
- `PopupPage.Pop()` no longer does `FindAnyObjectByType<MenuController>()`. Each page holds the `MenuController` that pushed it, so `Pop()` removes *that* page from *its* menu instead of blind-popping the top of an arbitrary controller
- `PopPage(PopupPage)` stays `void` (UnityEvents only bind void methods); the `bool`-returning `TryPopPage(PopupPage)` is the new virtual override point. Subclasses overriding `PopPage(PopupPage)` need to move to `TryPopPage`
- A page is only ever in the stack once — re-pushing a buried page moves it to the top

### Added
- `MenuController.CurrentPage`, `PageCount`, `IsActive` and a `StackChanged` event, so UI can react to the stack without polling
- `PopupPage.MenuController` and `PopupPage.IsTransitioning`

### Modern WebGL template
- Removed the "WebGL builds are not supported on mobile devices" banner inherited from Unity's stock template — it isn't true, and every phone player was seeing it over the game
- Mobile touch fixes: `touch-action: none` (the browser was eating drags as scroll/pinch-zoom), `user-select: none`, no tap highlight, `overflow: hidden`, and a static viewport meta with `viewport-fit=cover` instead of one injected by user-agent sniffing
- A failed loader used to hang on a frozen progress bar with nothing in the console. `script.onerror` now names the missing file and points at the two usual causes — Build folder not uploaded, or the server not sending `Content-Encoding` for `.br`/`.gz`. Load failures render in the page banner instead of a blocking `alert()`
- Added a click-to-play overlay after loading, so the audio context is unlocked by a real user gesture instead of the game appearing to have broken sound (mainly iOS Safari). Focus moves to the canvas on dismiss, so keyboard input works immediately
- Added Open Graph / Twitter card tags so shared links get a preview instead of a bare URL. Drop a 1200x630 `TemplateData/social-preview.png` in per project
- Fullscreen: the icon now updates on Safari (`webkit`/`MS` change events were never listened for), `isFullscreen()` returns an actual boolean, the icon swaps via a CSS class instead of JS writing inline `background-image` with a different relative path than the stylesheet, and unsupported-fullscreen shows a banner instead of `alert()`
- The loading screen's `logo.png` is a 1356x90 "play with headphones" banner, but `background-size: contain` squeezed it into a 200x200 box — it rendered 200x13 with 187px of the box left empty. The box is now 420x40, so the banner reads at 420x28 above the progress bar
- Added `headphones-icon.png` (the glyph cropped out of that banner) as a 48x48 icon in the bottom-left corner, mirroring the fullscreen button's footprint and 0.333 opacity. It links to https://www.youtube.com/watch?v=dQw4w9WgXcQ in a new tab (`rel="noopener noreferrer"`)
- Progress bar is CSS instead of two PNGs (deleted `progress-bar-empty/full.png`), with a percentage readout and `role="progressbar"`. Loading/warning colours moved to custom properties for per-project reskinning
- Removed dead CSS (`#unity-footer`, duplicate `.unity-desktop`, `font-style: regular`), the redundant `Content-Type` meta and the inline `onclick`; canvas got fallback text and `tabindex`; the fullscreen button got an `aria-label`. The commented-out `beforeunload` snippet now uses `preventDefault()` + `returnValue` — the old `confirm()` version silently did nothing in modern browsers

## [1.6.0] - 2026-27-07
### Added
- `Timer` (`gishadev.tools.Timers`) — UniTask-backed delays: `Timer.After(2f, action)` and `Timer.Every(0.5f, action)`, both returning a `TimerHandle` you can `Cancel()`. Overloads taking the calling `Component` link the timer to its lifetime, so callbacks stop when the object is destroyed instead of running against a dead GameObject. `ignoreTimeScale: true` keeps a timer running while the game is paused. Kept in its own namespace so importing `gishadev.tools.Core` doesn't pull a type called `Timer` into scope

## [1.5.1] - 2026-27-07
### Added
- Declared the registry-resolvable dependencies in `package.json`: `com.unity.ugui` (UI + TextMeshPro) and the `audio`/`ui` built-in modules, so they resolve on install instead of being assumed. UniTask, VContainer and PrimeTween can't be declared this way — UPM's `dependencies` field only resolves registry packages, not the Git URLs / local tarball they're installed from

## [1.5.0] - 2026-27-07
### Changed
- `GeneratedExtensionMethods.cs` is now regenerated by `CodeGenerator` alongside the enums (same GENERATE ENUMS button) rather than written once during project setup. It's emitted only for enums that actually exist, so a project without a `PoolDataSO` no longer gets pool overloads referencing enums that were never generated. This keeps the typed `EmitAt(SFXPoolEnum.X, …)` API — passing the wrong enum to an emitter is a compile error again — while removing the second writer and the path mismatch that let the file go stale
- `PoolManager<T>.PoolObjectsCollection` is now `IReadOnlyList<T>`; emitters return the `PoolDataSO` array directly
- Renamed assemblies `polish-tools`/`polish-tools.Editor` → `gishadev.tools`/`gishadev.tools.Editor`, and moved `DisableSFXOnComplete` from the legacy `Gisha.Effects.Audio` namespace to `gishadev.tools.Effects`

### Fixed
- Removed per-emit allocations from the hot path: `PoolObjectsCollection` no longer rebuilds a `List` on every access, pooled-object reuse no longer allocates via LINQ, `SFXEmitter` tracks emitted sources in a `HashSet` instead of an O(n) `List.Contains`, and `AudioManager.GetAudioCollection` no longer re-`Cast`/`ToArray`s the collection on every play and volume change (`PlayAudio` was allocating it twice)
- `MusicPlayer.InitPlay` and `AudioManager.DelayFunc` are `UniTaskVoid` instead of `async void`, so exceptions surface instead of being swallowed
- Removed the `Debug.Log` that fired on every single audio play
- Actionable errors instead of crashes/silence for unconfigured data: missing `PoolDataSO`/`AudioMasterSO`, out-of-range pool and audio indices, pool entries with no prefab, and audio entries with no clips. Music auto-sequencing no longer throws when the playing clip isn't part of the collection

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