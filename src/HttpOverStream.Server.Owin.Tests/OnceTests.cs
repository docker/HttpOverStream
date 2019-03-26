using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.Server.Owin.Tests
{

    [TestClass]
    public class OnceTests
    {
        [TestMethod]
        public void TestOnceCallsOnlyOnce()
        {
            int value = 0;
            var once = new Once<int>(() => Interlocked.Increment(ref value));
            Parallel.For(0, 1000, _ =>
            {
                once.Do();
            });
            Assert.AreEqual(1, value);
        }
    }
}
