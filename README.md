# gishadev-tools

`com.gishadev.tools` — a small Unity toolkit to polish your game: audio, pooled effects, events, state machines, scene loading, saving and a handful of general-purpose extensions.

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
- **SavingSystem** — `LocalPrefs` is a `PlayerPrefs` replacement that also stores vectors and writes to a file you can move, back up or delete. Create `LocalPrefsStore`s directly for save slots; each one owns its own file. Saves are written atomically with a one-generation backup, so a crash mid-write can't corrupt them, and pass an `AesEncryptor` to keep them out of a text editor. `ISaverSystem` (`FileSaverSystem` / `PlayerPrefsSaverSystem` / `WebSaverSystem`) is the blob-level interface to inject when a system wants to persist its own JSON
- **UI** — `MenuController` drives a stack of `PopupPage`s (push, pop any page in the stack, pop all) with slide/fade/scale transitions; call `Cancel()` from your own input layer for back-button behaviour. Plus UI event broadcasters (button/slider/toggle/input-field → typed events)
- **WebGL** — a `Modern` build template (pick it in Player Settings → Resolution and Presentation): letterboxed 16:9 canvas, CSS loading bar, click-to-play overlay that unlocks browser audio, Open Graph tags for link previews, and a real error message when a build fails to load instead of a stuck progress bar. For a link-preview image, put a 1200x630 `TemplateData/social-preview.png` into the built output before uploading — when the package is installed under `Packages/` it's immutable, so copy the template into `Assets/WebGLTemplates` first if you want to customise it per project
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

### Saving

```csharp
LocalPrefs.SetInt("highscore", 42);
LocalPrefs.SetVector3("checkpoint", transform.position);
LocalPrefs.Save();

int best = LocalPrefs.GetInt("highscore");        // 0 if never set — reading never creates the key
if (LocalPrefs.TryGet("checkpoint", out Vector3 spawn)) { … }
```

Save slots are separate stores, each with its own file. Pass an encryptor to make one unreadable in a text editor:

```csharp
using var slot = new LocalPrefsStore("slot1", new AesEncryptor());
slot.Set("level", 3);
slot.Save();
```

`AesEncryptor` is obfuscation, not security — the passphrase ships inside the build. It stops casual save editing and detects corruption; it won't stop someone determined to read their own save. Pass your own passphrase to avoid sharing one with every other project using this package.

To persist a system's own JSON rather than individual values, inject an `ISaverSystem`. On WebGL, `WebSaverSystem` writes to localStorage and falls back to whatever saver you hand it everywhere else — the JavaScript side is bundled in the package, so unlike the WebGL template there's nothing to copy per project:

```csharp
ISaverSystem saver = new WebSaverSystem(new FileSaverSystem("save"));
saver.Save("player", JsonUtility.ToJson(playerData));
if (saver.TryLoad("player", out string json)) { … }   // Load returns null when unset
```

## License

MIT — see [LICENSE](LICENSE).
