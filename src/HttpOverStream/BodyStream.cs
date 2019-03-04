using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public class BodyStream : Stream
    {
        long? _length;
        long _offset;
        long _read;
        public Stream Underlying { get; private set; }

        public BodyStream(Stream underlying, long? length)
        {
            Underlying = underlying;
            _length = length;
        }

        private int BoundedCount(int count)
        {
            if (!_length.HasValue)
            {
                return count;
            }
            return (int)Math.Min((long)count, _length.Value - _read);
        }


        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = BoundedCount(count);
            if (count == 0)
            {
                return 0;
            }
            var read = Underlying.Read(buffer, offset, count);
            _read += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length
        {
            get
            {
                if (!_length.HasValue)
                {
                    return Underlying.Length - _offset;
                }
                return _length.Value;
            }
        }
        public override long Position
        {
            get => Underlying.Position;
            set
            {
                throw new InvalidOperationException("Cannot seek");
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            count = BoundedCount(count);
            if (count == 0)
            {
                return 0;
            }
            var read = await Underlying.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            _read += read;
            return read;
        }
        public override void Close() => Underlying.Close();

    }
}
