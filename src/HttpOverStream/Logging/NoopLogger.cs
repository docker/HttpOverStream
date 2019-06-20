namespace HttpOverStream.Logging
{
    public class NoopLogger : ILoggerHttpOverStream
    {
        public void LogVerbose(string message)
        {
        }

        public void LogError(string message)
        {
        }

        public void LogWarning(string message)
        {
        }
    }
}