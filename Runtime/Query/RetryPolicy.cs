using System;

namespace emiteat.NexUI.Query
{
    /// <summary>Retry configuration with optional exponential backoff.</summary>
    public sealed class RetryPolicy
    {
        public int MaxRetries { get; }
        public float BaseDelaySeconds { get; }
        public bool ExponentialBackoff { get; }

        public RetryPolicy(int maxRetries = 2, float baseDelaySeconds = 0.5f, bool exponentialBackoff = true)
        {
            MaxRetries = Mathf(maxRetries);
            BaseDelaySeconds = Math.Max(0f, baseDelaySeconds);
            ExponentialBackoff = exponentialBackoff;
        }

        public bool ShouldRetry(int attempt) => attempt < MaxRetries;

        public TimeSpan DelayFor(int attempt)
        {
            float seconds = ExponentialBackoff
                ? BaseDelaySeconds * (float)Math.Pow(2, attempt)
                : BaseDelaySeconds;
            return TimeSpan.FromSeconds(seconds);
        }

        private static int Mathf(int v) => v < 0 ? 0 : v;

        public static RetryPolicy None => new RetryPolicy(0, 0f, false);
        public static RetryPolicy Default => new RetryPolicy();
    }
}
