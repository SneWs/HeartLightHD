using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using HeartLight.Game.Objects;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;

namespace HeartLight.Game
{
    public class Level : GameComponent
    {
        private const double MoveObjectDelaySeconds = 0.45;

        private readonly Block[] _blocks;
        private readonly LevelData _levelData;
        private readonly Dictionary<int, CanvasBitmap> _objectTypeImages;
        private readonly Dictionary<int, ISoundEffect> _objectSoundEffects;

        private Player _player;
        private Direction _playerDirection = Direction.None;
        private Rect _targetRect;
        private float _targetBlockSize;

        // Effects
        private float _bombSaturationMultiplier = 0.0025f;
        private float _bombExplosionSaturationMultiplier = 0.0050f;

        public Level(LevelData levelData, ref Dictionary<int, CanvasBitmap> objectTypeImages, ref Dictionary<int, ISoundEffect> objectSoundEffects)
            : base(false)
        {
            _levelData = levelData;
            _objectTypeImages = objectTypeImages;
            _objectSoundEffects = objectSoundEffects;
            _blocks = levelData.Data;
            _targetRect = new Rect(10, 10, 100, 100);
        }

        public Player Player
        {
            get { return _player; }
            set { _player = value; }
        }

        public CanvasBitmap PlayerLeftImage { get; set; }
        public CanvasBitmap PlayerRightImage { get; set; }
        public CanvasBitmap ActivePlayerImage { get; private set; }

        public Block[] Blocks
        {
            get { return _blocks; }
        }

        public SaturationEffect BombExplosionEffect { get; set; }

        public SaturationEffect BombEffect { get; set; }

        public void UpdatePlayerStateToDead()
        {
            _blocks[_player.CurrentPosition].Type = ObjectTypes.BombExplode;
            _blocks[_player.CurrentPosition].When = DateTime.UtcNow.AddSeconds(2);

            _objectSoundEffects[ObjectTypes.BombExplode].Play();
        }

        public override void Update(UpdateInfo updateInfo)
        {
            UpdateVisualConstraints(updateInfo);

            if (_player != null)
            {
                ValidateBlockMovement(_player.CurrentPosition);
                MoveBlocks();
            }

            // Effects
            UpdateBombEffect(updateInfo);
            UpdateBombExplosionEffect(updateInfo);
        }

        private void UpdateBombEffect(UpdateInfo updateInfo)
        {
            var newSaturation = BombEffect.Saturation + _bombSaturationMultiplier * (float)updateInfo.ElapsedTime.TotalMilliseconds;
            if (newSaturation >= 1.0f)
            {
                newSaturation = 1.0f;
                _bombSaturationMultiplier = -_bombSaturationMultiplier;
            }
            else if (newSaturation <= 0.0f)
            {
                newSaturation = 0.0f;
                _bombSaturationMultiplier = -_bombSaturationMultiplier;
            }

            BombEffect.Saturation = newSaturation;
            BombEffect.Source = _objectTypeImages[ObjectTypes.Bomb];
        }

        private void UpdateBombExplosionEffect(UpdateInfo updateInfo)
        {
            var newSaturation = BombExplosionEffect.Saturation + _bombExplosionSaturationMultiplier * (float)updateInfo.ElapsedTime.TotalMilliseconds;
            if (newSaturation >= 1.0f)
            {
                newSaturation = 1.0f;
                _bombSaturationMultiplier = -_bombExplosionSaturationMultiplier;
            }
            else if (newSaturation <= 0.0f)
            {
                newSaturation = 0.0f;
                _bombExplosionSaturationMultiplier = -_bombExplosionSaturationMultiplier;
            }

            BombExplosionEffect.Saturation = newSaturation;
            BombExplosionEffect.Source = _objectTypeImages[ObjectTypes.BombExplode];
        }

