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
    public class PipStreamWithCloseWrite : Stream, IWithCloseWriteSupport
    {
        private readonly PipeStream _underlying;

        public PipStreamWithCloseWrite(PipeStream underlying)
        {
            _underlying = underlying;
        }
        public Task CloseWriteAsync()
        {
            int result;
            WriteFile(_underlying.SafePipeHandle, IntPtr.Zero, 0, out result, IntPtr.Zero);
            return Task.CompletedTask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int WriteFile(SafeHandle handle, IntPtr bytes, int numBytesToWrite, out int numBytesWritten, IntPtr mustBeZero);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => _underlying.BeginRead(buffer, offset, count, callback, state);
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => _underlying.BeginWrite(buffer, offset, count, callback, state);
        public override bool CanRead => _underlying.CanRead;
        public override bool CanSeek => _underlying.CanSeek;
        public override bool CanTimeout => _underlying.CanTimeout;
        public override bool CanWrite => _underlying.CanWrite;
        public override void Close() => _underlying.Close();
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => _underlying.CopyToAsync(destination, bufferSize, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _underlying.Dispose();
            }
        }
        public override int EndRead(IAsyncResult asyncResult) => _underlying.EndRead(asyncResult);
        public override void EndWrite(IAsyncResult asyncResult) => _underlying.EndWrite(asyncResult);
        public override void Flush() => _underlying.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _underlying.FlushAsync(cancellationToken);
        public override object InitializeLifetimeService() => _underlying.InitializeLifetimeService();
        public override long Length => _underlying.Length;
        public override long Position { get => _underlying.Position; set => _underlying.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => _underlying.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _underlying.ReadAsync(buffer, offset, count, cancellationToken);
        public override int ReadByte() => _underlying.ReadByte();
        public override int ReadTimeout { get => _underlying.ReadTimeout; set => _underlying.ReadTimeout = value; }
        public override long Seek(long offset, SeekOrigin origin) => _underlying.Seek(offset, origin);
        public override void SetLength(long value) => _underlying.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _underlying.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _underlying.WriteAsync(buffer, offset, count, cancellationToken);
        public override void WriteByte(byte value) => _underlying.WriteByte(value);
        public override int WriteTimeout { get => _underlying.WriteTimeout; set => _underlying.WriteTimeout = value; }
    }

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
                        var srv = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                        await srv.WaitForConnectionAsync(ct);
                        srv.ReadMode = PipeTransmissionMode.Message;
                        onConnection(new PipStreamWithCloseWrite(srv));
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
                var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                await pipe.ConnectAsync(0,cancellationToken).ConfigureAwait(false);
                pipe.ReadMode = PipeTransmissionMode.Message;
                return new PipStreamWithCloseWrite(pipe);
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
