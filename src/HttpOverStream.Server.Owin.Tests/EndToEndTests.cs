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
            await Task.Delay(TimeSpan.FromSeconds(5));
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
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task TestGet()
        {
            await TestGet_Impl();
        }

        [TestMethod]
        public async Task TestGetStressTest()
        {
            for (int i = 0; i < 100; i++)
            {
                await TestGet_Impl();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (Debug.Listeners.Count > 1)
                {
                    Debug.Listeners.RemoveAt(1);
                }
            }
        }

        [TestMethod]
        public async Task TestGetStressTest_SingleServer()
        {
            for (int i = 0; i < 5; i++)
            {
                await TestGet_Impl(100);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (Debug.Listeners.Count > 1)
                {
                    Debug.Listeners.RemoveAt(1);
                }
            }
        }

        private async Task TestGet_Impl(int numberOfRequests = 1)
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener(TestContext.TestName)))
            {
                for (int i = 0; i < numberOfRequests; i++)
                {
                    var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName)));
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var result = await client.GetAsync("http://localhost/api/e2e-tests/hello-world");
                    Assert.AreEqual("Hello World", await result.Content.ReadAsAsync<string>());
                }
            }
        }


        [TestMethod]
        public async Task TestGetStressTest_ClientBeforeServer()
        {
            for (int i = 0; i < 5; i++)
            {
                await TestClientStartsFirst_ServerDropsClientButComesUpAfterwards_Impl(100);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (Debug.Listeners.Count > 1)
                {
                    Debug.Listeners.RemoveAt(1);
                }
            }
        }

        private async Task TestClientStartsFirst_ServerDropsClientButComesUpAfterwards_Impl(int numberOfRequests)
        {
            var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName, (int)TimeSpan.FromSeconds(30).TotalMilliseconds)));
            client.Timeout = TimeSpan.FromSeconds(30);
            var clientTask = client.GetAsync("http://localhost/api/e2e-tests/hello-world");

            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener(TestContext.TestName)))
            {
                for (int i = 0; i < numberOfRequests; i++)
                {
                    var client2 = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName)));
                    client2.Timeout = TimeSpan.FromSeconds(5);
                    var result = await client2.GetAsync("http://localhost/api/e2e-tests/hello-world");
                    Assert.AreEqual("Hello World", await result.Content.ReadAsAsync<string>());
                }
            }
        }

        [TestMethod]
        public async Task TestBadRequestStressTest()
        {
            for (int i = 0; i < 5; i++)
            {
                await TestBadRequest_BadMediaType_Impl();
            }
        }

        private async Task TestBadRequest_BadMediaType_Impl()
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener(TestContext.TestName)))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName)));
                client.Timeout = TimeSpan.FromSeconds(1);
                var badContent = new StringContent("{ ", Encoding.UTF8, "application/broken");
                var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", badContent);
                var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
                Assert.AreEqual("Hello ", wlcMsg.Text);
            }
        }

        [TestMethod]
        public async Task TestBadRequest_NonexistentEndpoint_StressTest()
        {
            for (int i = 0; i < 5; i++)
            {
                Debug.WriteLine(i);
                await TestBadRequest_NonexistentEndpoint_Impl();
            }
        }

        [TestMethod]
        public async Task TestBadRequest_NonexistentEndpoint()
        {
            await TestBadRequest_NonexistentEndpoint_Impl();
        }
        private async Task TestBadRequest_NonexistentEndpoint_Impl()
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener(TestContext.TestName)))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName)));
                try
                {
                    var badContent = new StringContent("{ }");
                    var result = await client.PostAsJsonAsync("http://localhost/api/this-doesnt-exist", badContent);
                    Debug.WriteLine("Client: Posted Json ");

                    Assert.AreEqual(result.StatusCode, System.Net.HttpStatusCode.NotFound);
                }
                catch(Exception e)
                {
                    Debug.WriteLine("EXCEPTION " + e);
                    throw;
                }
            }
        }

        [TestMethod]
        public async Task TestBodyStream()
        {
            await TestBodyStream_Impl();
        }

        [TestMethod]
        public async Task TestBodyStreamStressTest()
        {
            for (int i = 0; i < 50; i++)
            {
                await TestBodyStream_Impl();
            }
        }

        private async Task TestBodyStream_Impl()
        {
            var listener = new NamedPipeListener(TestContext.TestName);
            var payload = Encoding.UTF8.GetBytes("Hello world");
            await listener.StartAsync(con =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await con.WriteAsync(payload, 0, payload.Length);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("... WriteAsync exception:" + e.Message);
                    }
                });
            }, CancellationToken.None);


            var dialer = new NamedPipeDialer(TestContext.TestName);
            var stream = await dialer.DialAsync(new HttpRequestMessage(), CancellationToken.None);
            var bodyStream = new BodyStream(stream, payload.Length);
            var data = new byte[4096];
            var read = await bodyStream.ReadAsync(data, 0, data.Length);
            Assert.AreEqual(payload.Length, read);
            read = await bodyStream.ReadAsync(data, 0, data.Length);
            Assert.AreEqual(0, read);

            // Clean up
            await listener.StopAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestPost()
        {
            await TestPost_Impl();
        }

        [TestMethod]
        public async Task TestPostStressTest()
        {
            for (int i = 0; i < 50; i++)
            {
                await TestPost_Impl();
            }
        }

        private async Task TestPost_Impl()
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener(TestContext.TestName)))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName)));
                var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
                var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
                Assert.AreEqual("Hello Test", wlcMsg.Text);
            }
        }

        [TestMethod]
        public async Task TestStreamInteruption()
        {
            await TestStreamInterruption_Impl();
        }

        [TestMethod]
        public async Task TestStreamInteruptionStressTest()
        {
            for (int i = 0; i < 50; i++)
            {
                await TestStreamInterruption_Impl();
            }
        }

        private async Task TestStreamInterruption_Impl()
        {
            var logFactory = new TestLoggerFactory();
            using (CustomListenerHost.Start(app =>
            {
                SetupDefaultAppBuilder(app);
                app.SetLoggerFactory(logFactory);
            }, new NamedPipeListener(TestContext.TestName)))
            {
                var dialer = new NamedPipeDialer(TestContext.TestName);
                using (var fuzzyStream = await dialer.DialAsync(new HttpRequestMessage(), CancellationToken.None))
                {
                    // just write the first line of a valid http request, and drop the connection
                    var payload = Encoding.ASCII.GetBytes("GET /docs/index.html HTTP/1.0\n");
                    await fuzzyStream.WriteAsync(payload, 0, payload.Length);
                }
                var ex = await logFactory.ExceptionReceived.Task;
                Assert.IsInstanceOfType(ex, typeof(EndOfStreamException));

                // Test the stream still works afterwards
                var client = new HttpClient(new DialMessageHandler(dialer));
                var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
                var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
                Assert.AreEqual("Hello Test", wlcMsg.Text);
            }
        }

        [TestMethod]
        public async Task TestClientTimeoutIsRespectedWhenServerTakesTooLong()
        {
            using (CustomListenerHost.Start(SetupDefaultAppBuilder, new NamedPipeListener(TestContext.TestName)))
            {
                var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer(TestContext.TestName)));
                client.Timeout = TimeSpan.FromMilliseconds(100);
                var sw = Stopwatch.StartNew();
                await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
                {
                    await client.GetAsync("http://localhost/api/e2e-tests/timeout");
                });
                sw.Stop();
                Assert.IsTrue(sw.ElapsedMilliseconds < 1000, $"Client Timeout wasn't respected - GetAsync took too long ({sw.ElapsedMilliseconds} ms)");
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
