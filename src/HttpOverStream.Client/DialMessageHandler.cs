using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.Client
{
    public class DialMessageHandler : HttpMessageHandler
    {
        public const string UnderlyingStreamProperty = "DIAL_UNDERLYING_STREAM";
        private readonly IDial _dial;

        public DialMessageHandler(IDial dial)
        {
            _dial = dial;
        }

        private class DialResponseContent : HttpContent
        {
            private Stream _stream;
            private long? _length;

            public void SetContent(Stream unread, long? length)
            {
                _stream = unread;
                _length = length;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return _stream.CopyToAsync(stream);
            }

            protected override bool TryComputeLength(out long length)
            {
                if (_length.HasValue)
                {
                    length = _length.Value;
                    return true;
                }
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
                return Task.FromResult(_stream);
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ValidateAndNormalizeRequest(request);

            var stream = await _dial.DialAsync(request, cancellationToken).ConfigureAwait(false);

            request.Properties.Add(UnderlyingStreamProperty, stream);
            await stream.WriteMethodAndHeadersAsync(request, cancellationToken).ConfigureAwait(false);
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(stream).ConfigureAwait(false);
            }
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var responseContent = new DialResponseContent();
            var response = new HttpResponseMessage { RequestMessage = request, Content = responseContent };
            string statusLine = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            ParseStatusLine(response, statusLine);
            for (; ; )
            {
                var line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line.Length == 0)
                {
                    break;
                }
                try
                {
                    (var name, var value) = HttpParser.ParseHeaderNameValues(line);
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
            responseContent.SetContent(new BodyStream(stream, response.Content.Headers.ContentLength, closeOnReachEnd:true), response.Content.Headers.ContentLength);
            return response;
        }

        private void ParseStatusLine(HttpResponseMessage response, string line)
        {
            const int MinStatusLineLength = 12; // "HTTP/1.x 123" 
            if (line.Length < MinStatusLineLength || line[8] != ' ')
            {
                throw new HttpRequestException("Invalid response, expecting HTTP/1.0");
            }

            if (!line.StartsWith("HTTP/1.0"))
            {                
                throw new HttpRequestException("Invalid response, expecting HTTP/1.0");
            }
            response.Version = HttpVersion.Version10;
            // Set the status code
            if (int.TryParse(line.Substring(9,3), out int statusCode))
            {
                response.StatusCode = (HttpStatusCode)statusCode;
            }
            else
            {
                throw new HttpRequestException("Invalid response, can't parse status code");
            }
            // Parse (optional) reason phrase
            if (line.Length == MinStatusLineLength)
            {
                response.ReasonPhrase = string.Empty;
            }
            else if (line[MinStatusLineLength] == ' ')
            {
                response.ReasonPhrase = line.Substring(MinStatusLineLength + 1);
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
