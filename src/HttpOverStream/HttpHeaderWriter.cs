using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class HttpHeaderWriter
    {
        private static readonly byte[] s_spaceHttp10NewlineAsciiBytes = Encoding.ASCII.GetBytes(" HTTP/1.0\r\n");
        private readonly Stream _stream;
        private readonly int _bufferSize;
        private readonly byte[] _buffer;
        private int _offset = 0;

        public HttpHeaderWriter(Stream stream, int size)
        {
            _stream = stream;
            _bufferSize = size;
            _buffer = new byte[size];
        }

        public Task FlushAsync()
        {
            if (_offset == 0)
            {
                return Task.CompletedTask;
            }
            var o = _offset;
            _offset = 0;
            return _stream.WriteAsync(_buffer, 0, o);
        }

        public Task WriteByteAsync(byte b)
        {
            if (_offset < _bufferSize)
            {
                _buffer[_offset++] = b;
                return Task.CompletedTask;
            }
            return FlushAsync().ContinueWith((_) => WriteByteAsync(b));
        }

        public Task WriteBytesAsync(byte[] bytes, int offset = 0, int size = -1)
        {
            if (size == -1)
            {
                size = bytes.Length - offset;
            }
            if (size + _offset <= _bufferSize)
            {
                Buffer.BlockCopy(bytes, offset, _buffer, _offset, size);
                _offset += size;
                return Task.CompletedTask;
            }
            var remaining = _bufferSize - _offset;
            return WriteBytesAsync(bytes, offset, remaining)
                .ContinueWith((_) => FlushAsync()).Unwrap()
                .ContinueWith((_) => WriteBytesAsync(bytes, offset + remaining, size - remaining)).Unwrap();
        }

        public Task WriteTwoBytesAsync(byte first, byte second)
        {
            if(_offset+2 <= _bufferSize)
            {
                _buffer[_offset++] = first;
                _buffer[_offset++] = second;
                return Task.CompletedTask;
            }
            return WriteByteAsync(first)
                .ContinueWith((_) => WriteByteAsync(second)).Unwrap();
        }

        public Task WriteStringAsync(ReadOnlyMemory<char> s)
        {
            if (s.Length + _offset <= _bufferSize)
            {
                foreach (char c in s.Span)
                {
                    if ((c & 0xFF80) != 0)
                    {
                        throw new HttpRequestException("Invalid character encoding");
                    }
                    _buffer[_offset++] = (byte)c;
                }
                return Task.CompletedTask;
            }
            var remaining = _bufferSize - _offset;
            var head = s.Slice(0, remaining);
            var tail = s.Slice(remaining);
            return WriteStringAsync(head)
                .ContinueWith((_) => FlushAsync()).Unwrap()
                .ContinueWith((_) => WriteStringAsync(tail)).Unwrap();
        }
        public Task WriteStringAsync(string s)
        {
            return WriteStringAsync(s.AsMemory());
        }

        private async ValueTask WriteHeadersAsync(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            foreach (var header in headers)
            {
                await WriteStringAsync(header.Key).ConfigureAwait(false);
                await WriteTwoBytesAsync((byte)':', (byte)' ').ConfigureAwait(false);
                var first = true;
                var separator = header.Key == "Server" ? " " : ", ";
                foreach (var value in header.Value)
                {
                    if (first)
                    {
                        first = false;
                    }else
                    {
                        await WriteStringAsync(separator).ConfigureAwait(false);
                    }
                    await WriteStringAsync(value).ConfigureAwait(false);

                }
                await WriteTwoBytesAsync((byte)'\r', (byte)'\n').ConfigureAwait(false);
            }
        }
        public async ValueTask WriteMethodAndHeadersAsync(HttpRequestMessage request)
        {
            await WriteStringAsync(request.Method.Method).ConfigureAwait(false);
            await WriteByteAsync((byte)' ').ConfigureAwait(false);
            await WriteStringAsync(request.RequestUri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped)).ConfigureAwait(false);
            await WriteBytesAsync(s_spaceHttp10NewlineAsciiBytes).ConfigureAwait(false);
            await WriteHeadersAsync(request.Headers).ConfigureAwait(false);
            if(request.Content != null)
            {
                await WriteHeadersAsync(request.Content.Headers).ConfigureAwait(false);
            }
            await WriteTwoBytesAsync((byte)'\r', (byte)'\n').ConfigureAwait(false);
        }

        public async ValueTask WriteStatusAndHeadersAsync(string protocol, string statusCode, string reasonPhrase, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            await WriteStringAsync(protocol).ConfigureAwait(false);
            await WriteByteAsync((byte)' ').ConfigureAwait(false);
            await WriteStringAsync(statusCode).ConfigureAwait(false);
            await WriteByteAsync((byte)' ').ConfigureAwait(false);
            await WriteStringAsync(reasonPhrase).ConfigureAwait(false);
            await WriteTwoBytesAsync((byte)'\r', (byte)'\n').ConfigureAwait(false);
            await WriteHeadersAsync(headers).ConfigureAwait(false);
            await WriteTwoBytesAsync((byte)'\r', (byte)'\n').ConfigureAwait(false);

        }
    }
}
