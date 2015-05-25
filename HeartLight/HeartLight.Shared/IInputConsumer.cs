using Windows.UI.Core;

namespace HeartLight
{
    public interface IInputConsumer
    {
        void OnKeyDown(KeyEventArgs args);
        void OnKeyUp(KeyEventArgs args);

        void OnPointerPressed(PointerEventArgs args);
    }
}
