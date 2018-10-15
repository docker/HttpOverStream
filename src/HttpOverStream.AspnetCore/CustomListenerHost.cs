using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.AspnetCore
{
    public class CustomListenerHost : IServer
    {
        private readonly IListen _listener;

        public CustomListenerHost( IListen listener)
           : this( new FeatureCollection(), listener)
        {

        }



        public CustomListenerHost( IFeatureCollection featureCollection, IListen listener)
        {           
            Features = featureCollection ?? throw new ArgumentNullException(nameof(featureCollection));
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }
        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            var app = (IHttpApplication<HostingApplication.Context>)application;
            return _listener.StartAsync(stream => {
                HandleClientStream(stream, app);
            }, cancellationToken);
        }

        async void HandleClientStream(Stream stream, IHttpApplication<HostingApplication.Context> application)
        {
            using (stream)
            {
                var httpCtx = new DefaultHttpContext();
                var requestFeature = await CreateRequesteAsync(stream).ConfigureAwait(false);
                httpCtx.Features.Set<IHttpRequestFeature>(requestFeature);
                var responseFeature = new HttpResponseFeature();
                httpCtx.Features.Set<IHttpResponseFeature>(responseFeature);
                var ctx = application.CreateContext(httpCtx.Features);
                var body = new MemoryStream();
                responseFeature.Body = body;
                await application.ProcessRequestAsync(ctx).ConfigureAwait(false);
                var writer = new HttpHeaderWriter(stream, 1024);
                await writer.WriteStatusAndHeadersAsync(requestFeature.Protocol, responseFeature.StatusCode.ToString(), responseFeature.ReasonPhrase, responseFeature.Headers.Select(i => new KeyValuePair<string, IEnumerable<string>>(i.Key, i.Value))).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                body.Position = 0;
                await body.CopyToAsync(stream).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                await ((stream as IWithCloseWriteSupport)?.CloseWriteAsync() ?? Task.CompletedTask).ConfigureAwait(false);
            }
        }

        async Task<HttpRequestFeature> CreateRequesteAsync(Stream stream)
        {
            var lineReader = new ByLineReader(stream, 1024);
            var requestLine = await lineReader.NextLineAsync().ConfigureAwait(false);
            var firstLine = HttpParser.GetAsciiString(requestLine);
            var parts = firstLine.Split(' ');
            var result = new HttpRequestFeature();
            result.Method = parts[0];
            var uri = new Uri("http://localhost"+ parts[1]);
            result.Protocol = parts[2];
            for(; ; )
            {
                var line = await lineReader.NextLineAsync().ConfigureAwait(false);
                if(line.Count == 0)
                {
                    break;
                }
                (var name, var values) = HttpParser.ParseHeaderNameValue(line);
                result.Headers.Add(name, new Microsoft.Extensions.Primitives.StringValues(values.ToArray()));
            }
            result.Scheme = uri.Scheme;    
            result.Path = PathString.FromUriComponent(uri);
            result.QueryString = QueryString.FromUriComponent(uri).Value;            
            result.Body = new StreamWithPrefix(lineReader.Remaining, stream, result.Headers.ContentLength);
            return result;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _listener.StopAsync(cancellationToken);
        }

        public IFeatureCollection Features { get; }

        public void Dispose() { }
    }
}
