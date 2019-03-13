using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpOverStream.Tests
{
    public class ByLineReaderTests
    {
        [Fact]
        public async Task TestWithCrLf()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("first line\r\nsecond longer line\r\nthird line\r\nhello\r\n\r\n"));
            ms.Flush();
            ms.Position = 0;
            Assert.Equal("first line", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("second longer line", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("third line", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("hello", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("", await ms.ReadLineAsync(CancellationToken.None));
        }
        [Fact]
        public async Task TestWithLf()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("first line\nsecond longer line\nthird line\nhello\n\n"));
            ms.Flush();
            ms.Position = 0;
            Assert.Equal("first line", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("second longer line", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("third line", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("hello", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("", await ms.ReadLineAsync(CancellationToken.None));
        }
       
        [Fact]
        public async Task TestMalformedHeaders()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("aaa\nsecond longer line\n"));
            ms.Flush();
            ms.Position = 0;
            Assert.Equal("aaa", await ms.ReadLineAsync(CancellationToken.None));
            Assert.Equal("second longer line", await ms.ReadLineAsync(CancellationToken.None));
            await Assert.ThrowsAsync<EndOfStreamException>(async ()=> await ms.ReadLineAsync(CancellationToken.None));
        }
    }
}
