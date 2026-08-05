using System;
using System.Collections.Generic;

namespace gishadev.tools.Events
{
    public class EventBus : IEventBus
    {
        // Dictionary to store handlers for each event type
        private readonly Dictionary<Type, List<Delegate>> _eventHandlers = new();

        public IDisposable Subscribe<T>(Action<T> handler) where T : IEvent
        {
            var eventType = typeof(T);
            if (!_eventHandlers.ContainsKey(eventType))
                _eventHandlers[eventType] = new List<Delegate>();

            _eventHandlers[eventType].Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            var eventType = typeof(T);
            if (!_eventHandlers.ContainsKey(eventType))
                return;

            _eventHandlers[eventType].Remove(handler);

            // Clean up empty lists
            if (_eventHandlers[eventType].Count == 0)
                _eventHandlers.Remove(eventType);
        }

        public void Fire<T>(T gameEvent) where T : IEvent
        {
            var eventType = typeof(T);
            if (!_eventHandlers.ContainsKey(eventType))
                return;

            // Create a copy to avoid issues if handlers subscribe/unsubscribe during event processing
            var handlers = new List<Delegate>(_eventHandlers[eventType]);

            foreach (var handler in handlers)
            {
                if (handler is Action<T> typedHandler)
                    typedHandler(gameEvent);
            }
        }

        /// <summary>
        /// Token returned by <see cref="Subscribe{T}"/>. Disposing unsubscribes once; further
        /// disposals are no-ops, so it is safe to dispose from both OnDisable and OnDestroy.
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            private Action _unsubscribe;

            public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

            public void Dispose()
            {
                _unsubscribe?.Invoke();
                _unsubscribe = null;
            }
        }
    }
}