using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public static class ByLineReader
    {
        public static async ValueTask<string> ReadLineAsync(this Stream stream)
        {
            var bytes = new List<byte>();
            var buffer = new byte[1];
            const byte lineSeparator = (byte)'\n';
            for(; ; )
            {
                var read = await stream.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
                if (read == 0)
                {
                    // EOF -> throw
                    throw new EndOfStreamException("Reached end of stream before finding a line seperator");
                }
                if(buffer[0] == lineSeparator)
                {
                    break;
                }
                bytes.Add(buffer[0]);
            }
            // handle \r\n eol markers
            const byte carriageReturn = (byte)'\r';
            if (bytes.Count>0 && bytes[bytes.Count-1] == carriageReturn)
            {
                bytes.RemoveAt(bytes.Count - 1);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }
}
