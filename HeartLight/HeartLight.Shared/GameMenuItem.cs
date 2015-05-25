using System;
using Windows.Foundation;
using Windows.UI;

namespace HeartLight
{
    public class GameMenuItem
    {
        public string Text { get; set; }
        public Color Color { get; set; }
        public Color ActiveColor { get; set; }
        public Rect Rect { get; set; }

        public event EventHandler<EventArgs> OnActivated = delegate { };

        public GameMenuItem(string text, Color color, Color activeColor)
            : this(text, color, activeColor, delegate { })
        {  }

        public GameMenuItem(string text, Color color, Color activeColor, EventHandler<EventArgs> activationCallback)
        {
            Text = text;
            Color = color;
            ActiveColor = activeColor;
            Rect = new Rect(0, 0, 0, 0);
            OnActivated = activationCallback;
        }

        public void TriggerActivation()
        {
            OnActivated.Invoke(this, EventArgs.Empty);
        }
    }
}
