using System;
using System.Threading;

namespace Veldrid.Vk
{
    internal class ResourceRefCount
    {
        private static int nextResourceId = 1;

        private readonly Action _disposeAction;
        private int _refCount;
        private int _resouceId;

        public int ResourceId => _resouceId;

        public ResourceRefCount(Action disposeAction)
        {
            _disposeAction = disposeAction;
            _refCount = 1;
            _resouceId = Interlocked.Increment(ref nextResourceId);
            if ((_resouceId % 10000) == 0)
            {
                Console.WriteLine("ResourceRefCount: " + _resouceId);
            }
        }

        public int Increment()
        {
            int ret = Interlocked.Increment(ref _refCount);
#if VALIDATE_USAGE
            if (ret == 0)
            {
                throw new VeldridException("An attempt was made to reference a disposed resource.");
            }
#endif
            return ret;
        }

        public int Decrement()
        {
            int ret = Interlocked.Decrement(ref _refCount);
            if (ret == 0)
            {
                _disposeAction();
            }

            return ret;
        }
    }
}
