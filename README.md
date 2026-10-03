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
- **Infrastructure** — `GishadevToolsInstaller`, a VContainer installer that registers the services above into any scope, plus `GishadevToolsLifetimeScope` as a drop-in quick start (see [Integration](#integration))
- **Editor tooling** — `AudioEditor`, `PoolEditor` and a code generator that turns your `PoolDataSO`/`AudioMasterSO` entries into strongly-typed enums, so you call `EmitAt(SFXPoolEnum.EXPLOSION, pos)` instead of passing raw indices (see [`unity-setup`](https://github.com/gishadev/unity-setup), which scaffolds all of this into a new project)

## Integration

The package's services are app-lifetime — the audio players and emitter pools live under `DontDestroyOnLoad` objects and the pools reset on every scene load — so register them once, in your root/project scope.

### Installer (recommended)

Install `GishadevToolsInstaller` in your own `LifetimeScope`. Turn off any module you register yourself or don't use:

```csharp
public class ProjectLifetimeScope : LifetimeScope
{
    [SerializeField] private AudioMasterSO audioMasterSO;
    [SerializeField] private PoolDataSO poolDataSO;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IEventBus, EventBus>(Lifetime.Singleton);
        new GishadevToolsInstaller(audioMasterSO, poolDataSO) { RegisterEventBus = false }.Install(builder);
    }
}

public class HoseService
{
    private readonly ISFXEmitter _sfx;
    public HoseService(ISFXEmitter sfx) => _sfx = sfx;
    // _sfx.EmitAt(SFXPoolEnum.WATER_SPLASH, hit.point);
}
```

| Flag | Registers | Needs |
|---|---|---|
| `RegisterEventBus` | `IEventBus` | — |
| `RegisterAudio` | `IAudioManager` | `AudioMasterSO` |
| `RegisterEmitters` | `ISFXEmitter`, `IVFXEmitter`, `IOtherEmitter` | `PoolDataSO`, plus an `IAudioManager` for the SFX emitter |
| `RegisterSceneLoader` | `ISceneLoader` | — |

All flags default to `true`. An enabled module with a null asset throws when the scope builds, naming the missing asset — pass `null` only for modules you've turned off. Nothing is injected by reflection: register your MonoBehaviours explicitly (`RegisterComponent`, `RegisterComponentInHierarchy`, …) and give them an `[Inject] public void Construct(...)` method.

### Quick start: `GishadevToolsLifetimeScope`

Put `GishadevToolsLifetimeScope` in your scene (or use it as a parent scope), assign the two assets, and untick any module you don't want. It runs the same installer, and also auto-injects every scene MonoBehaviour that has an `[Inject]` member.

That auto-inject base, `AutoInjectLifetimeScope`, is deprecated: it scans scenes by reflection, hides dependencies and misses objects spawned at runtime. It still works and will be removed in 2.0.0 — prefer the installer for new projects.

## Usage

```csharp
public class Player
{
    private readonly IAudioManager _audioManager;
    private readonly ISFXEmitter _sfxEmitter;
    private readonly ISceneLoader _sceneLoader;

    public Player(IAudioManager audioManager, ISFXEmitter sfxEmitter, ISceneLoader sceneLoader)
    {
        _audioManager = audioManager;
        _sfxEmitter = sfxEmitter;
        _sceneLoader = sceneLoader;
    }
}

_audioManager.PlayMusic(MusicAudioEnum.MUSIC_1);
_sfxEmitter.EmitAt(SFXPoolEnum.EXPLOSION, hitPoint); // rotation defaults to identity
await _sceneLoader.LoadScene("Level2"); // fades by default, pass fade: false to skip

Timer.After(2f, () => Debug.Log("done"), this);      // cancelled if this component is destroyed
var loop = Timer.Every(0.5f, Spawn, this);
loop.Cancel();
```

Writing your own pool on top of `PoolManager<T>`? Pass the `PoolDataSO` through its protected constructor: `public MyEmitter(PoolDataSO poolDataSO) : base(poolDataSO) { }`.

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
