using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class ByLineReader
    {
        private readonly Stream _stream;
        private byte[] _buffer;
        int _offset = 0;
        int _unconsumedSize = 0;

        public ByLineReader(Stream stream, int initialBufferSize)
        {
            _stream = stream;
            _buffer = new byte[initialBufferSize];
        }

        private async Task FillBufferAsync()
        {
            if (_offset == _buffer.Length)
            {
                // buffer empty
                _offset = 0;
            }
            else if(_offset > 0)
            {
                // partially consumed. put everything at the head of the buffer and continue
                Buffer.BlockCopy(_buffer, _offset, _buffer, 0, _unconsumedSize);
                _offset = 0;
            }
            else if(_offset +_unconsumedSize == _buffer.Length)
            {
                // read buffer is full, but we require a re-fill. It is because it does not contains Lf char. Let us increase buffer size
                var newBuf = new byte[_buffer.Length * 2];
                Buffer.BlockCopy(_buffer, 0, newBuf, 0, _buffer.Length);
                _buffer = newBuf;
            }
            var read = await _stream.ReadAsync(_buffer, _offset + _unconsumedSize, _buffer.Length - _offset - _unconsumedSize);
            if(read == 0)
            {
                throw new IOException("Invalid http response");
            }
            _unconsumedSize += read;
        }

        public async Task<ArraySegment<byte>> NextLineAsync()
        {
            var scanned = 0;
            for(; ; )
            {
                var lfIndex = Array.IndexOf(_buffer, (byte)'\n', _offset+ scanned, _unconsumedSize- scanned);
                scanned = _unconsumedSize;
                if (lfIndex >= 0)
                {
                    var startIndex = _offset;
                    var length = lfIndex - startIndex;
                    if(_buffer[lfIndex-1] == (byte)'\r')
                    {
                        length--;
                    }
                    var newOffset = lfIndex + 1;
                    _unconsumedSize -= newOffset - _offset;
                    _offset = newOffset;
                    return new ArraySegment<byte>(_buffer, startIndex, length);
                }
                await FillBufferAsync().ConfigureAwait(false);
            }
        }
        public ArraySegment<byte> Remaining
        {
            get
            {
                return new ArraySegment<byte>(_buffer, _offset, _unconsumedSize);
            }
        }
    }
}
