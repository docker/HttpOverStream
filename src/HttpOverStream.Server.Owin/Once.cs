using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.Server.Owin
{
    public class Once<T>
    {
        private Func<T> _todo;
        public Once(Func<T> todo)
        {
            _todo = todo;
        }

        public (T, bool) Do()
        {
            var todo = Interlocked.Exchange(ref _todo, null);
            if (todo == null)
            {
                return (default(T), false);
            }
            return (todo(), true);
        }
    }
}
