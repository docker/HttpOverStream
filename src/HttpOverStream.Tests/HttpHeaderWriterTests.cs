using HttpOverStream.Client;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpOverStream.Tests
{
    public class HttpHeaderWriterTests
    {
        [Fact]
        public async Task WriteByteAsyncShouldSucceedWhenFullOrNot()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 1);
            await testee.WriteByteAsync(1);
            // assert buffered
            Assert.Equal(0, ms.Length);
            await testee.WriteByteAsync(2);
            // assert flushed
            Assert.Equal(1, ms.Length);
            await testee.FlushAsync();
            await ms.FlushAsync();
            Assert.Equal(2, ms.Length);
            Assert.True(Enumerable.SequenceEqual(new byte[] { 1, 2 }, ms.ToArray()));
        }

        IEnumerable<byte> byteSequence(byte fromIncl, byte toExcl)
        {
            for(var b = fromIncl; b < toExcl; b++)
            {
                yield return b;
            }
        }

        [Fact]
        public async Task WriteBytesAsyncWithVariousBufferAlignment()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 3);
            await testee.WriteBytesAsync(new byte[] { 1, 2 });
            await testee.WriteBytesAsync(new byte[] { 3 });
            await testee.WriteBytesAsync(new byte[] { 4 });
            await testee.WriteBytesAsync(new byte[] { 5, 6, 7 });
            await testee.WriteBytesAsync(new byte[] { 8, 9 });
            await testee.WriteBytesAsync(new byte[] { 10,11,12,13 });
            await testee.FlushAsync();
            await ms.FlushAsync();
            Assert.Equal(byteSequence(1, 14), ms.ToArray());
        }
        
        [Fact] 
        public async Task FlushTwice()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 3);
            await testee.WriteBytesAsync(new byte[] { 1, 2 });
            await testee.WriteBytesAsync(new byte[] { 3 });
            await testee.WriteBytesAsync(new byte[] { 4 });
            await testee.WriteBytesAsync(new byte[] { 5, 6, 7 });
            await testee.WriteBytesAsync(new byte[] { 8, 9 });
            await testee.WriteBytesAsync(new byte[] { 10, 11, 12, 13 });
            await testee.FlushAsync();
            await testee.FlushAsync();
            await ms.FlushAsync();
            await ms.FlushAsync();
            Assert.Equal(byteSequence(1, 14), ms.ToArray());
        }
        
        [Fact]
        public async Task WriteTwoBytesWithDifferentAlignments()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 3);
            await testee.WriteTwoBytesAsync(1, 2);
            await testee.WriteTwoBytesAsync(3, 4);
            await testee.WriteTwoBytesAsync(5,6);

            await testee.FlushAsync();
            await ms.FlushAsync();
            Assert.Equal(byteSequence(1, 7), ms.ToArray());
        }

        [Fact]
        public async Task WriteInvalidStringAsync()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 3);
            await Assert.ThrowsAsync<HttpRequestException>(() => testee.WriteStringAsync("ñ"));
        }
        [Fact]
        public async Task WriteStringWithDifferentAlignments()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 3);
            await testee.WriteStringAsync("Hello world");
            await testee.FlushAsync();
            await ms.FlushAsync();
            Assert.Equal("Hello world", Encoding.UTF8.GetString(ms.ToArray()));
        }
        [Fact]
        public async Task WriteMultiValueHeader()
        {
            var ms = new MemoryStream();
            var testee = new HttpHeaderWriter(ms, 3);
            await testee.WriteStatusAndHeadersAsync("HTTP/1.0", "200", "OK", new Dictionary<string, IEnumerable<string>>
            {
                {
                    "Test-Header", new List<string>{"value1", "value2"}
                }
            });
            await testee.FlushAsync();
            await ms.FlushAsync();
            Assert.Equal("HTTP/1.0 200 OK\r\nTest-Header: value1, value2\r\n\r\n", Encoding.UTF8.GetString(ms.ToArray()));
        }
    }
}
