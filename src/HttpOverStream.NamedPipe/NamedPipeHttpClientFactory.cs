using HttpOverStream.Client;
using System;
using System.Net.Http;
using HttpOverStream.Logging;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeHttpClientFactory
    {
        public static HttpClient ForPipeName(string pipeName, ILoggerHttpOverStream logger = null, TimeSpan? perRequestTimeout = null)
        {
            var httpClient = new HttpClient(new DialMessageHandler(new NamedPipeDialer(pipeName), logger))
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
