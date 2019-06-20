using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HttpOverStream.Logging;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeListener : IListen
    {
        private Task _listenTask;
        private CancellationTokenSource _listenTcs;
        private readonly string _pipeName;
        private readonly PipeOptions _pipeOptions;
        private readonly PipeTransmissionMode _pipeTransmissionMode;
        private readonly int _maxAllowedServerInstances;
        private static readonly int _numServerThreads = 5;
        private readonly ILoggerHttpOverStream _logger;

        public NamedPipeListener(string pipeName, ILoggerHttpOverStream logger = null)
            : this(pipeName, PipeOptions.Asynchronous, PipeTransmissionMode.Byte, NamedPipeServerStream.MaxAllowedServerInstances, logger)
        {
        }

        public NamedPipeListener(string pipeName, PipeOptions pipeOptions, PipeTransmissionMode pipeTransmissionMode, int maxAllowedServerInstances, ILoggerHttpOverStream logger)
        {
            _pipeName = pipeName;
            _pipeOptions = pipeOptions;
            _pipeTransmissionMode = pipeTransmissionMode;
            _maxAllowedServerInstances = maxAllowedServerInstances;
            _logger = logger ?? new NoopLogger();
        }

        public Task StartAsync(Action<Stream> onConnection, CancellationToken cancellationToken)
        {
            _listenTcs = new CancellationTokenSource();
            var ct = _listenTcs.Token;
            _listenTask = StartServerAndDummyThreads(onConnection, ct);
            return Task.CompletedTask;
        }

        private Task StartServerAndDummyThreads(Action<Stream> onConnection, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            // We block on creating the dummy server/client to ensure we definitely have that set up before doing anything else
            var (dummyClient, dummyServer) = ConnectDummyClientAndServer(cancellationToken);
            tasks.Add(Task.Run(() => DisposeWhenCancelled(dummyClient, "client", cancellationToken)));
            tasks.Add(Task.Run(() => DisposeWhenCancelled(dummyServer, "server", cancellationToken)));

            // This runs synchronously until we've created the first server listener to ensure we can handle at least the first client connection
            var listenTask = CreateServerStreamAndListen(-1, onConnection, cancellationToken);
            tasks.Add(listenTask);

            // We don't technically need more than 1 thread but its faster
            for (int i = 0; i < _numServerThreads - 1; i++)
            {
                var i1 = i; // Capture value immediately to prevent late closure binding
                tasks.Add(Task.Run(() => CreateServerStreamAndListen(i1, onConnection, cancellationToken), cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        // We dont use the cancellation token but other implementations might
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _listenTcs.Cancel();
            }
            catch (AggregateException a) when (a.InnerExceptions.All(e => e is ObjectDisposedException))
            {
                // NamedPipe cancellations can throw ObjectDisposedException
                // They will be grouped in an AggregateException and this shouldnt break
            }
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task CreateServerStreamAndListen(int threadNumber, Action<Stream> onConnection,
            CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    var serverStream = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxAllowedServerInstances, _pipeTransmissionMode, _pipeOptions);
                    try
                    {
                        _logger.LogVerbose("[ thread " + threadNumber + "] Waiting for connection..");
                        await serverStream.WaitForConnectionAsync(cancelToken).ConfigureAwait(false);
                        _logger.LogVerbose("[ thread " + threadNumber + "] Found connection!");
                        // MP: We deliberately don't await this because we want to kick off the work on a background thead
                        // and immediately check for the next client connecting
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Task.Run(() => onConnection(serverStream), cancelToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                    }
                    catch (OperationCanceledException) // Thrown when cancellationToken is cancelled
                    {
                        _logger.LogVerbose("[ thread " + threadNumber + "] Cancelling server wait.");
                        serverStream.Dispose();
                    }
                    catch (IOException ex) // Thrown if client disconnects early
                    {
                        if (ex.Message.Contains("The pipe is being closed."))
                        {
                            _logger.LogVerbose("[ thread " + threadNumber + "] IOException: Could not read Named Pipe message - client disconnected before server finished reading.");
                        }
                        else
                        {
                            _logger.LogWarning("[ thread " + threadNumber + "] IOException, possibly because the client disconnected early:" + ex);
                        }
                        serverStream.Dispose();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("[ thread " + threadNumber + "] Exception thrown during server stream wait:" + e);
                        serverStream.Dispose();
                    }
                }
                _logger.LogVerbose("[ thread " + threadNumber + "] Stopping thread - cancelled");
            }
            catch (Exception e)
            {
                _logger.LogError("[ thread " + threadNumber + "] Exception creating server stream:" + e);
            }
        }

        private (NamedPipeClientStream, NamedPipeServerStream) ConnectDummyClientAndServer(CancellationToken cancellationToken)
        {
            const int MAX_DUMMYCONNECTION_RETRIES = 500;
            for (int i = 0; i < MAX_DUMMYCONNECTION_RETRIES; i++)
            {
                // Always have another stream active so if HandleStream finishes really quickly theres
                // no chance of the named pipe being removed altogether.
                // This is the same pattern as microsofts go library -> https://github.com/Microsoft/go-winio/pull/80/commits/ecd994be061f4ae21f463bbf08166d8edc96cadb
                var serverStream = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxAllowedServerInstances, _pipeTransmissionMode, _pipeOptions);
                serverStream.WaitForConnectionAsync(cancellationToken);

                var dummyClientStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    dummyClientStream.Connect(10); // 10ms timeout to connect to itself
                }
                catch (Exception)
                {
                    _logger.LogVerbose("[DUMMY] Dummy client couldn't connect, usually because a real client is trying to connect before the server is ready. Closing the pending connection and restarting server..");
                    serverStream.Disconnect();
                    continue;
                }

                _logger.LogVerbose("[DUMMY] Connected!");
                return (dummyClientStream, serverStream);
            }

            var errorMessage =
                $"Could not start server - dummy connection could not be made after {MAX_DUMMYCONNECTION_RETRIES} retries. " +
                "This could be because there are too many pending connections on this named pipe. Ensure clients don't spam the server before it's ready.";
            _logger.LogError(errorMessage);
            throw new Exception(errorMessage);
        }

        private async Task DisposeWhenCancelled(IDisposable disposable, string threadName,
            CancellationToken cancellationToken)
        {
            await cancellationToken.WhenCanceled().ConfigureAwait(false);

            try
            {
                disposable.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception disposing dummy {threadName} stream: {e}");
            }
        }
    }

    internal static class CancellationTokenExtensions
    {
        // Taken from https://github.com/dotnet/corefx/issues/2704#issuecomment-131221355
        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
