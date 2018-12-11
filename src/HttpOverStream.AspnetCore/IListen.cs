using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.AspnetCore
{
    public interface IListen
    {
        Task StartAsync(Action<Stream> onConnection, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
