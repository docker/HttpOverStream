using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class StreamWithPrefix : Stream, IWithCloseWriteSupport
    {
        private readonly ArraySegment<byte> _prefix;
        private readonly Stream _stream;
        private readonly long? _length;
        long _position;

        public StreamWithPrefix(ArraySegment<byte> prefix, Stream stream, long? length)
        {
            _prefix = prefix;
            _stream = stream;
            _length = length;
        }
        public override void Flush() => throw new NotImplementedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).Result;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_length.HasValue && _length.Value - _position < count)
            {
                count = (int)(_length.Value - _position);
            }
            if (count == 0)
            {
                return Task.FromResult(0);
            }
            if (_position < _prefix.Count)
            {
                var bufferCount = Math.Min(count, (int)(_prefix.Count - _position));
                _prefix.AsSpan().Slice((int)_position, bufferCount).CopyTo(buffer.AsSpan(offset, bufferCount));
                _position += bufferCount;
                var remainingCount = count - bufferCount;
                if (remainingCount == 0)
                {
                    return Task.FromResult(bufferCount);
                }
                return ReadAsync(buffer, offset + bufferCount, remainingCount, cancellationToken)
                    .ContinueWith(t => t.Result + bufferCount);
            }
            return _stream.ReadAsync(buffer, offset, count, cancellationToken).ContinueWith(t =>
            {
                _position += t.Result;
                return t.Result;
            });
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _length ?? throw new NotSupportedException();
        public override long Position { get { return _position; } set { throw new NotSupportedException(); } }

        public Task CloseWriteAsync()  {
            return (_stream as IWithCloseWriteSupport)?.CloseWriteAsync() ?? Task.CompletedTask;
        }
    }

}
