using HttpOverStream.Client;
using System;
using System.Net.Http;
using HttpOverStream.Logging;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeHttpClientFactory
    {
        public static HttpClient ForPipeName(string pipeName, ILoggerHttpOverStream logger = null, TimeSpan? perRequestTimeout = null, Version httpVersion = null, DelegatingHandler outerHandler = null )
        {
            var innerHandler = new DialMessageHandler(new NamedPipeDialer(pipeName), logger, httpVersion);
            HttpClient httpClient;
            if (outerHandler != null)
            {
                outerHandler.InnerHandler = innerHandler;
                httpClient = new HttpClient(outerHandler);
            }
            else
            {
                httpClient = new HttpClient(innerHandler);
            }

            httpClient.BaseAddress = new Uri("http://localhost");
            if (perRequestTimeout != null)
            {
                httpClient.Timeout = perRequestTimeout.Value;
            }

            return httpClient;
        }
    }
}
