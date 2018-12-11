using System;
using System.Collections.Generic;
using System.Text;

namespace HttpOverStream
{
    public static class HttpParser
    {
        public static (string name, List<string> values) ParseHeaderNameValue(Span<byte> line)
        {
            int pos = 0;
            while (line[pos] != (byte)':' && line[pos] != (byte)' ')
            {
                pos++;
                if (pos == line.Length)
                {
                    // Invalid header line that doesn't contain ':'.
                    throw new FormatException("Invalid header format");
                }
            }
            if (pos == 0)
            {
                // Invalid empty header name.
                throw new FormatException("Invalid header format");
            }

            var nameBytes = line.Slice(0, pos);
            // Eat any trailing whitespace
            while (line[pos] == (byte)' ')
            {
                pos++;
                if (pos == line.Length)
                {
                    // Invalid header line that doesn't contain ':'.
                    throw new FormatException("Invalid header format");
                }
            }
            if (line[pos++] != ':')
            {
                // Invalid header line that doesn't contain ':'.
                throw new FormatException("Invalid header format");
            }

            // Skip whitespace after colon
            while (pos < line.Length && (line[pos] == (byte)' ' || line[pos] == (byte)'\t'))
            {
                pos++;
            }
            var valuesBytes = line.Slice(pos);
            // Note we ignore the return value from TryAddWithoutValidation; 

            var name = GetAsciiString(nameBytes);
            var values = new List<string>();
            for (; ; )
            {
                var sepIndex = valuesBytes.IndexOf((byte)',');
                if (sepIndex < 1)
                {
                    values.Add(GetAsciiString(TrimSpaceAndTabs(valuesBytes)));
                    break;
                }
                values.Add(GetAsciiString(TrimSpaceAndTabs(valuesBytes.Slice(0, sepIndex))));
                valuesBytes = valuesBytes.Slice(sepIndex + 1);
            }
            return (name, values);
        }
        static Span<byte> TrimSpaceAndTabs(Span<byte> source)
        {
            for (; ; )
            {
                if (source.IsEmpty)
                {
                    return source;
                }
                if (source[0] != (byte)' ' && source[0] != (byte)'\t')
                {
                    break;
                }
                source = source.Slice(1);
            }
            for (; ; )
            {
                if (source.IsEmpty)
                {
                    return source;
                }
                if (source[source.Length - 1] != (byte)' ' && source[source.Length - 1] != (byte)'\t')
                {
                    break;
                }
                source = source.Slice(0, source.Length - 1);
            }
            return source;
        }
        public static string GetAsciiString(Span<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }
            unsafe
            {
                fixed (byte* first = &bytes[0])
                {
                    return Encoding.ASCII.GetString(first, bytes.Length);
                }
            }
        }

        public static bool IsDigit(byte c) => (uint)(c - '0') <= '9' - '0';
    }
}
