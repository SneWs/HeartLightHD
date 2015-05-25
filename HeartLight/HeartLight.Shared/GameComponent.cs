using Windows.UI.Core;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace HeartLight
{
    public class GameComponent : IUpdatable, IDrawable, IInputConsumer
    {
        public virtual bool IsEnabled { get; set; }

        protected GameComponent(bool isEnabled = true)
        {
            IsEnabled = isEnabled;
        }

        public virtual void CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        { }
        
        public virtual void Update(UpdateInfo updateInfo)
        { }

        public virtual void Draw(CanvasDrawingSession g)
        { }

        public virtual void OnKeyDown(KeyEventArgs args)
        { }

        public virtual void OnKeyUp(KeyEventArgs args)
        { }

        public virtual void OnPointerPressed(PointerEventArgs args)
        { }
    }
}
