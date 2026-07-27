using System;
using Cysharp.Threading.Tasks;
using gishadev.tools.Extensions;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace gishadev.tools.SceneLoading
{
    // A full-screen color overlay you can fade in/out on demand. Not tied to scene loading -
    // create your own instance anywhere you need a fade (loading screens, cutscenes, transitions, ...).
    public class ScreenFader : IDisposable
    {
        private const float DefaultFadeDuration = .5f;

        private readonly GameObject _root;
        private readonly Image _image;

        // dontDestroyOnLoad defaults to false: a fader created ad hoc dies with its scene like any
        // other GameObject, so nothing leaks if you forget to Dispose it. Only opt in when you need
        // the same fader to survive a scene load - e.g. SceneLoader, which stays visible while the
        // new scene loads underneath it.
        public ScreenFader(Color? color = null, int sortingOrder = 9999, bool dontDestroyOnLoad = false)
        {
            _root = new GameObject("[ScreenFader]");
            if (dontDestroyOnLoad)
                Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            _root.AddComponent<GraphicRaycaster>();

            var overlayObject = new GameObject("Overlay");
            overlayObject.transform.SetParent(_root.transform);

            _image = overlayObject.AddComponent<Image>();
            _image.color = (color ?? Color.black).WithAlpha(0f);
            overlayObject.SetActive(false);

            var rt = overlayObject.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        public async UniTask FadeIn(float duration = DefaultFadeDuration)
        {
            if (_image == null)
                return;

            _image.color = _image.color.WithAlpha(0f);
            _image.gameObject.SetActive(true);

            await Tween.Alpha(_image, 1f, duration);
        }

        public async UniTask FadeOut(float duration = DefaultFadeDuration)
        {
            if (_image == null)
                return;

            await Tween.Alpha(_image, 0f, duration);

            // The overlay may have been destroyed (scene unload, Dispose) while this was suspended.
            if (_image == null)
                return;

            _image.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
        }
    }
}
