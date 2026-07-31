using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace gishadev.tools.UI
{
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private PopupPage initialPopupPage;
        [SerializeField] private GameObject firstFocusItem;
        [SerializeField] private bool zeroPagesAllowed;
        [SerializeField] private bool popOnSamePagePush = true;

        // Used as a stack, but backed by a list so pages can be removed from any position.
        private readonly List<PopupPage> _pageStack = new();
        private Canvas _rootCanvas;

        public event Action StackChanged;

        public PopupPage CurrentPage => _pageStack.Count > 0 ? _pageStack[_pageStack.Count - 1] : null;
        public int PageCount => _pageStack.Count;
        public bool IsActive => _rootCanvas != null && _rootCanvas.enabled && _rootCanvas.gameObject.activeInHierarchy;

        protected virtual void Awake()
        {
            _rootCanvas = GetComponent<Canvas>();
        }

        protected virtual void Start()
        {
            if (firstFocusItem != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstFocusItem);

            if (initialPopupPage != null)
                PushPage(initialPopupPage);
        }

        protected virtual void OnDestroy()
        {
            foreach (PopupPage popupPage in _pageStack)
                if (popupPage != null && popupPage.MenuController == this)
                    popupPage.SetMenuController(null);

            _pageStack.Clear();
        }

        /// <summary>
        /// Input-agnostic back/cancel entry point. Call it from whatever input layer you use.
        /// </summary>
        public void Cancel()
        {
            if (IsActive)
                PopPage();
        }

        public bool IsPageInStack(PopupPage popupPage)
        {
            return popupPage != null && _pageStack.Contains(popupPage);
        }

        public bool IsPageOnTopOfStack(PopupPage popupPage)
        {
            return popupPage != null && popupPage == CurrentPage;
        }

        public virtual void PushPage(PopupPage popupPage)
        {
            if (popupPage == null)
                return;

            PopupPage currentPopupPage = CurrentPage;

            if (currentPopupPage != null)
            {
                if (currentPopupPage == popupPage && popOnSamePagePush)
                {
                    RemovePage(popupPage);
                    return;
                }

                if (currentPopupPage.ExitOnNewPagePush)
                    RemovePage(currentPopupPage);
            }

            if (popupPage.MenuController != null && popupPage.MenuController != this)
                popupPage.MenuController.ForgetPage(popupPage);

            // A page is only ever in the stack once, so re-pushing a buried page moves it to the top.
            _pageStack.Remove(popupPage);

            popupPage.SetMenuController(this);
            popupPage.Enter();
            _pageStack.Add(popupPage);

            StackChanged?.Invoke();
        }

        public void PopPage()
        {
            TryPopPage(CurrentPage);
        }

        // Kept void so UnityEvents can bind it.
        public void PopPage(PopupPage popupPage)
        {
            TryPopPage(popupPage);
        }

        /// <summary>
        /// Removes the page from any position in the stack and exits it. Returns false if the page
        /// is not in the stack, or if popping it would empty a stack that requires at least one page.
        /// </summary>
        public virtual bool TryPopPage(PopupPage popupPage)
        {
            if (!IsPageInStack(popupPage))
                return false;

            if (_pageStack.Count == 1 && !zeroPagesAllowed)
            {
                Debug.LogWarning("Trying to pop a page but only 1 page remains in the stack!", this);
                return false;
            }

            return RemovePage(popupPage);
        }

        public void PopAllPages()
        {
            int minPagesLeft = zeroPagesAllowed ? 0 : 1;

            while (_pageStack.Count > minPagesLeft)
                RemovePage(CurrentPage);
        }

        /// <summary>
        /// Drops a page without exiting it. Used when the page is destroyed or moves to another menu.
        /// </summary>
        internal void ForgetPage(PopupPage popupPage)
        {
            if (_pageStack.Remove(popupPage))
                StackChanged?.Invoke();
        }

        private bool RemovePage(PopupPage popupPage)
        {
            if (popupPage == null || !_pageStack.Remove(popupPage))
                return false;

            popupPage.Exit();
            StackChanged?.Invoke();
            return true;
        }
    }
}
