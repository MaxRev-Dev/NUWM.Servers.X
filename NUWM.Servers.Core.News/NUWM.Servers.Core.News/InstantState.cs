namespace NUWM.Servers.Core.News
{
    public enum InstantState
    {
        Success,
        TimedOut,
        ErrorParsing,
        ConnectionWithServerError,
        FromCache
    }
}