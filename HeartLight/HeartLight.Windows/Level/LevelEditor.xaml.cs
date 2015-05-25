using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using HeartLight.Game;
using HeartLight.Game.Objects;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Numerics;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace HeartLight.Level
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LevelEditor
    {
        private float _windowWidth;
        private float _windowHeight;
        private CanvasLinearGradientBrush _menuBackgroundBrush;
        private readonly Dictionary<int, CanvasBitmap> _objectTypeImages;

        private LevelData _levelData;
        private Block[] _blocks;
        private Rect[] _blockRects; // Used for hit testing
        private Rect _targetRect;
        private float _targetBlockSize;
        private CanvasBitmap _playerLeft;
        private CanvasBitmap _playerRight;
        private bool _isMoving;
        private int _activeBlockIndex;
        private int _activeObjectType;
        private SaturationEffect _activeBlockEffect;
        private float _activeBlockSaturationMultiplier = 0.005f;
        private ShadowEffect _shadowEffect;

        public LevelEditor()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;

            _objectTypeImages = new Dictionary<int, CanvasBitmap>(10);
            _targetRect = new Rect(10, 10, 100, 100);
            _blockRects = new Rect[0];
            OnNumberOfBlocksChanged(this, null);

            _activeObjectType = ObjectTypes.SolidWall;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.Current.CoreWindow;
            //window.PointerPressed += OnWindowPointerPressed;
            //window.PointerReleased += OnWindowPointerReleased;
            //window.PointerMoved += OnWindowPointerMoved;
            window.KeyDown += OnWindowKeyDown;
            window.KeyUp += OnWindowKeyUp;

            window.SetPointerCapture();
            

            GrabWindowSize();
            Window.Current.CoreWindow.SetPointerCapture();
        }

        private void GrabWindowSize()
        {
            _windowWidth = (float)CanvasArea.ActualWidth;
            _windowHeight = (float)CanvasArea.ActualHeight;
        }

        private void OnCanvasCreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 101); // Custom ARROW pointer

            // Main menu background
            var stops = new[] {
                new CanvasGradientStop { Color = Color.FromArgb(205, 245, 245, 245), Position = 0.0f },
                new CanvasGradientStop { Color = Color.FromArgb(205, 245, 245, 245), Position = 0.50f },
                new CanvasGradientStop { Color = Color.FromArgb(205, 133, 133, 133), Position = 0.72f},
                new CanvasGradientStop { Color = Color.FromArgb(205, 245, 245, 245), Position = 0.92f},
                new CanvasGradientStop { Color = Color.FromArgb(205, 245, 245, 245), Position = 1.0f },
            };

            _menuBackgroundBrush = new CanvasLinearGradientBrush(CanvasArea, stops, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied) {
                StartPoint = new Vector2 { X = 0, Y = 0 },
                EndPoint = new Vector2 { X = 0, Y = _windowHeight }
            };

            args.TrackAsyncAction(LoadImagesAsync(sender).AsAsyncAction());

            _activeBlockEffect = new SaturationEffect {
                Saturation = 0.8f
            };

            _shadowEffect = new ShadowEffect {
                BlurAmount = 4.0f,
                ShadowColor = Colors.DimGray,
                Optimization = EffectOptimization.Quality,
            };
        }

        private void OnCanvasUpdate(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.High, GrabWindowSize);
            UpdateVisualConstraints(new UpdateInfo(TimeSpan.Zero, _windowWidth, _windowHeight));

            var newSaturation = _activeBlockEffect.Saturation + _activeBlockSaturationMultiplier * (float)args.Timing.ElapsedTime.TotalMilliseconds;
            if (newSaturation >= 1.0f)
            {
                newSaturation = 1.0f;
                _activeBlockSaturationMultiplier = -_activeBlockSaturationMultiplier;
            }
            else if (newSaturation <= 0.0f)
            {
                newSaturation = 0.0f;
                _activeBlockSaturationMultiplier = -_activeBlockSaturationMultiplier;
            }

            _activeBlockEffect.Saturation = newSaturation;
        }

        private void OnCanvasDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            using (var g = args.DrawingSession)
            {
                g.TextAntialiasing = CanvasTextAntialiasing.ClearType;
                g.Antialiasing = CanvasAntialiasing.Antialiased;
                g.Blend = CanvasBlend.SourceOver;

                DebugRenderer.Instance.GfxSession = g;
                g.FillRectangle(0, 0, _windowWidth, _windowHeight, _menuBackgroundBrush);

                var bottomLineY = _windowHeight - 2;
                g.DrawLine(0, bottomLineY, _windowWidth, bottomLineY, Colors.White);

                var nextX = _targetRect.X; // ((_windowWidth / 2) - _targetRect.Width / 2) - (_levelData.Columns / 2.0f) * _targetBlockSize;
                var nextY = _targetRect.Y + 10.0f; //((_windowHeight / 2) - _targetRect.Height / 2) - (_levelData.Rows / 2.0f) * _targetBlockSize;

                var currentRow = 0;
                var currentColumn = 0;
                for (var i = 0; i < _blocks.Length; i++)
                {
                    if (currentColumn >= _levelData.Columns)
                    {
                        ++currentRow;
                        currentColumn = 0;
                        nextY += _targetBlockSize + 1.0f;
                        nextX = _targetRect.Left;
                    }

                    nextX += _targetBlockSize + 1.0f;
                    ++currentColumn;

                    var blockRc = _blockRects[i];
                    DrawBasedOnType(g, _blocks[i].Type, blockRc, i);

                    g.DrawText(i.ToString(CultureInfo.InvariantCulture), _blockRects[i], Colors.Red, new CanvasTextFormat());
                }

                if (_activeBlockIndex >= 0 && _activeBlockIndex < _blocks.Length && _activeObjectType >= ObjectTypes.Empty)
                {
                    //DrawBasedOnType(g, _activeObjectType, _blockRects[_activeBlockIndex], _activeBlockIndex, true);

                    if (_objectTypeImages.ContainsKey(_activeObjectType))
                    {
                        _activeBlockEffect.Source = _objectTypeImages[_activeObjectType];
                        g.DrawImage(_activeBlockEffect, _blockRects[_activeBlockIndex], _objectTypeImages[_activeObjectType].Bounds);

                        _shadowEffect.Source = _objectTypeImages[_activeObjectType];
                    }

                    var strokeStyle = new CanvasStrokeStyle();
                    strokeStyle.DashStyle = CanvasDashStyle.DashDotDot;
                    g.DrawRectangle(_blockRects[_activeBlockIndex], Colors.OrangeRed, 2.0f, strokeStyle);
                }

                // Draw a line around the actual workable area
                g.DrawRectangle(_targetRect, Colors.White);
            }
        }

        private void DrawBasedOnType(CanvasDrawingSession g, int type, Rect blockRc, int index, bool useActiveBlockEffect = false)
        {
            if (type == ObjectTypes.SolidWall)
            {
                DrawWallBlock(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.BrickWall)
            {
                DrawBrickWallBlock(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Empty)
            {
                DrawEmptyBlock(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Grass)
            {
                DrawGrassBlock(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Stone)
            {
                DrawStoneBlock(g, blockRc, index, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Heart)
            {
                DrawHeartBlock(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Door)
            {
                DrawDoorBlock(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Player)
            {
                DrawPlayer(g, blockRc, useActiveBlockEffect);
            }
            else if (type == ObjectTypes.Bomb)
            {
                DrawBombBlock(g, blockRc, useActiveBlockEffect);
            }
        }

        private void DrawPlayer(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_playerRight, blockRc);
        }

        private void DrawHeartBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_objectTypeImages[ObjectTypes.Heart], blockRc);
        }

        private void DrawDoorBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            g.DrawImage(_objectTypeImages[ObjectTypes.Door], blockRc);
        }

        private void DrawStoneBlock(CanvasDrawingSession g, Rect blockRc, int i, bool useActiveBlockEffect = false)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_objectTypeImages[ObjectTypes.Stone], blockRc);

            if (DebugRenderer.DebugRendererEnabled)
            {
                var textFormat = new CanvasTextFormat {
                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                };

                g.DrawText(i.ToString(), blockRc, Colors.Black, textFormat);
            }
        }

        private void DrawGrassBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_objectTypeImages[ObjectTypes.Grass], blockRc);
        }

        private void DrawWallBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            g.DrawImage(_objectTypeImages[ObjectTypes.SolidWall], blockRc);
        }

        private void DrawBrickWallBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            g.DrawImage(_objectTypeImages[ObjectTypes.BrickWall], blockRc);
        }

        private void DrawBlackBlock(CanvasDrawingSession g, Rect blockRect, bool useActiveBlockEffect = false)
        {
            g.FillRectangle(blockRect, Colors.Black);
        }

        private void DrawEmptyBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            g.FillRectangle(blockRc, Colors.Black);
        }

        private void DrawBombBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            g.DrawImage(_objectTypeImages[ObjectTypes.Bomb], blockRc);
        }

        private void UpdateVisualConstraints(UpdateInfo updateInfo)
        {
            var width = updateInfo.WindowWidth * 0.92f;
            var height = updateInfo.WindowHeight * 0.85f;

            _targetRect.X = (_windowWidth / 2) - (width / 2);
            _targetRect.Y = (_windowHeight / 2) - (height / 2);
            _targetRect.Width = width;
            _targetRect.Height = height;

            _targetBlockSize = (float)Math.Ceiling(width / _levelData.Columns);
            if (_targetBlockSize > 92.0f)
                _targetBlockSize = 92.0f;

            //_targetRect.X -= _targetBlockSize;
            //_targetRect.Y -= _targetBlockSize;

            var nextX = _targetRect.X;
            var nextY = _targetRect.Y;// + 10.0f;

            var currentColumn = 0;
            for (var i = 0; i < _blocks.Length; i++)
            {
                if (currentColumn >= _levelData.Columns)
                {
                    currentColumn = 0;
                    nextY += _targetBlockSize + 1.0f;
                    nextX = _targetRect.Left;
                }

                _blockRects[i] = new Rect(nextX, nextY, _targetBlockSize, _targetBlockSize);

                nextX += _targetBlockSize + 1.0f;
                ++currentColumn;
            }

            _targetRect = new Rect(_targetRect.X, _targetRect.Y, 
                (_levelData.Columns * _targetBlockSize) + _levelData.Columns * 1.0f, 
                (_levelData.Rows * _targetBlockSize) + _levelData.Rows * 1.0f);
        }

        private async Task LoadImagesAsync(CanvasAnimatedControl ctrl)
        {
            _objectTypeImages.Clear();

            _playerLeft = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Player_Left.png");
            _playerRight = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Player_Right.png");

            var stoneWallImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/StoneWall_1024x1024.png");
            var brickWallImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/BrickWall_1024x1024.png");
            var heartImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Heart_1024x1024.png");
            var grassImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Grass_1024x1024.png");
            var stoneImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Stone_1024x1024.png");
            var bombImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Bomb_1024x1024.png");
            var doorImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Door_1024x1024.png");

            _objectTypeImages.Add(ObjectTypes.SolidWall, stoneWallImage);
            _objectTypeImages.Add(ObjectTypes.BrickWall, brickWallImage);
            _objectTypeImages.Add(ObjectTypes.Heart, heartImage);
            _objectTypeImages.Add(ObjectTypes.Grass, grassImage);
            _objectTypeImages.Add(ObjectTypes.Stone, stoneImage);
            _objectTypeImages.Add(ObjectTypes.Bomb, bombImage);
            _objectTypeImages.Add(ObjectTypes.Door, doorImage);
            _objectTypeImages.Add(ObjectTypes.DoorEnter, doorImage);
        }

        private bool _isCtrlDown = false;
        private void OnWindowKeyDown(CoreWindow sender, KeyEventArgs e)
        {
            if (e.VirtualKey == VirtualKey.Control)
            {
                e.Handled = true;
                _isCtrlDown = true;

                return;
            }

            var offset = 0;

            if (_isCtrlDown)
            {
                if (e.VirtualKey == VirtualKey.Up)
                    offset = -1;
                else if (e.VirtualKey == VirtualKey.Down)
                    offset = 1;

                if (offset != 0)
                {
                    e.Handled = true;

                    var newObjectType = _activeObjectType += offset;
                    if (newObjectType < ObjectTypes.Ignore)
                        newObjectType = ObjectTypes.Player;
                    else if (newObjectType > ObjectTypes.LastType)
                        newObjectType = ObjectTypes.Empty;

                    _activeObjectType = newObjectType;
                }

                return;
            }

            if (e.VirtualKey == VirtualKey.Left)
                offset = -1;
            else if (e.VirtualKey == VirtualKey.Right)
                offset = 1;
            else if (e.VirtualKey == VirtualKey.Up)
                offset = -_levelData.Columns;
            else if (e.VirtualKey == VirtualKey.Down)
                offset = _levelData.Columns;

            var newIndex = (_activeBlockIndex + offset);
            if (newIndex >= 0 && newIndex < _blocks.Length && newIndex != _activeBlockIndex)
            {
                e.Handled = true;
                _activeBlockIndex = newIndex;
            }
        }

        private void OnWindowKeyUp(CoreWindow sender, KeyEventArgs e)
        {
            if (e.VirtualKey == VirtualKey.Control)
            {
                e.Handled = true;
                _isCtrlDown = false;

                return;
            }

            if (e.VirtualKey == VirtualKey.Enter || e.VirtualKey == VirtualKey.Space)
            {
                e.Handled = true;
                if (_activeObjectType >= ObjectTypes.Ignore && _activeObjectType <= ObjectTypes.LastType)
                {
                    if (_activeBlockIndex >= 0 && _activeBlockIndex <= _blocks.Length)
                        _blocks[_activeBlockIndex].Type = _activeObjectType;
                }
            }
        }

        //private void OnWindowPointerPressed(CoreWindow sender, PointerEventArgs e)
        //{
        //    var pointData = e.CurrentPoint;
        //    for (var i = 0; i < _blocks.Length; i++)
        //    {
        //        if (_blockRects[i].Contains(pointData.Position))
        //        {
        //            _isMoving = false;
        //            _activeBlockIndex = i;
        //            _activeObjectType = _blocks[i].Type;

        //            break;
        //        }
        //    }

        //    if (e.KeyModifiers == VirtualKeyModifiers.Control)
        //    {
        //        BlockTypesMenu.IsOpen = true;
        //    }

        //    e.Handled = true;
        //}

        //private void OnWindowPointerReleased(CoreWindow sender, PointerEventArgs e)
        //{
        //    _isMoving = false;

        //    var pointData = e.CurrentPoint;
        //    for (var i = 0; i < _blocks.Length; i++)
        //    {
        //        if (_blockRects[i].Contains(pointData.Position))
        //        {
        //            _activeBlockIndex = i;
        //            break;
        //        }
        //    }

        //    e.Handled = true;
        //}

        //private void OnWindowPointerMoved(CoreWindow sender, PointerEventArgs e)
        //{
        //    var pointData = e.CurrentPoint;

        //    if (_isMoving)
        //    {
        //        for (var i = 0; i < _blocks.Length; i++)
        //        {
        //            if (_blockRects[i].Contains(pointData.Position))
        //            {
        //                _activeBlockIndex = i;
        //                break;
        //            }
        //        }
        //    }

        //    e.Handled = true;
        //}

        private void BackgroundVideo_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        private void OnNumberOfBlocksChanged(object sender, TextChangedEventArgs e)
        {
            int columns;
            int rows;

            if (!int.TryParse(BlockWidth.Text, out columns))
                columns = 20;

            if (!int.TryParse(BlockHeight.Text, out rows))
                rows = 12;

            _blocks = new Block[columns * rows];
            _blockRects = new Rect[_blocks.Length];
            for (var i = 0; i < _blocks.Length; i++)
            {
                _blocks[i] = new Block(ObjectTypes.Ignore);
                _blockRects[i] = new Rect(10, 10, 50, 50);
            }

            _levelData = new LevelData(columns, rows, _blocks, 5, 0, 
                DateTime.UtcNow, AuthorEmail.Text, AuthorName.Text);
        }

        private async void OnSaveLevel(object sender, RoutedEventArgs e)
        {
            var lines = new List<string>();

            _levelData.Email = AuthorEmail.Text.Trim(" \t\n,.|".ToCharArray());
            _levelData.FullName = AuthorName.Text.Trim(" \t\n,.|".ToCharArray());

            // Header line
            // lvl,20,12,2015-05-12 20:45,marcus@grenangen.se,Marcus Grenängen
            lines.Add(String.Format("lvl,{0},{1},{2},{3},{4}", 
                _levelData.Columns, _levelData.Rows, _levelData.Created.ToString("s", CultureInfo.InvariantCulture), _levelData.Email, _levelData.FullName));

            var line = "";
            var columnCount = 0;
            foreach (var block in _blocks)
            {
                line += block.Type.ToString(CultureInfo.InvariantCulture);
                ++columnCount;
                if (columnCount >= _levelData.Columns)
                {
                    lines.Add(line);
                    line = "";
                    columnCount = 0;
                }
            }
            
            if (!String.IsNullOrEmpty(line))
                lines.Add(line);

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("Level", new List<string> { ".lvl" });
            var result = await savePicker.PickSaveFileAsync();
            if (result != null)
            {
                await FileIO.WriteLinesAsync(result, lines);
            }
        }

        private async void OnOpenLevel(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".lvl");
            
            var result = await openPicker.PickSingleFileAsync();
            if (result != null)
            {
                var loader = new LevelLoader(result);
                var levelResult = await loader.LoadLevel();
                if (levelResult.Succeeded)
                {
                    _levelData = levelResult.Data;
                    _blocks = _levelData.Data;
                    _blockRects = new Rect[_blocks.Length];
                    for (var i = 0; i < _blockRects.Length; i++)
                        _blockRects[i] = new Rect(10, 10, 50, 50);

                    _activeBlockIndex = _levelData.PlayerStartPosition;
                    _activeObjectType = ObjectTypes.Player;

                    CanvasArea.Focus(FocusState.Programmatic);
                }
            }
        }

        private void OnSolidWallBlockSelected(object sender, RoutedEventArgs e)
        {
            if (_activeBlockIndex >= 0 && _activeBlockIndex < _blocks.Length)
            {
                _activeObjectType = ObjectTypes.SolidWall;
                _blocks[_activeBlockIndex].Type = _activeObjectType;
            }

            CanvasArea.Focus(FocusState.Programmatic);
        }
    }
}
