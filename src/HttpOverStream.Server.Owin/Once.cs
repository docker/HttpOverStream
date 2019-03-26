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
        private T _result;
        private Func<T> _todo;
        public Once(Func<T> todo)
        {
            _todo = todo;
        }

        public T EnsureDone()
        {
            var todo = Interlocked.Exchange(ref _todo, null);
            if (todo != null)
            {
                _result = todo();
            }
            return _result;
        }
    }
}
