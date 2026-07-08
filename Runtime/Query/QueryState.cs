namespace emiteat.NexUI.Query
{
    public enum QueryStatus
    {
        Idle = 0,
        Loading = 1,
        Success = 2,
        Empty = 3,
        Error = 4
    }

    /// <summary>Immutable snapshot of a query's current status, data and error.</summary>
    public readonly struct QueryState<T>
    {
        public QueryStatus Status { get; }
        public T Data { get; }
        public string Error { get; }

        private QueryState(QueryStatus status, T data, string error)
        {
            Status = status;
            Data = data;
            Error = error;
        }

        public bool IsLoading => Status == QueryStatus.Loading;
        public bool IsSuccess => Status == QueryStatus.Success;
        public bool IsError => Status == QueryStatus.Error;
        public bool IsEmpty => Status == QueryStatus.Empty;

        public static QueryState<T> Idle() => new QueryState<T>(QueryStatus.Idle, default, null);
        public static QueryState<T> Loading() => new QueryState<T>(QueryStatus.Loading, default, null);
        public static QueryState<T> Success(T data) => new QueryState<T>(QueryStatus.Success, data, null);
        public static QueryState<T> EmptyResult() => new QueryState<T>(QueryStatus.Empty, default, null);
        public static QueryState<T> Failure(string error) => new QueryState<T>(QueryStatus.Error, default, error);
    }
}
