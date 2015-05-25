using Windows.Foundation;
using Windows.UI.Core;

namespace HeartLight
{
    public interface IMainThreadDispatcher
    {
        IAsyncAction RunAsync(CoreDispatcherPriority prio, DispatchedHandler handler);
    }

    public class MainThreadDispatcher : IMainThreadDispatcher
    {
        private readonly CoreDispatcher _dispatcher;

        public MainThreadDispatcher(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public IAsyncAction RunAsync(CoreDispatcherPriority prio, DispatchedHandler handler)
        {
            return _dispatcher.RunAsync(prio, handler);
        }
    }
}
