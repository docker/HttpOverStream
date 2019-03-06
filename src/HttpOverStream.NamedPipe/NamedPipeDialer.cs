using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeDialer : IDial
    {
        private readonly string _pipeName;
        private readonly string _serverName;
        private readonly PipeOptions _pipeOptions;
        private readonly int _timeout;

        public NamedPipeDialer(string pipeName) 
            : this(pipeName, ".", PipeOptions.Asynchronous, 0)
        {
        }
        public NamedPipeDialer(string pipeName, string serverName, PipeOptions pipeOptions, int timeout)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _pipeOptions = pipeOptions;
            _timeout = timeout;
        }

        public async ValueTask<Stream> DialAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pipe = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, _pipeOptions);
            await pipe.ConnectAsync(_timeout, cancellationToken).ConfigureAwait(false);
            return pipe;
        }
    }
}
