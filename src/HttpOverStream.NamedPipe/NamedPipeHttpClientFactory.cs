using HttpOverStream.Client;
using System;
using System.Net.Http;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeHttpClientFactory
    {
        public static HttpClient ForPipeName(string pipeName, TimeSpan? perRequestTimeout = null)
        {
            var httpClient = new HttpClient(new DialMessageHandler(new NamedPipeDialer(pipeName)))
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