        private void MoveBlocks()
        {
            for (var i = 0; i < _blocks.Length; i++)
            {
                // Remove expired exploded bomb blocks
                if (_blocks[i].Type == ObjectTypes.BombExplode)
                {
                    if (DateTime.UtcNow > _blocks[i].When)
                        _blocks[i].Type = ObjectTypes.Empty;
                }

                // Start moving blocks based on direction(s)
                if (_blocks[i].Move == Direction.None)
                    continue;

                int moveToIndex;
                switch (_blocks[i].Move)
                {
                    case Direction.Down:
                        moveToIndex = i + _levelData.Columns;
                        break;

                    case Direction.Right:
                        moveToIndex = i + 1;
                        break;

                    case Direction.Left:
                        moveToIndex = i - 1;
                        break;

                    default:
                        continue;
                }

                if (_blocks[moveToIndex].Type == ObjectTypes.Player)
                {
                    continue;
                }

                if (_blocks[moveToIndex].Type == ObjectTypes.Door ||
                    _blocks[moveToIndex].Type == ObjectTypes.DoorEnter)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if (now < _blocks[i].When)
                {
                    DebugRenderer.Instance.DrawDebugText(String.Format("Skipping to move {0} with expiry time {1}", 
                        i, _blocks[i].When.ToString("O", CultureInfo.InvariantCulture)));

                    continue;
                }

                if (_blocks[i].Type == ObjectTypes.Stone || _blocks[i].Type == ObjectTypes.Bomb)
                {
                    if (_blocks[moveToIndex].Type == ObjectTypes.Empty)
                    {
                        _blocks[moveToIndex].Type = _blocks[i].Type;
                        _blocks[moveToIndex].Move = _blocks[moveToIndex].ValidDirections = Direction.Down;
                        _blocks[moveToIndex].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds);

                        _blocks[i].Type = ObjectTypes.Empty;
                        _blocks[i].Move = Direction.None;
                        _blocks[i].ValidDirections = Direction.None;

                        // Did we move a stone?
                        if (_blocks[moveToIndex].Type == ObjectTypes.Stone)
                            PlayStoneMovedSoundEffect();
                    }
                    else if ((_blocks[moveToIndex].Type == ObjectTypes.SolidWall || 
                              _blocks[moveToIndex].Type == ObjectTypes.BrickWall ||
                              _blocks[moveToIndex].Type == ObjectTypes.Stone) && _blocks[i].Type == ObjectTypes.Bomb)
                    {
                        PlayBombExplodedSoundEffect();

                        // Let's destroy some shit around the bomb

                        const int multiplier = 3;

                        // The block below the bomb
                        if (IsBlockPlayerBlock(moveToIndex))
                            _player.CommitSuicide();
                        _blocks[moveToIndex].Type = ObjectTypes.BombExplode;
                        _blocks[moveToIndex].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds * multiplier);

                        // The actual bomb
                        if (IsBlockPlayerBlock(i))
                            _player.CommitSuicide();
                        _blocks[i].Type = ObjectTypes.BombExplode;
                        _blocks[i].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds * multiplier);

                        // The block above the bomb
                        if (IsBlockPlayerBlock(i - _levelData.Columns))
                            _player.CommitSuicide();
                        _blocks[i - _levelData.Columns].Type = ObjectTypes.BombExplode;
                        _blocks[i - _levelData.Columns].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds * multiplier);

                        // Left of bomb
                        if (IsBlockPlayerBlock(i - 1))
                            _player.CommitSuicide();
                        _blocks[i - 1].Type = ObjectTypes.BombExplode;
                        _blocks[i - 1].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds * multiplier);

