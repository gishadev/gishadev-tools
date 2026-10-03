using System;
using gishadev.tools.Audio;
using gishadev.tools.Effects;
using gishadev.tools.Events;
using gishadev.tools.Pooling;
using gishadev.tools.SceneLoading;
using VContainer;
using VContainer.Unity;

namespace gishadev.tools.Infrastructure
{
    /// <summary>
    /// Registers the package's services into any <see cref="IContainerBuilder"/>.
    /// Install it in your app-lifetime (root/project) scope: the audio parent and emitter pools are
    /// <c>DontDestroyOnLoad</c> and pools reset on every scene load, so these are app-lifetime services.
    /// <code>
    /// new GishadevToolsInstaller(audioMasterSO, poolDataSO) { RegisterEventBus = false }.Install(builder);
    /// </code>
    /// </summary>
    public class GishadevToolsInstaller : IInstaller
    {
        private readonly AudioMasterSO _audioMasterSO;
        private readonly PoolDataSO _poolDataSO;

        /// <summary>Registers <see cref="EventBus"/> as <see cref="IEventBus"/>. Turn off when the project registers its own.</summary>
        public bool RegisterEventBus { get; set; } = true;

        /// <summary>Registers <see cref="AudioManager"/> as <see cref="IAudioManager"/>. Requires an <see cref="AudioMasterSO"/>.</summary>
        public bool RegisterAudio { get; set; } = true;

        /// <summary>
        /// Registers <see cref="SFXEmitter"/>, <see cref="VFXEmitter"/> and <see cref="OtherEmitter"/>.
        /// Requires a <see cref="PoolDataSO"/>; <see cref="SFXEmitter"/> also needs an <see cref="IAudioManager"/>
        /// in the container, from <see cref="RegisterAudio"/> or your own registration.
        /// </summary>
        public bool RegisterEmitters { get; set; } = true;

        /// <summary>Registers <see cref="SceneLoader"/> as <see cref="ISceneLoader"/>.</summary>
        public bool RegisterSceneLoader { get; set; } = true;

        public GishadevToolsInstaller(AudioMasterSO audioMasterSO, PoolDataSO poolDataSO)
        {
            _audioMasterSO = audioMasterSO;
            _poolDataSO = poolDataSO;
        }

        public void Install(IContainerBuilder builder)
        {
            Validate();

            if (RegisterEventBus)
                builder.Register<EventBus>(Lifetime.Singleton).AsImplementedInterfaces();

            if (RegisterAudio)
            {
                builder.RegisterInstance(_audioMasterSO);
                builder.Register<AudioManager>(Lifetime.Singleton).AsImplementedInterfaces();
            }

            if (RegisterEmitters)
            {
                builder.RegisterInstance(_poolDataSO);
                builder.Register<SFXEmitter>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<VFXEmitter>(Lifetime.Singleton).AsImplementedInterfaces();
                builder.Register<OtherEmitter>(Lifetime.Singleton).AsImplementedInterfaces();
            }

            if (RegisterSceneLoader)
                builder.Register<SceneLoader>(Lifetime.Singleton).AsImplementedInterfaces();
        }

        // Fails at build time with a message naming the fix, rather than a NullReferenceException
        // from RegisterInstance or an error from Initialize once the game is already running.
        private void Validate()
        {
            if (RegisterAudio && _audioMasterSO == null)
                throw new InvalidOperationException(
                    $"{nameof(GishadevToolsInstaller)}: audio is enabled but no {nameof(AudioMasterSO)} was given. " +
                    $"Pass one to the constructor, or set {nameof(RegisterAudio)} = false.");

            if (RegisterEmitters && _poolDataSO == null)
                throw new InvalidOperationException(
                    $"{nameof(GishadevToolsInstaller)}: emitters are enabled but no {nameof(PoolDataSO)} was given. " +
                    $"Pass one to the constructor, or set {nameof(RegisterEmitters)} = false.");
        }
    }
}
