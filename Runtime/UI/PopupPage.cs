using System;
using UnityEngine;

namespace gishadev.tools.UI
{
    public class PopupPage : MonoBehaviour
    {
        [field: SerializeField] public bool ExitOnNewPagePush { get; private set; }
        
        [field: SerializeField] public PageTransitionType EnterTransition { get; private set; }
        [field: SerializeField] public PageTransitionType ExitTransition { get; private set; }

        public event Action Changed;

        public MenuController MenuController { get; private set; }

        public bool IsTransitioning => _isInitialized && _transitionProcessor.IsTransitioning;

        private PageTransitionProcessor _transitionProcessor;

        private bool _isInitialized;

        protected virtual void OnDestroy()
        {
            if (_isInitialized)
                _transitionProcessor.StopCurrentTransition();

            if (MenuController != null)
                MenuController.ForgetPage(this);
        }

        internal void SetMenuController(MenuController menuController)
        {
            MenuController = menuController;
        }

        private void TryInitTransitions()
        {
            if (_isInitialized)
                return;

            _transitionProcessor = new PageTransitionProcessor(this);
            _isInitialized = true;
        }

        public virtual void Enter()
        {
            TryInitTransitions();

            _transitionProcessor.DoEnterTransition();
            Changed?.Invoke();
        }

        public virtual void Exit()
        {
            TryInitTransitions();

            _transitionProcessor.DoExitTransition();
            Changed?.Invoke();
        }

        /// <summary>
        /// Pops this page from the menu that pushed it, wherever it sits in the stack.
        /// </summary>
        public void Pop()
        {
            if (MenuController == null)
            {
                Debug.LogWarning($"{name} is not in any menu stack, nothing to pop.", this);
                return;
            }

            MenuController.TryPopPage(this);
        }
    }
}