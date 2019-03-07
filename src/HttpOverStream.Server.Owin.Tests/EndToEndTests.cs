using HttpOverStream.Client;
using HttpOverStream.NamedPipe;
using Microsoft.Owin.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Owin;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace HttpOverStream.Server.Owin.Tests
{

    [RoutePrefix("api/e2e-tests")]
    public class EndToEndApiController : ApiController
    {
        [Route("hello-world")]
        [HttpGet()]
        public string GetHelloWorld()
        {
            return "Hello World";
        }

        [Route("hello")]
        [HttpPost()]
        public WelcomeMessage PostHello([FromBody] PersonMessage person)
        {
            return new WelcomeMessage { Text = $"Hello {person.Name}" };
        }

        [Route("timeout")]
        [HttpGet()]
        public async Task<string> GetTimeoutAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            return "This should have timed out.";
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
        [TestMethod]
        public async Task TestGet()
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener("legacy_test_get")))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("legacy_test_get")));
                var result = await client.GetAsync("http://localhost/api/e2e-tests/hello-world");
                Assert.AreEqual("Hello World", await result.Content.ReadAsAsync<string>());
            }
        }

        [TestMethod]
        public async Task TestServerTimeout()
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener("legacy_test_get")))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("legacy_test_get")));
                client.Timeout = TimeSpan.FromSeconds(1);
                await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
                {
                    await client.GetAsync("http://localhost/api/e2e-tests/timeout");
                });
            }
        }

        [TestMethod]
        public async Task TestBodyStream()
        {
            var listener = new NamedPipeListener("test-body-stream");
            var payload = Encoding.UTF8.GetBytes("Hello world");
            await listener.StartAsync(con =>
            {
                Task.Run(async () =>
                {
                    await con.WriteAsync(payload, 0, payload.Length);
                });
            }, CancellationToken.None);


            var dialer = new NamedPipeDialer("test-body-stream");
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
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener("legacy_test_post")))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("legacy_test_post")));
                var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
                var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
                Assert.AreEqual("Hello Test", wlcMsg.Text);
            }
        }

        [TestMethod]
        public async Task TestStreamInteruption()
        {
            var logFactory = new TestLoggerFactory();
            using (CustomListenerHost.Start(app =>
            {
                SetupDefaultAppBuilder(app);
                app.SetLoggerFactory(logFactory);
            }, new NamedPipeListener("test_stream_interuption")))
            {
                var dialer = new NamedPipeDialer("test_stream_interuption");
                using (var fuzzyStream = await dialer.DialAsync(new HttpRequestMessage(), CancellationToken.None))
                {
                    // just write the first line of a valid http request, and drop the connection
                    var payload = Encoding.ASCII.GetBytes("GET /docs/index.html HTTP/1.0\n");
                    await fuzzyStream.WriteAsync(payload, 0, payload.Length);
                }
                var ex = await logFactory.ExceptionReceived.Task;
                Assert.IsInstanceOfType(ex, typeof(EndOfStreamException));
                var client = new HttpClient(new DialMessageHandler(dialer));
                var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
                var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
                Assert.AreEqual("Hello Test", wlcMsg.Text);
            }
        }

        private static void SetupDefaultAppBuilder(IAppBuilder appBuilder)
        {
            HttpConfiguration config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.SuppressDefaultHostAuthentication();
            config.SuppressHostPrincipal();
            appBuilder.UseWebApi(config);
        }

        class TestLoggerFactory : ILoggerFactory, ILogger
        {
            public TaskCompletionSource<Exception> ExceptionReceived { get; private set; } = new TaskCompletionSource<Exception>();
            public ILogger Create(string name) => this;
            
            public bool WriteCore(TraceEventType eventType, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
            {
                ExceptionReceived.TrySetResult(exception);
                return true;
            }
        }
    }
}
