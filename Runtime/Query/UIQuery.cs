using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using emiteat.NexUI.State;

namespace emiteat.NexUI.Query
{
    /// <summary>
    /// A single asynchronous data query with caching, retry, empty-detection and an
    /// observable <see cref="State"/> signal. Depends only on Abstractions + State, so the
    /// whole Query module compiles and runs without Core.
    ///
    /// Note: continuations after a retry delay may resume off the main thread. When wiring
    /// State/UI boundaries in a Unity context, marshal back to the main thread as needed.
    /// </summary>
    public sealed class UIQuery<T>
    {
        private readonly QueryKey _key;
        private readonly Func<CancellationToken, UniTask<T>> _fetch;
        private readonly QueryCache _cache;
        private readonly RetryPolicy _retry;
        private readonly Func<T, bool> _isEmpty;

        public UISignal<QueryState<T>> State { get; } = new UISignal<QueryState<T>>(QueryState<T>.Idle());

        public UIQuery(
            QueryKey key,
            Func<CancellationToken, UniTask<T>> fetch,
            QueryCache cache = null,
            RetryPolicy retry = null,
            Func<T, bool> isEmpty = null)
        {
            _key = key;
            _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
            _cache = cache;
            _retry = retry ?? RetryPolicy.Default;
            _isEmpty = isEmpty ?? DefaultIsEmpty;
        }

        public async UniTask RunAsync(CancellationToken ct = default, bool allowCache = true)
        {
            if (allowCache && _cache != null &&
                _cache.TryGet<T>(_key, out var cached, out var stale) && !stale)
            {
                Publish(cached);
                return;
            }

            State.Value = QueryState<T>.Loading();

            int attempt = 0;
            while (true)
            {
                try
                {
                    var data = await _fetch(ct);
                    _cache?.Set(_key, data);
                    Publish(data);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_retry.ShouldRetry(attempt))
                    {
                        await UniTask.Delay(_retry.DelayFor(attempt), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
                        attempt++;
                        continue;
                    }
                    State.Value = QueryState<T>.Failure(ex.Message);
                    return;
                }
            }
        }

        private void Publish(T data)
        {
            State.Value = _isEmpty(data) ? QueryState<T>.EmptyResult() : QueryState<T>.Success(data);
        }

        private static bool DefaultIsEmpty(T data)
        {
            if (data == null) return true;
            if (data is System.Collections.ICollection c) return c.Count == 0;
            return false;
        }
    }
}
