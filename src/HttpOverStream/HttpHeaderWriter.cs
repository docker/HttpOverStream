using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public static class HttpHeaderWriter
    {
        static byte[] _eol = Encoding.ASCII.GetBytes("\n");
        public static async Task WriteResponseStatusAndHeadersAsync(this Stream stream, string protocol, string statusCode, string reasonPhrase, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, CancellationToken cancellationToken)
        {
            var statusLine = $"{protocol} {statusCode} {reasonPhrase}\n";
            var payload = Encoding.ASCII.GetBytes(statusLine);
            Debug.WriteLine("-- Server: Trying to write statusline: " + statusLine);
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine("-- Server: Finished writeAsync");
            await WriteHeadersAsync(stream, headers, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine("-- Server: Finished WriteHeadersAsync");
            await stream.WriteAsync(_eol, 0, _eol.Length, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine("-- Server: Finished writeAsync eol");
        }

        private static async Task WriteHeadersAsync(Stream stream, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, CancellationToken cancellationToken)
        {
            foreach(var header in headers)
            {
                var separator = header.Key == "Server" ? " " : ", ";
                var values = string.Join(separator, header.Value);
                var line = $"{header.Key}: {values}\n";
                var payload = Encoding.ASCII.GetBytes(line);
                await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task WriteMethodAndHeadersAsync(this Stream stream, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var firstLine = $"{request.Method.Method} {request.RequestUri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped)} HTTP/{request.Version}\n";
            var payload = Encoding.ASCII.GetBytes(firstLine);

            Debug.WriteLine("-- Client: Writing request - first line");
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            Debug.WriteLine("-- Client: Writing request - headers");
            await WriteHeadersAsync(stream, request.Headers, cancellationToken).ConfigureAwait(false);
            if(request.Content != null)
            {
                if (!request.Content.Headers.ContentLength.HasValue)
                {
                    await request.Content.LoadIntoBufferAsync();
                    // force populating the underlying headers collection
                    request.Content.Headers.ContentLength = request.Content.Headers.ContentLength;
                }
                await WriteHeadersAsync(stream, request.Content.Headers, cancellationToken).ConfigureAwait(false);
            }
            await stream.WriteAsync(_eol, 0, _eol.Length, cancellationToken).ConfigureAwait(false);
        }
    }
}
