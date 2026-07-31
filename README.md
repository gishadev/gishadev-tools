# gishadev-tools

`com.gishadev.tools` — a small Unity toolkit to polish your game: audio, pooled effects, events, state machines, scene loading and a handful of general-purpose extensions.

Built for Unity 6, wired up with [VContainer](https://github.com/hadashiA/VContainer) for DI, [UniTask](https://github.com/Cysharp/UniTask) for async, and [PrimeTween](https://github.com/KyryloKuzyk/PrimeTween) for tweening.

## Install

Add via Package Manager → **Add package from git URL**:

```
https://github.com/gishadev/gishadev-tools.git
```

Or drop it in as a git submodule under `Assets/`.

## What's inside

- **Audio** — `AudioManager` for music/SFX playback with master/music/SFX volume mixing, fades, and auto-sequencing
- **Effects** — `SFXEmitter` / `VFXEmitter` / `OtherEmitter`: pooled prefab emitters, `EmitAt(index, position, rotation?)`
- **Pooling** — generic `PoolManager<T>` backing the emitters above; reuses inactive instances before instantiating new ones
- **Events** — `EventChannelSO` (Bool/Float/Int/String/Vector variants) for ScriptableObject-based decoupled messaging
- **StateMachine** — a lightweight state machine (`IState` + `StateMachine`)
- **Timers** — `Timer.After(2f, …)` / `Timer.Every(0.5f, …)`; pass the calling component to tie a timer to its lifetime, or cancel it via the returned handle
- **SceneLoading** — `SceneLoader` for async scene loads with an optional fade; `ScreenFader` is a standalone fade-overlay you can use on its own
- **UI** — `MenuController` drives a stack of `PopupPage`s (push, pop any page in the stack, pop all) with slide/fade/scale transitions; call `Cancel()` from your own input layer for back-button behaviour. Plus UI event broadcasters (button/slider/toggle/input-field → typed events)
- **WebGL** — a `Modern` build template (pick it in Player Settings → Resolution and Presentation): letterboxed 16:9 canvas, CSS loading bar, click-to-play overlay that unlocks browser audio, Open Graph tags for link previews (drop a 1200x630 `TemplateData/social-preview.png` in per project), and a real error message when a build fails to load instead of a stuck progress bar
- **Extensions** — small, general-purpose extensions (`GetOrAddComponent`, `GetRandomElement`, `DestroyChildren`, `WithAlpha`, `With(x,y,z)` for vectors, etc.)
- **Infrastructure** — `GishadevToolsLifetimeScope`, a VContainer lifetime scope wiring up the above
- **Editor tooling** — `AudioEditor`, `PoolEditor` and a code generator that turns your `PoolDataSO`/`AudioMasterSO` entries into strongly-typed enums, so you call `EmitAt(SFXPoolEnum.EXPLOSION, pos)` instead of passing raw indices (see [`unity-setup`](https://github.com/gishadev/unity-setup), which scaffolds all of this into a new project)

## Usage

```csharp
[Inject] private IAudioManager _audioManager;
[Inject] private ISFXEmitter _sfxEmitter;
[Inject] private ISceneLoader _sceneLoader;

_audioManager.PlayMusic(MusicAudioEnum.MUSIC_1);
_sfxEmitter.EmitAt(SFXPoolEnum.EXPLOSION, hitPoint); // rotation defaults to identity
await _sceneLoader.LoadScene("Level2"); // fades by default, pass fade: false to skip

Timer.After(2f, () => Debug.Log("done"), this);      // cancelled if this component is destroyed
var loop = Timer.Every(0.5f, Spawn, this);
loop.Cancel();
```

Register `GishadevToolsLifetimeScope` in your scene (or as a parent scope) to get everything above injected.

## License

MIT — see [LICENSE](LICENSE).
