using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public static class HttpHeaderWriter
    {
        static byte[] _eol = Encoding.ASCII.GetBytes("\n");
        public static async ValueTask WriteResponseStatusAndHeadersAsync(this Stream stream, string protocol, string statusCode, string reasonPhrase, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            var statusLine = $"{protocol} {statusCode} {reasonPhrase}\n";
            var payload = Encoding.ASCII.GetBytes(statusLine);
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await WriteHeadersAsync(stream, headers).ConfigureAwait(false);
            await stream.WriteAsync(_eol, 0, _eol.Length).ConfigureAwait(false);
        }

        private static async ValueTask WriteHeadersAsync(Stream stream, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            foreach(var header in headers)
            {
                var separator = header.Key == "Server" ? " " : ", ";
                var values = string.Join(separator, header.Value);
                var line = $"{header.Key}: {values}\n";
                var payload = Encoding.ASCII.GetBytes(line);
                await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            }
        }

        public static async ValueTask WriteMethodAndHeadersAsync(this Stream stream, HttpRequestMessage request)
        {
            var firstLine = $"{request.Method.Method} {request.RequestUri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped)} HTTP/{request.Version}\n";
            var payload = Encoding.ASCII.GetBytes(firstLine);
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await WriteHeadersAsync(stream, request.Headers).ConfigureAwait(false);
            if(request.Content != null)
            {
                if (!request.Content.Headers.ContentLength.HasValue)
                {
                    await request.Content.LoadIntoBufferAsync();
                    // force populating the underlying headers collection
                    request.Content.Headers.ContentLength = request.Content.Headers.ContentLength;
                }
                await WriteHeadersAsync(stream, request.Content.Headers).ConfigureAwait(false);
                
            }
            await stream.WriteAsync(_eol, 0, _eol.Length).ConfigureAwait(false);
        }
    }
}
