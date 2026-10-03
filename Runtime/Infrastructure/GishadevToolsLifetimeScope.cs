using gishadev.tools.Audio;
using gishadev.tools.Pooling;
using UnityEngine;
using VContainer;

namespace gishadev.tools.Infrastructure
{
    /// <summary>
    /// Quick-start scope: drop it in a scene to get every package service registered.
    /// Projects with their own scopes should install <see cref="GishadevToolsInstaller"/> instead.
    /// </summary>
#pragma warning disable CS0618 // AutoInjectLifetimeScope is obsolete; kept as the base here until 2.0.0.
    public class GishadevToolsLifetimeScope : AutoInjectLifetimeScope
#pragma warning restore CS0618
    {
        [SerializeField] private AudioMasterSO audioMasterSO;
        [SerializeField] private PoolDataSO poolDataSO;

        [Header("Modules")]
        [SerializeField] private bool registerEventBus = true;
        [SerializeField] private bool registerAudio = true;
        [SerializeField] private bool registerEmitters = true;
        [SerializeField] private bool registerSceneLoader = true;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            new GishadevToolsInstaller(audioMasterSO, poolDataSO)
            {
                RegisterEventBus = registerEventBus,
                RegisterAudio = registerAudio,
                RegisterEmitters = registerEmitters,
                RegisterSceneLoader = registerSceneLoader,
            }.Install(builder);
        }
    }
}
