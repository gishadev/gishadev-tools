using gishadev.tools.Extensions;
using PrimeTween;
using UnityEngine;

namespace gishadev.tools.UI
{
    public class PageTransitionProcessor
    {
        private readonly PopupPage _popupPage;

        private readonly float _slideDuration = 1f;
        private readonly float _fadeDuration = 0.6f;
        private readonly float _scaleDuration = 0.5f;

        private readonly RectTransform _rectTransform;
        private readonly CanvasGroup _canvasGroup;

        private Sequence _currentSequence;
        private bool _raycastsBlocked;
        private bool _raycastsBlockedValue;

        public bool IsTransitioning => _currentSequence.isAlive;

        public PageTransitionProcessor(PopupPage popupPage)
        {
            _popupPage = popupPage;

            _rectTransform = popupPage.GetComponent<RectTransform>();
            _canvasGroup = popupPage.gameObject.GetOrAddComponent<CanvasGroup>();
        }

        public void DoEnterTransition()
        {
            StopCurrentTransition();

            _popupPage.gameObject.SetActive(true);
            ResetVisualState();

            switch (_popupPage.EnterTransition)
            {
                case PageTransitionType.None:
                default:
                    return;

                case PageTransitionType.SideSlide:
                    _currentSequence = SideSlideTransitionEffect(-Screen.width, 0f, _slideDuration, Ease.OutElastic);
                    break;

                case PageTransitionType.VerticalSlide:
                    _currentSequence = VerticalSlideTransitionEffect(Screen.height, 0f, _slideDuration, Ease.OutElastic);
                    break;

                case PageTransitionType.Fade:
                    _currentSequence = FadeTransitionEffect(0f, 1f, _fadeDuration);
                    break;

                case PageTransitionType.Scale:
                    _currentSequence = ScaleTransitionEffect(0f, 1f, _scaleDuration, Ease.OutElastic);
                    break;
            }
        }

        public void DoExitTransition()
        {
            StopCurrentTransition();

            if (_popupPage.ExitTransition == PageTransitionType.None)
            {
                FinishExit();
                return;
            }

            // The page is leaving, so it must stop taking clicks right away.
            BlockRaycasts();

            switch (_popupPage.ExitTransition)
            {
                case PageTransitionType.SideSlide:
                    _currentSequence = SideSlideTransitionEffect(0f, Screen.width, _slideDuration, Ease.InOutQuint);
                    break;

                case PageTransitionType.VerticalSlide:
                    _currentSequence = VerticalSlideTransitionEffect(0f, -Screen.height, _slideDuration, Ease.InOutQuint);
                    break;

                case PageTransitionType.Fade:
                    _currentSequence = FadeTransitionEffect(1f, 0f, _fadeDuration, Ease.OutSine);
                    break;

                case PageTransitionType.Scale:
                    _currentSequence = ScaleTransitionEffect(1f, 0f, _scaleDuration, Ease.InOutQuint);
                    break;
            }

            _currentSequence.OnComplete(this, processor => processor.FinishExit(), warnIfTargetDestroyed: false);
        }

        /// <summary>
        /// Kills the running transition without firing its callbacks, so a stale exit can never
        /// deactivate a page that has since been re-entered.
        /// </summary>
        public void StopCurrentTransition()
        {
            if (_currentSequence.isAlive)
                _currentSequence.Stop();

            _currentSequence = default;
            RestoreRaycasts();
        }

        private void FinishExit()
        {
            _currentSequence = default;

            if (_popupPage == null)
                return;

            RestoreRaycasts();

            _popupPage.gameObject.SetActive(false);
            ResetVisualState();
        }

        private void ResetVisualState()
        {
            _canvasGroup.alpha = 1f;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
        }

        private void BlockRaycasts()
        {
            if (_raycastsBlocked)
                return;

            _raycastsBlockedValue = _canvasGroup.blocksRaycasts;
            _canvasGroup.blocksRaycasts = false;
            _raycastsBlocked = true;
        }

        private void RestoreRaycasts()
        {
            if (!_raycastsBlocked)
                return;

            _canvasGroup.blocksRaycasts = _raycastsBlockedValue;
            _raycastsBlocked = false;
        }

        #region Transition Effects

        private Sequence SideSlideTransitionEffect(float startValue, float endValue, float duration,
            Ease ease = Ease.InSine)
        {
            _rectTransform.anchoredPosition = Vector2.right * startValue;

            var seq = Sequence.Create(sequenceEase: ease, useUnscaledTime: true);
            seq.Chain(Tween.UIAnchoredPosition(_rectTransform, Vector2.right * endValue, duration));

            return seq;
        }

        private Sequence VerticalSlideTransitionEffect(float startValue, float endValue, float duration,
            Ease ease = Ease.InSine)
        {
            _rectTransform.anchoredPosition = Vector2.up * startValue;

            var seq = Sequence.Create(sequenceEase: ease, useUnscaledTime: true);
            seq.Chain(Tween.UIAnchoredPosition(_rectTransform, Vector2.up * endValue, duration));

            return seq;
        }

        private Sequence FadeTransitionEffect(float startValue, float endValue, float duration,
            Ease ease = Ease.InSine)
        {
            _canvasGroup.alpha = startValue;

            var seq = Sequence.Create(sequenceEase: ease, useUnscaledTime: true);
            seq.Chain(Tween.Alpha(_canvasGroup, endValue, duration));

            return seq;
        }

        private Sequence ScaleTransitionEffect(float startValue, float endValue, float duration,
            Ease ease = Ease.InSine)
        {
            _rectTransform.localScale = Vector3.one * startValue;

            var seq = Sequence.Create(sequenceEase: ease, useUnscaledTime: true);
            seq.Chain(Tween.Scale(_rectTransform.transform, endValue, duration));

            return seq;
        }

        #endregion
    }
}
