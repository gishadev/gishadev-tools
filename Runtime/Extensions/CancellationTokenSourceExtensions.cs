using System.Threading;

namespace gishadev.tools.Extensions
{
    public static class CancellationTokenSourceExtensions
    {
        // Cancels the current source (if any) and returns a fresh one to replace it with.
        public static CancellationTokenSource Renew(this CancellationTokenSource cts)
        {
            cts?.Cancel();
            return new CancellationTokenSource();
        }
    }
}
