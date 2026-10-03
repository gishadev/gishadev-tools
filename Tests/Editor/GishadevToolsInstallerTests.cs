using System;
using gishadev.tools.Audio;
using gishadev.tools.Effects;
using gishadev.tools.Events;
using gishadev.tools.Infrastructure;
using gishadev.tools.Pooling;
using gishadev.tools.SceneLoading;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace gishadev.tools.Tests
{
    // A plain ContainerBuilder has no entry-point dispatcher, so resolving here only constructs the
    // services - Initialize never runs and no GameObjects are created in edit mode.
    public class GishadevToolsInstallerTests
    {
        private AudioMasterSO _audioMasterSO;
        private PoolDataSO _poolDataSO;

        [SetUp]
        public void SetUp()
        {
            _audioMasterSO = ScriptableObject.CreateInstance<AudioMasterSO>();
            _poolDataSO = ScriptableObject.CreateInstance<PoolDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_audioMasterSO);
            UnityEngine.Object.DestroyImmediate(_poolDataSO);
        }

        [Test]
        public void Install_Defaults_ResolvesEveryService()
        {
            using var resolver = Build(new GishadevToolsInstaller(_audioMasterSO, _poolDataSO));

            Assert.IsInstanceOf<EventBus>(resolver.Resolve<IEventBus>());
            Assert.IsInstanceOf<AudioManager>(resolver.Resolve<IAudioManager>());
            Assert.IsInstanceOf<SFXEmitter>(resolver.Resolve<ISFXEmitter>());
            Assert.IsInstanceOf<VFXEmitter>(resolver.Resolve<IVFXEmitter>());
            Assert.IsInstanceOf<OtherEmitter>(resolver.Resolve<IOtherEmitter>());
            Assert.IsInstanceOf<SceneLoader>(resolver.Resolve<ISceneLoader>());
        }

        [Test]
        public void Install_Defaults_InjectsTheGivenAssets()
        {
            using var resolver = Build(new GishadevToolsInstaller(_audioMasterSO, _poolDataSO));

            Assert.AreSame(_audioMasterSO, ((AudioManager)resolver.Resolve<IAudioManager>()).AudioMasterData);
            Assert.AreSame(_audioMasterSO, resolver.Resolve<AudioMasterSO>());
            Assert.AreSame(_poolDataSO, resolver.Resolve<PoolDataSO>());
        }

        [Test]
        public void Install_Defaults_RegistersSingletons()
        {
            using var resolver = Build(new GishadevToolsInstaller(_audioMasterSO, _poolDataSO));

            Assert.AreSame(resolver.Resolve<IAudioManager>(), resolver.Resolve<IAudioManager>());
            Assert.AreSame(resolver.Resolve<ISFXEmitter>(), resolver.Resolve<ISFXEmitter>());
        }

        [Test]
        public void Install_WithoutEventBus_LeavesIEventBusUnregistered()
        {
            using var resolver = Build(new GishadevToolsInstaller(_audioMasterSO, _poolDataSO)
            {
                RegisterEventBus = false
            });

            Assert.Throws<VContainerException>(() => resolver.Resolve<IEventBus>());
            Assert.IsNotNull(resolver.Resolve<IAudioManager>());
        }

        [Test]
        public void Install_WithProjectEventBus_ResolvesTheProjectOne()
        {
            var builder = new ContainerBuilder();
            builder.Register<IEventBus, EventBus>(Lifetime.Singleton);
            new GishadevToolsInstaller(_audioMasterSO, _poolDataSO) { RegisterEventBus = false }.Install(builder);

            using var resolver = builder.Build();

            Assert.IsNotNull(resolver.Resolve<IEventBus>());
        }

        [Test]
        public void Install_AudioEnabledWithNullAudioMasterSO_ThrowsNamingTheFix()
        {
            var installer = new GishadevToolsInstaller(null, _poolDataSO);

            var ex = Assert.Throws<InvalidOperationException>(() => installer.Install(new ContainerBuilder()));
            StringAssert.Contains(nameof(AudioMasterSO), ex.Message);
            StringAssert.Contains(nameof(GishadevToolsInstaller.RegisterAudio), ex.Message);
        }

        [Test]
        public void Install_EmittersEnabledWithNullPoolDataSO_ThrowsNamingTheFix()
        {
            var installer = new GishadevToolsInstaller(_audioMasterSO, null);

            var ex = Assert.Throws<InvalidOperationException>(() => installer.Install(new ContainerBuilder()));
            StringAssert.Contains(nameof(PoolDataSO), ex.Message);
            StringAssert.Contains(nameof(GishadevToolsInstaller.RegisterEmitters), ex.Message);
        }

        [Test]
        public void Install_DisabledModules_AcceptNullAssets()
        {
            using var resolver = Build(new GishadevToolsInstaller(null, null)
            {
                RegisterAudio = false,
                RegisterEmitters = false
            });

            Assert.IsNotNull(resolver.Resolve<ISceneLoader>());
            Assert.Throws<VContainerException>(() => resolver.Resolve<IAudioManager>());
            Assert.Throws<VContainerException>(() => resolver.Resolve<ISFXEmitter>());
        }

        private static IObjectResolver Build(GishadevToolsInstaller installer)
        {
            var builder = new ContainerBuilder();
            installer.Install(builder);
            return builder.Build();
        }
    }
}
