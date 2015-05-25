using Microsoft.Graphics.Canvas;

namespace HeartLight
{
    public interface IDrawable
    {
        void Draw(CanvasDrawingSession g);
    }
}
