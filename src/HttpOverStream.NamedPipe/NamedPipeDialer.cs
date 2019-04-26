using System;
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
        private readonly int _timeoutMs;

        public NamedPipeDialer(string pipeName)
            : this(pipeName, ".", PipeOptions.Asynchronous, 0)
        {
        }

        public NamedPipeDialer(string pipeName, int timeoutMs)
            : this(pipeName, ".", PipeOptions.Asynchronous, timeoutMs)
        {
        }

        public NamedPipeDialer(string pipeName, string serverName, PipeOptions pipeOptions, int timeoutMs)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _pipeOptions = pipeOptions;
            _timeoutMs = timeoutMs;
        }

        public async ValueTask<Stream> DialAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pipeStream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, _pipeOptions);
            await pipeStream.ConnectAsync(_timeoutMs, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => 
                    {
                        try
                        {
                            pipeStream.Dispose();
                        }
                        catch (Exception) { }
                    }
                );
            }
            return pipeStream;
        }
    }
}