                        // Right of bomb
                        if (IsBlockPlayerBlock(i + 1))
                            _player.CommitSuicide();
                        _blocks[i + 1].Type = ObjectTypes.BombExplode;
                        _blocks[i + 1].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds * multiplier);
                    }
                }
            }
        }

        private bool IsBlockPlayerBlock(int index)
        {
            return _blocks[index].Type == ObjectTypes.Player;
        }

        private void ValidateBlockMovement(int position)
        {
            if (position < 1 || position >= _blocks.Length)
                return;

            for (var i = _blocks.Length - 1; i >= 0; i--)
            {
                var type = _blocks[i].Type;
                if (type != ObjectTypes.Empty)
                    continue;

                var pos = i - _levelData.Columns;
                if (pos < 0)
                    continue;

                if (_blocks[pos].Type == ObjectTypes.Stone ||
                    _blocks[pos].Type == ObjectTypes.Heart ||
                    _blocks[pos].Type == ObjectTypes.Bomb)
                {
                    // Move the stone down
                    _blocks[i].Type = _blocks[pos].Type;
                    _blocks[i].Move = Direction.Down;
                    _blocks[i].ValidDirections = Direction.Down;
                    _blocks[i].When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds);

                    _blocks[pos].Type = ObjectTypes.Empty;
                    _blocks[pos].ValidDirections = _blocks[pos].Move = Direction.None;
                }
            }

            // Check right of player
            var rightPos = position + 1;
            if (rightPos < _blocks.Length)
            {
                if ((_blocks[rightPos].ValidDirections & Direction.Left) > 0)
                {
                    var right = _blocks[rightPos];
                    if (right.Type == ObjectTypes.Stone)
                    {
                        if (_blocks[position].Type == ObjectTypes.Player && _blocks[rightPos - _levelData.Columns].Type != ObjectTypes.Stone)
                        {
                            if (_playerDirection != Direction.Left)
                            {
                                right.Move = Direction.Left;
                                right.ValidDirections = Direction.Left | Direction.Down;
                                right.When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds);
                            }
                        }
                    }
                }
            }

            // Check left of player
            var leftPos = position - 1;
            if (leftPos >= 0)
            {
                if ((_blocks[leftPos].ValidDirections & Direction.Right) > 0)
                {
                    var left = _blocks[leftPos];
                    if (left.Type == ObjectTypes.Stone)
                    {
                        if (_blocks[position].Type == ObjectTypes.Player && 
                            _blocks[rightPos - _levelData.Columns].Type != ObjectTypes.Stone)
                        {
                            if (_playerDirection != Direction.Right)
                            {
                                left.Move = Direction.Right;
                                left.ValidDirections = Direction.Right | Direction.Down;
                                left.When = DateTime.UtcNow.AddSeconds(MoveObjectDelaySeconds);
                            }
                        }
                    }
                }
            }

        }

        private void PlayStoneMovedSoundEffect()
        {
            _objectSoundEffects[ObjectTypes.Stone].Play();
        }

        private void PlayBombExplodedSoundEffect()
        {
            _objectSoundEffects[ObjectTypes.BombExplode].Play();
        }

        public override void Draw(CanvasDrawingSession g)
        {
            var nextX = _targetRect.Left;
            var nextY = _targetRect.Top;

            var currentRow = 0;
            var currentColumn = 0;
            for (var i = 0; i < _blocks.Length; i++)
            {
                if (currentColumn >= _levelData.Columns)
                {
                    ++currentRow;
                    currentColumn = 0;
                    nextY += _targetBlockSize;
                    nextX = _targetRect.Left;
                }

                nextX += _targetBlockSize;
                ++currentColumn;

                var blockRc = new Rect(nextX, nextY, _targetBlockSize, _targetBlockSize);
                if (_blocks[i].Type == ObjectTypes.SolidWall)
                {
                    DrawWallBlock(g, blockRc);
                }
                else if (_blocks[i].Type == ObjectTypes.BrickWall)
                {
                    DrawBrickWallBlock(g, blockRc);
                }
                else if (_blocks[i].Type == ObjectTypes.Empty)
                {
                    DrawEmptyBlock(g, blockRc);
                }
                else if (_blocks[i].Type == ObjectTypes.Grass)
                {
                    DrawGrassBlock(g, blockRc);
                }
                else if (_blocks[i].Type == ObjectTypes.Stone)
                {
                    DrawStoneBlock(g, blockRc, i);
                }
                else if (_blocks[i].Type == ObjectTypes.Heart)
                {
                    DrawHeartBlock(g, blockRc);
                }
                else if (_blocks[i].Type == ObjectTypes.Door || _blocks[i].Type == ObjectTypes.DoorEnter)
                {
                    var isOpen = _blocks[i].Type == ObjectTypes.DoorEnter;
                    DrawDoorBlock(g, blockRc, i, isOpen);
                }
                else if (_blocks[i].Type == ObjectTypes.Player)
                {
                    DrawPlayer(g, blockRc);
                }
                else if (_blocks[i].Type == ObjectTypes.Bomb)
                {
                    DrawBombBlock(g, blockRc, i);
                }
                else if (_blocks[i].Type == ObjectTypes.BombExplode)
                {
                    DrawBombExplosionBlock(g, blockRc, i);
                }
            }
        }

        public override void OnKeyDown(KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Right)
            {
                _playerDirection = Direction.Right;
                ActivePlayerImage = PlayerRightImage;
            }
            else if (args.VirtualKey == VirtualKey.Left)
            {
                _playerDirection = Direction.Left;
                ActivePlayerImage = PlayerLeftImage;
            }
            else if (args.VirtualKey == VirtualKey.Up)
            {
                _playerDirection = Direction.Up;
            }
            else if (args.VirtualKey == VirtualKey.Down)
            {
                _playerDirection = Direction.Down;
            }
            
        }

        private void DrawPlayer(CanvasDrawingSession g, Rect blockRc)
        {
            if (ActivePlayerImage == null)
                ActivePlayerImage = PlayerRightImage;

            DrawBlackBlock(g, blockRc);
            g.DrawImage(ActivePlayerImage, blockRc);
        }

        private void DrawHeartBlock(CanvasDrawingSession g, Rect blockRc)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_objectTypeImages[ObjectTypes.Heart], blockRc);
        }

        private void DrawDoorBlock(CanvasDrawingSession g, Rect blockRc, int index, bool isOpen)
        {
            DrawBlackBlock(g, blockRc);

            var type = (isOpen ? ObjectTypes.DoorEnter : ObjectTypes.Door);
            g.DrawImage(_objectTypeImages[type], blockRc);

            if (DebugRenderer.DebugRendererEnabled)
            {
                var textFormat = new CanvasTextFormat
                {
                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                };

                g.DrawText(index.ToString(), blockRc, Colors.Black, textFormat);
            }
        }

        private void DrawStoneBlock(CanvasDrawingSession g, Rect blockRc, int index)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_objectTypeImages[ObjectTypes.Stone], blockRc);

            if (DebugRenderer.DebugRendererEnabled)
            {
                var textFormat = new CanvasTextFormat {
                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                };

                g.DrawText(index.ToString(), blockRc, Colors.MediumVioletRed, textFormat);
            }
        }

        private void DrawGrassBlock(CanvasDrawingSession g, Rect blockRc)
        {
            DrawBlackBlock(g, blockRc);
            g.DrawImage(_objectTypeImages[ObjectTypes.Grass], blockRc);
        }

        private void DrawWallBlock(CanvasDrawingSession g, Rect blockRc)
        {
            g.DrawImage(_objectTypeImages[ObjectTypes.SolidWall], blockRc);
        }

        private void DrawBrickWallBlock(CanvasDrawingSession g, Rect blockRc, bool useActiveBlockEffect = false)
        {
            g.DrawImage(_objectTypeImages[ObjectTypes.BrickWall], blockRc);
        }

        private void DrawBlackBlock(CanvasDrawingSession g, Rect blockRect)
        {
            g.FillRectangle(blockRect, Colors.Black);
        }

        private void DrawEmptyBlock(CanvasDrawingSession g, Rect blockRc)
        {
            DrawBlackBlock(g, blockRc);
        }

        private void DrawBombBlock(CanvasDrawingSession g, Rect blockRc, int index, bool useActiveBlockEffect = false)
        {
            DrawBlackBlock(g, blockRc);
            BombEffect.Source = _objectTypeImages[ObjectTypes.Bomb];
            g.DrawImage(BombEffect, blockRc, _objectTypeImages[ObjectTypes.Bomb].Bounds);

            if (DebugRenderer.DebugRendererEnabled)
            {
                var textFormat = new CanvasTextFormat {
                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                    VerticalAlignment = CanvasVerticalAlignment.Center
                };

                g.DrawText(index.ToString(), blockRc, Colors.White, textFormat);
            }
        }

        private void DrawBombExplosionBlock(CanvasDrawingSession g, Rect blockRc, int index)
        {
            DrawBlackBlock(g, blockRc);

            BombExplosionEffect.Source = _objectTypeImages[ObjectTypes.BombExplode];
            g.DrawImage(BombExplosionEffect, blockRc, _objectTypeImages[ObjectTypes.BombExplode].Bounds);
        }

        private void UpdateVisualConstraints(UpdateInfo updateInfo)
        {
            var width = updateInfo.WindowWidth * 0.85f;
            var height = updateInfo.WindowHeight * 0.70f;

            _targetRect.X = updateInfo.WindowWidth / 2 - width / 2;
            _targetRect.Y = updateInfo.WindowHeight / 2 - height / 2;
            _targetRect.Width = width;
            _targetRect.Height = height;

            _targetBlockSize = (float)Math.Ceiling(width / _levelData.Columns);
            _targetRect.X -= _targetBlockSize;
            _targetRect.Y -= _targetBlockSize;
        }
    }
}
