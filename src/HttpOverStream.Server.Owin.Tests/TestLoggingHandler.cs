using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.Server.Owin.Tests
{
    public class TestLoggingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = Guid.NewGuid();
            LogRequest(request, correlationId);
            var stopwatch = Stopwatch.StartNew();
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            LogResponse(request, response, stopwatch.Elapsed, correlationId);
            return response;
        }

        private void LogRequest(HttpRequestMessage request, Guid correlationId)
        {
            if (request == null)
            {
                Console.WriteLine("Null request");
                return;
            }

            Console.WriteLine($"[{correlationId}] {request.Method?.Method} {request.RequestUri}");
        }

        private void LogResponse(HttpRequestMessage request, HttpResponseMessage response, TimeSpan stopwatchElapsed, Guid correlationId)
        {
            Console.WriteLine($"[{correlationId}] {request?.Method?.Method} {request?.RequestUri} -> {(int)response.StatusCode} {response.StatusCode} took {(int)stopwatchElapsed.TotalMilliseconds}ms");
        }
    }
}
