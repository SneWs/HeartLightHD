using System;
using HeartLight.Game.Objects;

namespace HeartLight.Game
{
    public class LevelData
    {
        private readonly int _columns;
        private readonly int _rows;
        private readonly Block[] _data;
        private readonly int _playerStartPosition;
        private readonly int _numberOfHearts;
        private readonly DateTime _created;
        private string _email;
        private string _fullName;

        public LevelData(int columns, int rows, Block[] data, int playerStartPosition, int numberOfHearts, DateTime created, string email, string fullName)
        {
            _columns = columns;
            _rows = rows;
            _data = data;
            _playerStartPosition = playerStartPosition;
            _numberOfHearts = numberOfHearts;
            _created = created;
            _email = email;
            _fullName = fullName;
        }

        public int Columns
        {
            get { return _columns; }
        }

        public int Rows
        {
            get { return _rows; }
        }

        public int DataLength
        {
            get { return _columns * _rows; }
        }

        public Block[] Data
        {
            get { return _data; }
        }

        public DateTime Created
        {
            get { return _created; }
        }

        public string Email
        {
            get { return _email; }
            set { _email = value; }
        }

        public string FullName
        {
            get { return _fullName; }
            set { _fullName = value; }
        }

        public int PlayerStartPosition
        {
            get { return _playerStartPosition; }
        }

        public int DoorPosition { get; set; }

        public int NumberOfHearts
        {
            get { return _numberOfHearts; }
        }
    }
}
