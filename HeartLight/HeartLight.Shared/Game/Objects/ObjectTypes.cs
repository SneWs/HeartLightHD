namespace HeartLight.Game.Objects
{
    public struct ObjectTypes
    {
        public const int Ignore = 0;
        public const int Empty = 1;
        public const int SolidWall = 2;
        public const int Grass = 3;
        public const int Stone = 4;
        public const int Bomb = 5;
        public const int Heart = 6;
        public const int Door = 7;
        public const int Player = 8;
        public const int BrickWall = 9;

        public const int LastType = BrickWall;

        public const int BombExplode = 1001;
        public const int DoorEnter = 1011;
    }
}
