using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpOverStream.Tests
{
    public class BodyStreamTests
    {
        class TestStream : MemoryStream
        {
            public bool Disposed { get; private set; }
            public TestStream(byte[] data) : base(data) { }
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                Disposed = true;
            }

        }
        [Fact]
        public async Task TestBodyStreamCloseOnReadEnd()
        {
            byte[] payload = new byte[10];
            var ms = new TestStream(payload);
            var bodyStream = new BodyStream(ms, 10, closeOnReachEnd: true);
            var result = new byte[10];
            var read = await bodyStream.ReadAsync(result, 0, 10);
            Assert.Equal(10, read);
            Assert.True(ms.Disposed);
        }
    }
}
