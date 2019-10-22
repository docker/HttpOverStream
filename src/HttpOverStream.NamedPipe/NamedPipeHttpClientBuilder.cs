using System;
using System.Net.Http;
using HttpOverStream.Logging;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeHttpClientBuilder
    {
        private string _pipeName;
        private ILoggerHttpOverStream _logger;
        private DelegatingHandler _outerHandler;
        private TimeSpan? _perRequestTimeout;
        private Version _httpVersion;

        public NamedPipeHttpClientBuilder(string pipeName)
        {
            _pipeName = pipeName;
        }

        public NamedPipeHttpClientBuilder WithLogger(ILoggerHttpOverStream logger)
        {
            _logger = logger;
            return this;
        }

        public NamedPipeHttpClientBuilder WithDelegatingHandler(DelegatingHandler outerHandler)
        {
            _outerHandler = outerHandler;
            return this;
        }

        public NamedPipeHttpClientBuilder WithPerRequestTimeout(TimeSpan perRequestTimeout)
        {
            _perRequestTimeout = perRequestTimeout;
            return this;
        }

        public NamedPipeHttpClientBuilder WithHttpVersion(Version httpVersion)
        {
            _httpVersion = httpVersion;
            return this;
        }

        public HttpClient Build()
        {
            return NamedPipeHttpClientFactory.ForPipeName(_pipeName, _logger, _perRequestTimeout, _httpVersion, _outerHandler);
        }
    }
}
