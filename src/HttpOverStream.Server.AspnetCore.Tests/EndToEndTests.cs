using HttpOverStream.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpOverStream.Server.AspnetCore.Tests
{
    public class PersonMessage
    {
        public string Name { get; set; }
    }

    public class WelcomeMessage
    {
        public string Text { get; set; }
    }
    [Route("api/e2e-tests")]
    public class EndToEndApiController : ControllerBase
    {
        [HttpGet("hello-world")]
        public string HelloWorld()
        {
            return "Hello World";
        }

        [HttpPost("hello")]
        public WelcomeMessage Hello([FromBody] PersonMessage person)
        {
            return new WelcomeMessage { Text = $"Hello {person.Name}" };
        }
    }
    public class EndToEndTests
    {
        private class TestListener : IListen
        {
            public TestListener(string pipeName)
            {
                _pipeName = pipeName;
            }
            private Task _listenTask;
            private CancellationTokenSource _listenTcs;
            private readonly string _pipeName;

            public Task StartAsync(Action<Stream> onConnection, CancellationToken cancellationToken)
            {
                _listenTcs = new CancellationTokenSource();
                var ct = _listenTcs.Token;
                _listenTask = Task.Run(async () =>
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var srv = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous );
                        await srv.WaitForConnectionAsync(ct);                        
                        onConnection(srv);
                    }
                });
                return Task.CompletedTask;
            }
            public async Task StopAsync(CancellationToken cancellationToken)
            {
                _listenTcs.Cancel();
                try
                {
                    await _listenTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }
        }

        private class TestDialer : IDial
        {
            private readonly string _pipeName;

            public TestDialer(string pipeName)
            {
                _pipeName = pipeName;
            }

            public async ValueTask<Stream> DialAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous );
                await pipe.ConnectAsync(0,cancellationToken).ConfigureAwait(false);
                return pipe;
            }
        }
        [Fact]
        public async Task TestHelloWorld()
        {
            var builder = new WebHostBuilder().Configure(app => app.UseMvc())
                .ConfigureServices(svc => svc.AddMvc().AddApplicationPart(Assembly.GetAssembly(typeof(EndToEndApiController))))
                .UseServer(new CustomListenerHost(new TestListener("test-core-get")));

            var host = builder.Build();
            await host.StartAsync();

            var client = new HttpClient(new DialMessageHandler(new TestDialer("test-core-get")));
            var result = await client.GetStringAsync("http://localhost/api/e2e-tests/hello-world");
            Assert.Equal("Hello World", result);

            await host.StopAsync();

        }
        [Fact]
        public async Task TestHelloPost()
        {
            var builder = new WebHostBuilder().Configure(app => app.UseMvc())
                .ConfigureServices(svc => svc.AddMvc().AddApplicationPart(Assembly.GetAssembly(typeof(EndToEndApiController))))
                .UseServer(new CustomListenerHost(new TestListener("test-core-post")));

            var host = builder.Build();
            await host.StartAsync();

            var client = new HttpClient(new DialMessageHandler(new TestDialer("test-core-post")));
            var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
            var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
            Assert.Equal("Hello Test", wlcMsg.Text);

            await host.StopAsync();

        }
    }
}
