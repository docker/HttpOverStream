using HttpOverStream.Client;
using HttpOverStream.Owin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Owin;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace HttpOverStream.WebApiTest
{  

    [RoutePrefix("api/e2e-tests")]
    public class EndToEndApiController : ApiController
    {
        [Route("hello-world")]
        [HttpGet()]
        public string HelloWorld()
        {
            return "Hello World";
        }

        [Route("hello")]
        [HttpPost()]
        public WelcomeMessage Hello([FromBody] PersonMessage person)
        {
            return new WelcomeMessage { Text = $"Hello {person.Name}" };
        }
    }
    public class PersonMessage
    {
        public string Name { get; set; }
    }

    public class WelcomeMessage
    {
        public string Text { get; set; }
    }

    [TestClass]
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
                        var srv = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
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
                var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(0,cancellationToken).ConfigureAwait(false);
                return pipe;
            }
        }

        [TestMethod]
        public async Task TestGet()
        {
            using (CustomListenerHost.Start(app =>
            {
                HttpConfiguration config = new HttpConfiguration();
                config.MapHttpAttributeRoutes();
                config.SuppressDefaultHostAuthentication();
                config.SuppressHostPrincipal();
                app.UseWebApi(config);
            }, new TestListener("legacy_test_get")))
            {
                var client = new HttpClient(new DialMessageHandler(new TestDialer("legacy_test_get")));
                var result = await client.GetAsync("http://localhost/api/e2e-tests/hello-world");
                Assert.AreEqual("Hello World", await result.Content.ReadAsAsync<string>());
            }
        }

        [TestMethod]
        public async Task TestBodyStream()
        {
            var listener = new TestListener("test-body-stream");
            var payload = Encoding.UTF8.GetBytes("Hello world");
            listener.StartAsync(con =>
            {
                Task.Run(async () =>
                {
                    await con.WriteAsync(payload, 0, payload.Length);
                });
            }, CancellationToken.None);


            var dialer = new TestDialer("test-body-stream");
            var stream = await dialer.DialAsync(new HttpRequestMessage(), CancellationToken.None);
            var bodyStream = new BodyStream(stream, payload.Length);
            var data = new byte[4096];
            var read = await bodyStream.ReadAsync(data, 0, data.Length);
            Assert.AreEqual(payload.Length, read);
            read = await bodyStream.ReadAsync(data, 0, data.Length);
            Assert.AreEqual(0, read);

        }

        [TestMethod]
        public async Task TestPost()
        {
            using (CustomListenerHost.Start(app =>
            {
                HttpConfiguration config = new HttpConfiguration();
                config.MapHttpAttributeRoutes();
                config.SuppressDefaultHostAuthentication();
                config.SuppressHostPrincipal();
                app.UseWebApi(config);
            }, new TestListener("legacy_test_post")))
            {
                var client = new HttpClient(new DialMessageHandler(new TestDialer("legacy_test_post")));
                var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
                var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
                Assert.AreEqual("Hello Test", wlcMsg.Text);
            }
        }
    }
}
