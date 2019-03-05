using Microsoft.Owin;
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

namespace HttpOverStream.Server.Owin
{
    public class CustomListenerHost : IDisposable
    {
        private readonly IListen _listener;
        private readonly Func<IDictionary<string, object>, Task> _app;
        private readonly ILogger _logger;
        private bool _disposed;

        private CustomListenerHost(IListen listener, Func<IDictionary<string, object>, Task> app, IAppBuilder builder)
        {
            _listener = listener;
            _app = app;
            _logger = builder.CreateLogger<CustomListenerHost>();
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
                    var owinContext = new OwinContext();
                    owinContext.Set("owin.Version", "1.0");
                    await PopulateRequestAsync(stream, owinContext.Request).ConfigureAwait(false);
                    var body = new MemoryStream();
                    owinContext.Response.Body = body;
                    // execute higher level middleware
                    await _app(owinContext.Environment).ConfigureAwait(false);
                    // write the response
                    await body.FlushAsync().ConfigureAwait(false);
                    await stream.WriteResponseStatusAndHeadersAsync(owinContext.Request.Protocol, owinContext.Response.StatusCode.ToString(), owinContext.Response.ReasonPhrase, owinContext.Response.Headers.Select(i => new KeyValuePair<string, IEnumerable<string>>(i.Key, i.Value))).ConfigureAwait(false);
                    body.Position = 0;
                    await body.CopyToAsync(stream).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
            }
            catch(Exception e)
            {
                _logger?.WriteWarning("error handling client stream", e);
            }
        }

        static Uri _localhostUri = new Uri("http://localhost/");

        private async Task PopulateRequestAsync(Stream stream, IOwinRequest request)
        {
            var firstLine = await stream.ReadLineAsync().ConfigureAwait(false);
            var parts = firstLine.Split(' ');
            if (parts.Length < 3)
            {
                throw new FormatException($"{firstLine} is not a valid request status");
            }
            request.Method = parts[0];
            request.Protocol = parts[2];
            var uri = new Uri(parts[1], UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri(_localhostUri, uri);
            }
            for (; ; )
            {
                var line = await stream.ReadLineAsync().ConfigureAwait(false);
                if (line.Length == 0)
                {
                    break;
                }
                (var name, var values) = HttpParser.ParseHeaderNameValues(line);
                request.Headers.Add(name, values.ToArray());
            }
            request.Scheme = uri.Scheme;
            request.Path = PathString.FromUriComponent(uri);
            request.QueryString = QueryString.FromUriComponent(uri);

            long? length = null;

            var contentLengthValues = request.Headers.GetValues("Content-Length");
            if (contentLengthValues!= null && contentLengthValues.Count > 0)
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

    }

}
