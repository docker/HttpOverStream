using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.Client
{
    public interface IDial
    {
        ValueTask<Stream> DialAsync(HttpRequestMessage request, CancellationToken cancellationToken);
    }
}
