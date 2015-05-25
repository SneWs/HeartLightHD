using System;

namespace HeartLight
{
    public class UpdateInfo
    {
        public TimeSpan ElapsedTime { get; set; }
        public float WindowWidth { get; set; }
        public float WindowHeight { get; set; }

        public UpdateInfo(TimeSpan elapsedTime, float width, float height)
        {
            ElapsedTime = elapsedTime;
            WindowWidth = width;
            WindowHeight = height;
        }
    }
}
