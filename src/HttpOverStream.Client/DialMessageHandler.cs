using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.Client
{
    public class DialMessageHandler : HttpMessageHandler
    {
        private static readonly ulong s_http10Bytes = BinaryPrimitives.ReadUInt64LittleEndian(Encoding.ASCII.GetBytes("HTTP/1.0"));
        public const string UnderlyingStreamProperty = "DIAL_UNDERLYING_STREAM";
        private readonly IDial _dial;

        public DialMessageHandler(IDial dial)
        {
            _dial = dial;
        }

        private class DialResponseContent : HttpContent
        {
            private ArraySegment<byte> _remainingFromHeadersLayer;
            private Stream _stream;
            public void SetContent(ArraySegment<byte> readAhead, Stream unread)
            {
                _remainingFromHeadersLayer = readAhead;
                _stream = unread;
            }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return stream.WriteAsync(_remainingFromHeadersLayer.Array, _remainingFromHeadersLayer.Offset, _remainingFromHeadersLayer.Count).ContinueWith((_) => _stream.CopyToAsync(stream)).Unwrap();
            }
            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    _stream.Dispose();
                }
            }

            protected override Task<Stream> CreateContentReadStreamAsync()
            {
                return Task.FromResult<Stream>(new StreamWithPrefix(_remainingFromHeadersLayer, _stream, Headers.ContentLength));
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ValidateAndNormalizeRequest(request);
            var stream = await _dial.DialAsync(request, cancellationToken).ConfigureAwait(false);

            request.Properties.Add(UnderlyingStreamProperty, stream);
            var headerWriter = new HttpHeaderWriter(stream, 4096);
            await headerWriter.WriteMethodAndHeadersAsync(request).ConfigureAwait(false);
            await headerWriter.FlushAsync().ConfigureAwait(false);
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(stream).ConfigureAwait(false);
            }
            await stream.FlushAsync().ConfigureAwait(false);
            await ((stream as IWithCloseWriteSupport)?.CloseWriteAsync() ?? Task.CompletedTask).ConfigureAwait(false);
            var responseContent = new DialResponseContent();
            var response = new HttpResponseMessage { RequestMessage = request, Content = responseContent };
            var lineReader = new ByLineReader(stream, 4096);
            ParseStatusLine(response, await lineReader.NextLineAsync().ConfigureAwait(false));
            for (; ; )
            {
                var line = await lineReader.NextLineAsync().ConfigureAwait(false);
                if (line.Count == 0)
                {
                    break;
                }
                try
                {
                    (var name, var value) = HttpParser.ParseHeaderNameValue(line);
                    if(!response.Headers.TryAddWithoutValidation(name, value))
                    {
                        response.Content.Headers.TryAddWithoutValidation(name, value);
                    }
                }
                catch (FormatException ex)
                {
                    throw new HttpRequestException("Error parsing header", ex);
                }
            }
            responseContent.SetContent(lineReader.Remaining, stream);
            return response;


        }


        private void ParseStatusLine(HttpResponseMessage response, Span<byte> line)
        {
            const int MinStatusLineLength = 12; // "HTTP/1.x 123" 
            if (line.Length < MinStatusLineLength || line[8] != ' ')
            {
                throw new HttpRequestException("Invalid response");
            }

            ulong first8Bytes = BinaryPrimitives.ReadUInt64LittleEndian(line);
            if (first8Bytes != s_http10Bytes)
            {
                throw new HttpRequestException("Invalid response");
            }
            response.Version = HttpVersion.Version10;
            // Set the status code
            byte status1 = line[9], status2 = line[10], status3 = line[11];
            if (!HttpParser.IsDigit(status1) || !HttpParser.IsDigit(status2) || !HttpParser.IsDigit(status3))
            {
                throw new HttpRequestException("Invalid response");
            }

            response.StatusCode = (HttpStatusCode)(100 * (status1 - '0') + 10 * (status2 - '0') + (status3 - '0'));
            // Parse (optional) reason phrase
            if (line.Length == MinStatusLineLength)
            {
                response.ReasonPhrase = string.Empty;
            }
            else if (line[MinStatusLineLength] == ' ')
            {
                Span<byte> reasonBytes = line.Slice(MinStatusLineLength + 1);
                try
                {
                    response.ReasonPhrase = HttpParser.GetAsciiString(reasonBytes);
                }
                catch (FormatException error)
                {
                    throw new HttpRequestException("Invalid response", error);
                }
            }
            else
            {
                throw new HttpRequestException("Invalid response");
            }
        }

        private void ValidateAndNormalizeRequest(HttpRequestMessage request)
        {
            request.Version = HttpVersion.Version10;
            // Add headers to define content transfer, if not present
            if (request.Headers.TransferEncodingChunked.GetValueOrDefault())
            {
                throw new HttpRequestException("DialMessageHandler does not support chunked encoding");
            }

            // HTTP 1.0 does not support Expect: 100-continue; just disable it.
            if (request.Headers.ExpectContinue == true)
            {
                request.Headers.ExpectContinue = false;
            }
        }


    }
}
