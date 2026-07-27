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

        public ScreenFader(Color? color = null, int sortingOrder = 9999)
        {
            _root = new GameObject("[ScreenFader]");
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
            _image.color = _image.color.WithAlpha(0f);
            _image.gameObject.SetActive(true);

            await Tween.Alpha(_image, 1f, duration);
        }

        public async UniTask FadeOut(float duration = DefaultFadeDuration)
        {
            await Tween.Alpha(_image, 0f, duration);
            _image.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root);
        }
    }
}
