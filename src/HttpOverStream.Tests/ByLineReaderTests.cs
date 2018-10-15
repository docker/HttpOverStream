using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            var testee = new ByLineReader(ms, 4);
            Assert.Equal("first line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("second longer line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("third line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("hello", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("", Encoding.UTF8.GetString(await testee.NextLineAsync()));
        }
        [Fact]
        public async Task TestWithLf()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("first line\nsecond longer line\nthird line\nhello\n\n"));
            ms.Flush();
            ms.Position = 0;
            var testee = new ByLineReader(ms, 4);
            Assert.Equal("first line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("second longer line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("third line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("hello", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("", Encoding.UTF8.GetString(await testee.NextLineAsync()));
        }
        [Fact]
        public async Task TestWithRemaining()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("first line\nsecond longer line\nthird line\nhello\n\nyololo"));
            ms.Flush();
            ms.Position = 0;
            var testee = new ByLineReader(ms, 4);
            Assert.Equal("first line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("second longer line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("third line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("hello", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            var remaining = testee.Remaining;
            Assert.Equal("yololo",Encoding.UTF8.GetString(remaining));
        }

        [Fact]
        public async Task TestWithBufferAlignmentAndInitialBufferSizeTooShort()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("aaa\nsecond longer line\n"));
            ms.Flush();
            ms.Position = 0;
            var testee = new ByLineReader(ms, 4);
            Assert.Equal("aaa", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("second longer line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Empty(testee.Remaining);
        }

        [Fact]
        public async Task TestMalformedHeaders()
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.UTF8.GetBytes("aaa\nsecond longer line\n"));
            ms.Flush();
            ms.Position = 0;
            var testee = new ByLineReader(ms, 4);
            Assert.Equal("aaa", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            Assert.Equal("second longer line", Encoding.UTF8.GetString(await testee.NextLineAsync()));
            await Assert.ThrowsAsync<IOException>(()=> testee.NextLineAsync());
        }
    }
}
