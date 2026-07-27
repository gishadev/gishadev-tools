using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace gishadev.tools.Timers
{
    // Fire-and-forget delays on top of UniTask. Every timer returns a handle you can cancel, and
    // the overloads taking an owner tie the timer to that object's lifetime - so a callback can
    // never run against a destroyed GameObject, which is the usual way delayed code blows up.
    public static class Timer
    {
        // Runs the action once, after delay seconds.
        public static TimerHandle After(float delay, Action action, bool ignoreTimeScale = false)
            => Run(delay, action, false, ignoreTimeScale, CancellationToken.None);

        public static TimerHandle After(float delay, Action action, Component owner, bool ignoreTimeScale = false)
            => Run(delay, action, false, ignoreTimeScale, owner.GetCancellationTokenOnDestroy());

        // Runs the action every interval seconds until the handle is cancelled (or the owner dies).
        // The first call happens after the first interval, not immediately.
        public static TimerHandle Every(float interval, Action action, bool ignoreTimeScale = false)
            => Run(interval, action, true, ignoreTimeScale, CancellationToken.None);

        public static TimerHandle Every(float interval, Action action, Component owner, bool ignoreTimeScale = false)
            => Run(interval, action, true, ignoreTimeScale, owner.GetCancellationTokenOnDestroy());

        private static TimerHandle Run(float seconds, Action action, bool repeat, bool ignoreTimeScale,
            CancellationToken ownerToken)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (seconds < 0f)
            {
                Debug.LogError($"Timer: interval must be >= 0, got {seconds}.");
                seconds = 0f;
            }

            var handle = new TimerHandle(ownerToken);
            Tick(seconds, action, repeat, ignoreTimeScale, handle).Forget();

            return handle;
        }

        private static async UniTaskVoid Tick(float seconds, Action action, bool repeat, bool ignoreTimeScale,
            TimerHandle handle)
        {
            var delayType = ignoreTimeScale ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
            var delay = TimeSpan.FromSeconds(seconds);

            try
            {
                do
                {
                    var cancelled = await UniTask.Delay(delay, delayType, cancellationToken: handle.Token)
                        .SuppressCancellationThrow();

                    if (cancelled)
                        return;

                    action();
                } while (repeat);
            }
            finally
            {
                handle.Complete();
            }
        }
    }

    public class TimerHandle
    {
        private readonly CancellationTokenSource _cts;

        internal TimerHandle(CancellationToken ownerToken)
        {
            // Linked so cancelling the owner (e.g. destroying its GameObject) stops this timer too.
            _cts = ownerToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(ownerToken)
                : new CancellationTokenSource();
        }

        internal CancellationToken Token => _cts.Token;

        public bool IsRunning { get; private set; } = true;

        public void Cancel()
        {
            if (!IsRunning)
                return;

            _cts.Cancel();
        }

        internal void Complete()
        {
            IsRunning = false;
            _cts.Dispose();
        }
    }
}
