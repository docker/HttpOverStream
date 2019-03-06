using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeListener : IListen
    {
        private Task _listenTask;
        private CancellationTokenSource _listenTcs;
        private readonly string _pipeName;
        private readonly string _serverName;
        private readonly PipeDirection _pipeDirection;
        private readonly PipeOptions _pipeOptions;
        private readonly PipeTransmissionMode _pipeTransmissionMode;
        private readonly int _maxAllowedServerInstances;

        public NamedPipeListener(string pipeName)
            : this(pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough, PipeTransmissionMode.Byte, 0)
        {
        }
        public NamedPipeListener(string pipeName, PipeDirection pipeDirection, PipeOptions pipeOptions, PipeTransmissionMode pipeTransmissionMode, int maxAllowedServerInstances)
        {
            _pipeName = pipeName;
            _pipeDirection = pipeDirection;
            _pipeOptions = pipeOptions;
            _pipeTransmissionMode = pipeTransmissionMode;
            _maxAllowedServerInstances = maxAllowedServerInstances;
        }

        public Task StartAsync(Action<Stream> onConnection, CancellationToken cancellationToken)
        {
            _listenTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var srv = new NamedPipeServerStream(_pipeName, _pipeDirection, _maxAllowedServerInstances, _pipeTransmissionMode, _pipeOptions);
                    await srv.WaitForConnectionAsync(cancellationToken);
                    onConnection(srv);
                }
            });
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _listenTcs.Cancel();
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
    }
}
