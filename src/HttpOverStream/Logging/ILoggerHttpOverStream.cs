namespace HttpOverStream.Logging
{
    public interface ILoggerHttpOverStream
    {
        void LogError(string message);
        void LogWarning(string message);
        void LogVerbose(string message);
    }
}
