using HttpOverStream.Client;
using System;
using System.Net.Http;
using HttpOverStream.Logging;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeHttpClientFactory
    {
        public static HttpClient ForPipeName(string pipeName, ILoggerHttpOverStream logger = null, TimeSpan? perRequestTimeout = null, Version httpVersion = null)
        {
            var httpClient = new HttpClient(new DialMessageHandler(new NamedPipeDialer(pipeName), logger, httpVersion))
            {
                BaseAddress = new Uri("http://localhost")
            };

            if (perRequestTimeout != null)
            {
                httpClient.Timeout = perRequestTimeout.Value;
            }

            return httpClient;
        }
    }
}
