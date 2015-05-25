using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using HeartLight.Game.Objects;

namespace HeartLight.Game
{
    public class Player : GameComponent, INotifyPropertyChanged
    {
        private enum MoveDirection
        {
            None,
            Left,
            Right,
            Up,
            Down
        }

        private readonly Level _currentLevel;
        private readonly LevelData _data;
        private readonly Dictionary<int, ISoundEffect> _objectSoundEffects;
        private int _currentPosition;
        private int _currentScore;
        private int _livesLeft;
        private bool _levelSolved;
        private bool _isDead;
        public event EventHandler<EventArgs> PlayerSolvedLevel = delegate { };
        public event EventHandler<EventArgs> PlayerDied = delegate { };

        public Player(Level currentLevel, LevelData data, ref Dictionary<int, ISoundEffect> objectSoundEffects)
        {
            _currentLevel = currentLevel;
            _data = data;
            _objectSoundEffects = objectSoundEffects;
            _currentPosition = data.PlayerStartPosition;
            _currentScore = 0;
            _livesLeft = 3;
        }

        public int CurrentPosition
        {
            get { return _currentPosition; }
        }

        public int Score
        {
            get { return _currentScore; }
            set
            {
                if (value != _currentScore)
                {
                    _currentScore = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Lives
        {
            get { return _livesLeft; }
        }

        public void CommitSuicide()
        {
            _isDead = true;
            _currentLevel.UpdatePlayerStateToDead();

            Task.Run(async () => {
                await Task.Delay(1000);
                WhenPlayerKilled();
            });
        }

        private void WhenPlayerKilled()
        {
            --_livesLeft;
            PlayerDied.Invoke(this, EventArgs.Empty);
        }

        private void WhenPlayerSolvedLevel()
        {
            _levelSolved = true;
            PlayerSolvedLevel.Invoke(this, EventArgs.Empty);

            _objectSoundEffects[ObjectTypes.DoorEnter].Play();
        }

        public override void OnKeyDown(KeyEventArgs args)
        {
            if (_levelSolved || _isDead)
                return;

            var dir = MoveDirection.None;
            if (args.VirtualKey == VirtualKey.Left)
            {
                args.Handled = true;
                dir = MoveDirection.Left;
            }
            else if (args.VirtualKey == VirtualKey.Right)
            {
                args.Handled = true;
                dir = MoveDirection.Right;
            }
            else if (args.VirtualKey == VirtualKey.Up)
            {
                args.Handled = true;
                dir = MoveDirection.Up;
            }
            else if (args.VirtualKey == VirtualKey.Down)
            {
                args.Handled = true;
                dir = MoveDirection.Down;
            }

            if (dir == MoveDirection.None)
                return;

            int newPos;
            if (CanMoveInDirection(dir, out newPos))
            {
                if (_currentLevel.Blocks[newPos].Type == ObjectTypes.Heart)
                {
                    _objectSoundEffects[ObjectTypes.Heart].Play();
                    ++Score;

                    if (Score >= _data.NumberOfHearts)
                    {
                        _currentLevel.Blocks[_data.DoorPosition].Type = ObjectTypes.DoorEnter;
                        _objectSoundEffects[ObjectTypes.Door].Play();
                    }
                }

                // We can only solve the level if all hearts have been collected and hence
                // the door is open.
                if (_currentLevel.Blocks[newPos].Type == ObjectTypes.DoorEnter)
                {
                    WhenPlayerSolvedLevel();
                }
                else if (_currentLevel.Blocks[newPos].Type == ObjectTypes.BombExplode)
                {
                    WhenPlayerKilled();
                }

                if (dir == MoveDirection.Left)
                {
                    if (_currentLevel.Blocks[newPos].Type == ObjectTypes.Stone || 
                        _currentLevel.Blocks[newPos].Type == ObjectTypes.Bomb)
                    {
                        _currentLevel.Blocks[newPos - 1].Type = _currentLevel.Blocks[newPos].Type;
                        _currentLevel.Blocks[newPos - 1].ValidDirections = _currentLevel.Blocks[newPos].ValidDirections;
                    }
                }
                else if (dir == MoveDirection.Right)
                {
                    if (_currentLevel.Blocks[newPos].Type == ObjectTypes.Stone ||
                        _currentLevel.Blocks[newPos].Type == ObjectTypes.Bomb)
                    {
                        _currentLevel.Blocks[newPos + 1].Type = _currentLevel.Blocks[newPos].Type;
                        _currentLevel.Blocks[newPos + 1].ValidDirections = _currentLevel.Blocks[newPos].ValidDirections;
                    }
                }

                _currentLevel.Blocks[_currentPosition].Type = ObjectTypes.Empty;
                _currentLevel.Blocks[_currentPosition].Move = _currentLevel.Blocks[_currentPosition].ValidDirections = Direction.None;
                
                _currentPosition = newPos;

                _currentLevel.Blocks[_currentPosition].Type = ObjectTypes.Player;
                _currentLevel.Blocks[_currentPosition].Move = _currentLevel.Blocks[_currentPosition].ValidDirections = Direction.All;
            }
        }

        private bool CanMoveInDirection(MoveDirection dir, out int position)
        {
            position = _currentPosition;

            var newPos = _currentPosition;
            if (dir == MoveDirection.Left)
                newPos -= 1;
            else if (dir == MoveDirection.Right)
                newPos += 1;
            else if (dir == MoveDirection.Up)
                newPos -= _data.Columns;
            else if (dir == MoveDirection.Down)
                newPos += _data.Columns;

            if (newPos < 0 || newPos >= _data.Data.Length)
                return false;

            if (_data.Data[newPos].Type == ObjectTypes.SolidWall ||
                _data.Data[newPos].Type == ObjectTypes.BrickWall || 
                _data.Data[newPos].Type == ObjectTypes.Door ||
                _data.Data[newPos].Type == ObjectTypes.Ignore)
            {
                return false;
            }

            if (_data.Data[newPos].Type == ObjectTypes.Stone || _data.Data[newPos].Type == ObjectTypes.Bomb)
            {
                if (dir == MoveDirection.Left)
                {
                    if (_data.Data[newPos - 1].Type != ObjectTypes.Empty)
                        return false;
                }
                else if (dir == MoveDirection.Right)
                {
                    if (_data.Data[newPos + 1].Type != ObjectTypes.Empty)
                        return false;
                }
                else if (dir == MoveDirection.Up || dir == MoveDirection.Down)
                {
                    return false; // Can't move stones up or down.
                }
            }

            position = newPos;
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
