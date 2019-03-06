using HttpOverStream.Client;
using System;
using System.Net.Http;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeHttpClientFactory
    {
        public static HttpClient ForPipeName(string pipeName)
        {
            return new HttpClient(new DialMessageHandler(new NamedPipeDialer(pipeName)))
            {
                BaseAddress = new Uri("http://localhost")
            };
        }
    }
}
