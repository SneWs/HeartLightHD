using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Foundation;
using Windows.UI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Numerics;
using Microsoft.Graphics.Canvas.Text;

namespace HeartLight
{
    internal struct DebugTextInfo
    {
        internal string Text;
        internal DateTime AddedTime;

        internal DebugTextInfo(string text)
            : this(text, DateTime.Now)
        { }

        internal DebugTextInfo(string text, DateTime addedTime)
        {
            Text = text;
            AddedTime = addedTime;
        }
    }

    public class DebugRenderer : GameComponent
    {
#if DEBUG || _DEBUG
        public static bool OutlineLayoutAreas = true;
        public static bool DebugRendererEnabled = true;
#else
        public static bool OutlineLayoutAreas = false;
        public static bool DebugRendererEnabled = false;
#endif

        private static DebugRenderer _debugRenderer;
        private static readonly Vector2 EmptyPosition = new Vector2 { X = -1, Y = -1 };
        private const int DebugTextQueueMaxSize = 25;
        private const int DebugTextMaxLifetime = 30000; // 30 Seconds

        private readonly CanvasStrokeStyle _outlineDebugStrokeStyle;
        private readonly CanvasTextFormat _debugTextFormat;
        private readonly Queue<DebugTextInfo> _debugTextQueue;
        private Timer _debugTextInvalidationTimer;
        private readonly object _debugTextQueueSyncRoot = new object();

        public CanvasDrawingSession GfxSession { get; set; }

        public DebugRenderer()
        {
            _outlineDebugStrokeStyle = new CanvasStrokeStyle {
                DashStyle = CanvasDashStyle.Dash,
                DashOffset = 2.0f
            };

            _debugTextFormat = new CanvasTextFormat {
                FontSize = 12,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Center,
                WordWrapping = CanvasWordWrapping.NoWrap,
                LineSpacing = 2.0f
            };

            _debugTextQueue = new Queue<DebugTextInfo>(DebugTextQueueMaxSize);
            _debugTextInvalidationTimer = new Timer(OnDebugTextInvalidationTimerCallback, null, 
                TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(1000));
        }

        public static DebugRenderer Instance
        {
            get
            {
                if (_debugRenderer == null)
                    _debugRenderer = new DebugRenderer();

                return _debugRenderer;
            }
        }

        public override void Draw(CanvasDrawingSession g)
        {
            if (!DebugRendererEnabled)
                return;

            var nextY = 225;

            if (_debugTextQueue.Count > 0)
            {
                g.FillRoundedRectangle(18, nextY - 22, 750, 385, 12, 12, Color.FromArgb(175, 0, 0, 0));
                g.DrawRoundedRectangle(18, nextY - 22, 750, 385, 12, 12, Colors.LightGray);

                lock (_debugTextQueueSyncRoot)
                {
                    foreach (var text in _debugTextQueue)
                    {
                        g.DrawText(String.Format("[{0}] - {1}", text.AddedTime.TimeOfDay, text.Text),
                            25, nextY, Colors.Yellow, _debugTextFormat);

                        nextY += 15;
                    }
                }
            }
        }

        public void DrawDebugText(string text)
        {
            DrawDebugText(text, EmptyPosition, Colors.Yellow);
        }

        public void DrawDebugText(string text, Vector2 pos)
        {
            DrawDebugText(text, pos, Colors.Yellow);
        }

        public void DrawDebugText(string text, Vector2 pos, Color color)
        {
            if (!DebugRendererEnabled)
                return;

            if (pos.Equals(EmptyPosition))
            {
                lock (_debugTextQueueSyncRoot)
                {
                    _debugTextQueue.Enqueue(new DebugTextInfo(text));
                    if (_debugTextQueue.Count > DebugTextQueueMaxSize - 1)
                        _debugTextQueue.Dequeue();
                }

                return;
            }

            GfxSession.DrawText(text, pos, color, _debugTextFormat);
        }

        public void DrawRectOutline(params Rect[] rects)
        {
            if (!DebugRendererEnabled)
                return;

            if (!OutlineLayoutAreas)
                return;

            foreach (var rc in rects)
            {
                GfxSession.DrawRectangle(rc, Colors.OrangeRed, 1.14f, _outlineDebugStrokeStyle);
            }
        }

        private void OnDebugTextInvalidationTimerCallback(object state)
        {
            if (!DebugRendererEnabled)
                return;

            lock (_debugTextQueueSyncRoot)
            {
                var now = DateTime.Now;

                while (_debugTextQueue.Count > 0)
                {
                    var item = _debugTextQueue.Peek();
                    var lifetime = now - item.AddedTime;

                    if (lifetime.TotalMilliseconds < DebugTextMaxLifetime)
                        break;

                    _debugTextQueue.Dequeue();
                }
            }
        }
    }
}
