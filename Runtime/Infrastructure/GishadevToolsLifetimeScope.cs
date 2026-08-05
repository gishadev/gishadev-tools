using gishadev.tools.Events;
using gishadev.tools.Audio;
using gishadev.tools.Effects;
using gishadev.tools.Pooling;
using gishadev.tools.SceneLoading;
using UnityEngine;
using VContainer;

namespace gishadev.tools.Infrastructure
{
    public class GishadevToolsLifetimeScope : AutoInjectLifetimeScope
    {
        [SerializeField] private AudioMasterSO audioMasterSO;
        [SerializeField] private PoolDataSO poolDataSO;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterInstance(audioMasterSO);
            builder.RegisterInstance(poolDataSO);

            builder.Register<IEventBus>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<AudioManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SFXEmitter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<VFXEmitter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<OtherEmitter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneLoader>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}