using System;
using System.Collections.Generic;
using System.Linq;

namespace HttpOverStream
{
    public static class HttpParser
    {
        public static (string name, List<string> values) ParseHeaderNameValues(string line)
        {
            var pos = line.IndexOf(':');
            if(pos == -1)
            {
                throw new FormatException("Invalid header format");
            }
            var name = line.Substring(0, pos).Trim();
            var values = line.Substring(pos + 1)
                .Split(',')
                .Select(v => v.Trim())
                .ToList();
            return (name, values);
        }
    }
}
