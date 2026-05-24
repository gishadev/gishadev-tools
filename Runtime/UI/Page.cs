using System;
using UnityEngine;

namespace gishadev.tools.UI
{
    public class Page : MonoBehaviour
    {
        [field: SerializeField] public bool ExitOnNewPagePush { get; private set; }
        
        [field: SerializeField] public PageTransitionType EnterTransition { get; private set; }
        [field: SerializeField] public PageTransitionType ExitTransition { get; private set; }

        public event Action Changed;

        private PageTransitionProcessor _transitionProcessor;

        private bool _isInitialized;

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
    }
}