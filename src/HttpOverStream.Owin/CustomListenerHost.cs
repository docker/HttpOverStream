using Microsoft.Owin;
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

namespace HttpOverStream.Owin
{
    public class CustomListenerHost : IDisposable
    {
        private readonly IListen _listener;
        private readonly Func<IDictionary<string, object>, Task> _app;
        private bool _disposed;

        private CustomListenerHost(IListen listener, Func<IDictionary<string, object>, Task> app)
        {
            _listener = listener;
            _app = app;
        }

        private void Start()
        {
            _listener.StartAsync(OnAccept, CancellationToken.None).Wait();
        }

        private async void OnAccept(Stream stream)
        {
            var owinContext = new OwinContext();
            owinContext.Set("owin.Version", "1.0");
            await PopulateRequestAsync(stream, owinContext.Request).ConfigureAwait(false);
            var body = new MemoryStream();
            owinContext.Response.Body = body;
            await _app(owinContext.Environment).ConfigureAwait(false);
            await body.FlushAsync().ConfigureAwait(false);
            var writer = new HttpHeaderWriter(stream, 1024);
            await writer.WriteStatusAndHeadersAsync(owinContext.Request.Protocol, owinContext.Response.StatusCode.ToString(), owinContext.Response.ReasonPhrase, owinContext.Response.Headers.Select(i => new KeyValuePair<string, IEnumerable<string>>(i.Key, i.Value))).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            body.Position = 0;
            await body.CopyToAsync(stream).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            await ((stream as IWithCloseWriteSupport)?.CloseWriteAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        }

        private async Task PopulateRequestAsync(Stream stream, IOwinRequest request)
        {
            var lineReader = new ByLineReader(stream, 1024);
            var requestLine = await lineReader.NextLineAsync().ConfigureAwait(false);
            var firstLine = HttpParser.GetAsciiString(requestLine);
            var parts = firstLine.Split(' ');
            request.Method = parts[0];
            request.Protocol = parts[2];
            var uri = new Uri("http://localhost" + parts[1]);
            for (; ; )
            {
                var line = await lineReader.NextLineAsync().ConfigureAwait(false);
                if (line.Count == 0)
                {
                    break;
                }
                (var name, var values) = HttpParser.ParseHeaderNameValue(line);
                request.Headers.Add(name, values.ToArray());
            }
            request.Scheme = uri.Scheme;
            request.Path = PathString.FromUriComponent(uri);
            request.QueryString = QueryString.FromUriComponent(uri);
            request.Body = new StreamWithPrefix(lineReader.Remaining, stream, null);
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
            context.ServerFactory = new ServerFactoryAdapter(new CustomListenerHostFactory(listener));
            IServiceProvider services = ServicesFactory.Create();
            var engine = services.GetService<IHostingEngine>();
            return engine.Start(context);
        }

        public static IDisposable Start(Action<IAppBuilder> startup, IListen listener)
        {
            var options = new StartOptions();
            options.AppStartup = startup.Method.ReflectedType.FullName;
            var context = new StartContext(options);
            context.Startup = startup;
            context.ServerFactory = new ServerFactoryAdapter(new CustomListenerHostFactory(listener));
            IServiceProvider services = ServicesFactory.Create();
            var engine = services.GetService<IHostingEngine>();
            return engine.Start(context);
        }

        private class CustomListenerHostFactory
        {
            private readonly IListen _listener;

            public CustomListenerHostFactory(IListen listener)
            {
                _listener = listener;
            }
            public IDisposable Create(Func<IDictionary<string, object>, Task> app, IDictionary<string, object> properties)
            {
                var result = new CustomListenerHost(_listener, app);
                result.Start();
                return result;
            }
        }

    }

}
