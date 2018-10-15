using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public interface IWithCloseWriteSupport
    {
        Task CloseWriteAsync();
    }
}
