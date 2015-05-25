using System.Collections.Generic;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;

namespace HeartLight
{
    public class MenuRenderer : GameComponent
    {
        private const float AlphaMultiplierBase = 0.0020f;

        private readonly float _itemHeight = 48.0f;
        private readonly float _itemSpacing = 32.0f;
        private readonly CanvasTextFormat _textFormat;
        private readonly List<GameMenuItem> _mainMenuItems;
        private Rect _menuArea;
        private float _activeItemCurrentAlpha = 1.0f;
        private float _alphaMultiplier = AlphaMultiplierBase;

        public MenuRenderer(string fontName, float fontSize)
        {
            _itemSpacing = fontSize - 10.0f;
            _itemHeight = fontSize + 6.0f;
            _textFormat = new CanvasTextFormat {
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center,
                Options = CanvasDrawTextOptions.Clip,
                FontSize = fontSize,
                FontFamily = fontName
            };
            _mainMenuItems = new List<GameMenuItem>();
            ActiveMenuItemIndex = 0;
        }

        public int ActiveMenuItemIndex { get; set; }

        public void AddItem(GameMenuItem item)
        {
            _mainMenuItems.Add(item);
        }

        public override void OnKeyDown(KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Down)
            {
                ++ActiveMenuItemIndex;
                if (ActiveMenuItemIndex >= _mainMenuItems.Count)
                    ActiveMenuItemIndex = 0;

                _activeItemCurrentAlpha = AlphaMultiplierBase * 10;
            }
            else if (args.VirtualKey == VirtualKey.Up)
            {
                --ActiveMenuItemIndex;
                if (ActiveMenuItemIndex < 0)
                    ActiveMenuItemIndex = _mainMenuItems.Count - 1;

                _activeItemCurrentAlpha = AlphaMultiplierBase * 10;
            }
            else if (args.VirtualKey == VirtualKey.Enter)
            {
                var activeItem = _mainMenuItems[ActiveMenuItemIndex];
                if (activeItem != null)
                {
                    activeItem.TriggerActivation();
                }
            }
        }

        public override void OnPointerPressed(PointerEventArgs args)
        {
            if (!_menuArea.Contains(args.CurrentPoint.Position))
                return;

            for (var index = 0; index < _mainMenuItems.Count; index++)
            {
                var item = _mainMenuItems[index];
                if (item.Rect.Contains(args.CurrentPoint.Position))
                {
                    ActiveMenuItemIndex = index;
                    _mainMenuItems[index].TriggerActivation();
                }
            }
        }

        public override void Update(UpdateInfo info)
        {
            var menuWidth = info.WindowWidth * 0.3f; // One third of the width
            var menuHeight = info.WindowHeight * 0.42f; // Slightly less than 50% of the available height

            _menuArea = new Rect(info.WindowWidth / 2 - (menuWidth / 2),
                info.WindowHeight / 2 - (menuHeight / 2), menuWidth, menuHeight);

            _activeItemCurrentAlpha += _alphaMultiplier * info.ElapsedTime.Milliseconds;
            if (_activeItemCurrentAlpha >= 1.0f)
                _alphaMultiplier = -AlphaMultiplierBase;
            else if (_activeItemCurrentAlpha < 0.30f)
                _alphaMultiplier = AlphaMultiplierBase;

            var startX = _menuArea.Left;
            var startY = _menuArea.Top;
            for (var index = 0; index < _mainMenuItems.Count; index++)
            {
                var item = _mainMenuItems[index];
                item.Rect = new Rect(startX, startY, _menuArea.Width, _itemHeight);

                startY += _itemSpacing + _itemHeight;
            }
        }

        public override void Draw(CanvasDrawingSession g)
        {
            DebugRenderer.Instance.DrawRectOutline(_menuArea);

            for (var index = 0; index < _mainMenuItems.Count; index++)
            {
                var item = _mainMenuItems[index];
                var rect = item.Rect;

                var color = item.Color;
                if (index == ActiveMenuItemIndex)
                {
                    color = Color.FromArgb((byte)(_activeItemCurrentAlpha * 100), item.ActiveColor.R, item.ActiveColor.G, item.ActiveColor.B);

                    g.FillCircle((float)rect.Left + (float)rect.Height, 
                        (float)rect.Top + (float)rect.Height / 2.0f, (float)rect.Height / 4, item.ActiveColor);
                }

                g.DrawText(item.Text, rect, color, _textFormat);

                DebugRenderer.Instance.DrawRectOutline(rect);
            }
        }
    }
}
