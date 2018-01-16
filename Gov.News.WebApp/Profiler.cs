using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News
{
    internal class Profiler
    {
        public static IDisposable StepStatic(string message)
        {
            return new ProfilerStep();
        }

        private class ProfilerStep : IDisposable
        {
            public void Dispose()
            {
                //TODO: Implement profiler trace logging
            }
        }
    }
}