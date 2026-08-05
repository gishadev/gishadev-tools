using System;

namespace gishadev.tools.Events
{
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes <paramref name="handler"/> to events of type <typeparamref name="T"/>.
        /// Returns a token that unsubscribes the handler when disposed — dispose it instead of
        /// calling <see cref="Unsubscribe{T}"/> by hand to avoid leaks. The explicit
        /// <see cref="Unsubscribe{T}"/> path remains supported.
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler) where T : IEvent;
        void Unsubscribe<T>(Action<T> handler) where T : IEvent;
        void Fire<T>(T gameEvent) where T : IEvent;
    }
}