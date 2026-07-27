using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace gishadev.tools.SceneLoading
{
    public class SceneLoader : ISceneLoader, IInitializable, IDisposable
    {
        private ScreenFader _fader;
        private bool _isLoadingScene;

        public void Initialize()
        {
            _fader = new ScreenFader();
        }

        public void Dispose()
        {
            _fader?.Dispose();
        }

        public async UniTask LoadScene(string sceneToLoad, bool fade = true)
        {
            if (_isLoadingScene)
                return;

            _isLoadingScene = true;

            if (fade)
                await _fader.FadeIn();

            var loadOperation = SceneManager.LoadSceneAsync(sceneToLoad);
            while (loadOperation != null && !loadOperation.isDone)
                await UniTask.Yield();

            if (fade)
                await _fader.FadeOut();

            _isLoadingScene = false;
        }
    }
}
