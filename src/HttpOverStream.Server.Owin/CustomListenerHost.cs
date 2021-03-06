﻿using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Engine;
using Microsoft.Owin.Hosting.ServerFactory;
using Microsoft.Owin.Hosting.Services;
using Owin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace HttpOverStream.Server.Owin
{
    public class CustomListenerHost : IDisposable
    {
        private readonly IListen _listener;
        private readonly Func<IDictionary<string, object>, Task> _app;
        private readonly ILogger _logger;
        private bool _disposed;
        static readonly byte[] _eol = Encoding.ASCII.GetBytes("\n");

        private CustomListenerHost(IListen listener, Func<IDictionary<string, object>, Task> app, IAppBuilder builder)
        {
            _listener = listener;
            _app = app;
            _logger = builder.CreateLogger("HttpOS.Owin.CLH");
        }


        private void Start()
        {
            _listener.StartAsync(OnAccept, CancellationToken.None).Wait();
        }

        private async void OnAccept(Stream stream)
        {
            try
            {
                using (stream)
                {
                    var onSendingHeadersCallbacks = new List<(Action<object>, object)>();
                    var owinContext = new OwinContext();
                    // this is an owin extension
                    owinContext.Set<Action<Action<object>, object>>("server.OnSendingHeaders", (callback, state) =>
                    {
                        onSendingHeadersCallbacks.Add((callback, state));
                    });
                    owinContext.Set("owin.Version", "1.0");

                    _logger.WriteVerbose("Server: reading message..");
                    await PopulateRequestAsync(stream, owinContext.Request, CancellationToken.None).ConfigureAwait(false);

                    // some transports (shuch as Named Pipes) may require the server to read some data before being able to get the
                    // client identity. So we get the client identity after reading the request headers
                    var transportIdentity = _listener.GetTransportIdentity(stream);
                    owinContext.Request.User = transportIdentity;

                    _logger.WriteVerbose("Server: finished reading message");
                    Func<Task> sendHeadersAsync = async () =>
                    {
                        // notify we are sending headers
                        foreach ((var callback, var state) in onSendingHeadersCallbacks)
                        {
                            callback(state);
                        }
                        // send status and headers
                        string statusCode = owinContext.Response.StatusCode.ToString();
                        _logger.WriteVerbose("Server: Statuscode was " + statusCode);
                        await stream.WriteServerResponseStatusAndHeadersAsync(owinContext.Request.Protocol, statusCode, owinContext.Response.ReasonPhrase, owinContext.Response.Headers.Select(i => new KeyValuePair<string, IEnumerable<string>>(i.Key, i.Value)), _logger.WriteVerbose, CancellationToken.None).ConfigureAwait(false);
                        _logger.WriteVerbose("Server: Wrote status and headers.");
                        await stream.FlushAsync().ConfigureAwait(false);
                    };
                    var body = new WriteInterceptStream(stream, sendHeadersAsync);
                    owinContext.Response.Body = body;
                    // execute higher level middleware
                    _logger.WriteVerbose("Server: executing middleware..");
                    try
                    {
                        await _app(owinContext.Environment).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        await HandleMiddlewareException(e, owinContext, body);
                    }
                    _logger.WriteVerbose("Server: finished executing middleware..");
                    await body.FlushAsync().ConfigureAwait(false);
                    _logger.WriteVerbose("Server: Finished request. Disposing connection.");
                }
            }
            catch (EndOfStreamException e)
            {
                _logger.WriteWarning("Server: Error handling client stream, (Client disconnected early / invalid HTTP request)", e);
            }
            catch (Exception e)
            {
                _logger.WriteError("Server: Error handling client stream " + e);
            }
        }

        private async Task HandleMiddlewareException(Exception e, OwinContext owinContext, WriteInterceptStream body)
        {
            var logMessage = "Exception trying to execute middleware: " + e;
            _logger.WriteError(logMessage);

            owinContext.Response.StatusCode = 500;
            var payload = Encoding.ASCII.GetBytes("Exception trying to execute middleware. See logs for details.");
            await body.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await body.WriteAsync(_eol, 0, _eol.Length).ConfigureAwait(false);
        }

        static Uri _localhostUri = new Uri("http://localhost/");

        private async Task PopulateRequestAsync(Stream stream, IOwinRequest request, CancellationToken cancellationToken)
        {
            var firstLine = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var parts = firstLine.Split(' ');
            if (parts.Length < 3)
            {
                throw new FormatException($"{firstLine} is not a valid request status");
            }

            _logger.WriteVerbose("Incoming request:" + firstLine);
            request.Method = parts[0];
            request.Protocol = parts[2];
            var uri = new Uri(parts[1], UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri(_localhostUri, uri);
            }
            for (; ; )
            {
                var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line.Length == 0)
                {
                    break;
                }
                _logger.WriteVerbose("Incoming header:" + line);
                (var name, var values) = HttpParser.ParseHeaderNameValues(line);
                request.Headers.Add(name, values.ToArray());
            }
            request.Scheme = uri.Scheme;
            request.Path = PathString.FromUriComponent(uri);
            request.QueryString = QueryString.FromUriComponent(uri);

            long? length = null;

            var contentLengthValues = request.Headers.GetValues("Content-Length");
            if (contentLengthValues != null && contentLengthValues.Count > 0)
            {
                length = long.Parse(contentLengthValues[0]);
            }
            request.Body = new BodyStream(stream, length);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _listener.StopAsync(CancellationToken.None).Wait();
        }

        public static IDisposable Start<TStartup>(IListen listener)
        {
            var options = new StartOptions();
            options.AppStartup = typeof(TStartup).AssemblyQualifiedName;
            var context = new StartContext(options);
            return Start(context, listener);
        }

        public static IDisposable Start(Action<IAppBuilder> startup, IListen listener)
        {
            var options = new StartOptions();
            options.AppStartup = startup.Method.ReflectedType.FullName;
            var context = new StartContext(options);
            context.Startup = startup;
            return Start(context, listener);
        }

        private static IDisposable Start(StartContext context, IListen listener)
        {
            IServiceProvider services = ServicesFactory.Create();
            var engine = services.GetService<IHostingEngine>();
            context.ServerFactory = new ServerFactoryAdapter(new CustomListenerHostFactory(listener));
            return engine.Start(context);
        }

        private class CustomListenerHostFactory
        {
            private readonly IListen _listener;
            private IAppBuilder _builder;

            public CustomListenerHostFactory(IListen listener)
            {
                _listener = listener;
            }

            public IDisposable Create(Func<IDictionary<string, object>, Task> app, IDictionary<string, object> properties)
            {
                var host = new CustomListenerHost(_listener, app, _builder);
                host.Start();
                return host;
            }

            public void Initialize(IAppBuilder builder)
            {
                _builder = builder;
            }
        }

        private class WriteInterceptStream : Stream
        {
            private readonly Stream _innerStream;
            private readonly Once<Task> _onFirstWrite;

            public WriteInterceptStream(Stream innerStream, Func<Task> onFirstWrite)
            {
                _innerStream = innerStream;
                _onFirstWrite = new Once<Task>(onFirstWrite);
            }

            public override void Flush()
            {
                _onFirstWrite.EnsureDone().Wait();
                _innerStream.Flush();
            }
            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
            public override void Write(byte[] buffer, int offset, int count)
            {
                _onFirstWrite.EnsureDone().Wait();
                _innerStream.Write(buffer, offset, count);
            }

            public override bool CanRead => false;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
            public override long Length => _innerStream.Length;
            public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                var task = WriteAsync(buffer, offset, count);
                var tcs = new TaskCompletionSource<int>(state);
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.TrySetException(t.Exception.InnerExceptions);
                    else if (t.IsCanceled)
                        tcs.TrySetCanceled();
                    else
                        tcs.TrySetResult(0);

                    callback?.Invoke(tcs.Task);
                }, TaskScheduler.Default);
                return tcs.Task;
            }
            public override void EndWrite(IAsyncResult asyncResult)
            {
                ((Task<int>)asyncResult).Wait();
            }
            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return _onFirstWrite
                    .EnsureDone()
                    .ContinueWith(previous => _innerStream.FlushAsync(cancellationToken), cancellationToken)
                    .Unwrap();
            }
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _onFirstWrite
                    .EnsureDone()
                    .ContinueWith(previous => _innerStream.WriteAsync(buffer, offset, count, cancellationToken), cancellationToken)
                    .Unwrap();
            }
            public override void WriteByte(byte value)
            {
                _onFirstWrite.EnsureDone().Wait();
                _innerStream.WriteByte(value);
            }
        }
    }
}
