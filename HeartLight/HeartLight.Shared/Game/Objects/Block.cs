using System;

namespace HeartLight.Game.Objects
{
    [Flags]
    public enum Direction
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,

        All = Left | Right | Up | Down
    }

    public class Block
    {
        public Block(int type, Direction validDirections = Direction.None)
        {
            Type = type;
            ValidDirections = validDirections;
            Move = Direction.None;
        }

        public int Type { get; set; }

        public Direction ValidDirections { get; set; }

        public Direction Move { get; set; }
        public DateTime When { get; set; }
    }
}
