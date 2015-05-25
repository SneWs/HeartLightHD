namespace HeartLight.Game
{
    public class LevelLoadResult
    {
        /// <summary>
        /// The level data. This will only be valid if Error is LevelLoadError.None.
        /// </summary>
        public LevelData Data { get; set; }

        /// <summary>
        /// The Error code, if any when the loading of a level have failed.
        /// </summary>
        public LevelLoadError Error { get; set; }

        /// <summary>
        /// This does only have valid data set if Error != LevelLoadError.None; 
        /// Otherwise it will point to the line number in the level file.
        /// </summary>
        public int ErrorLineNumber { get; set; }

        /// <summary>
        /// Did the load operation succeed, if so, this value will be True; Otherwise False.
        /// </summary>
        public bool Succeeded
        {
            get { return Error == LevelLoadError.None; }
        }

        public LevelLoadResult(LevelData data, LevelLoadError error, int errorLineNumber)
        {
            Data = data;
            Error = error;
            ErrorLineNumber = errorLineNumber;
        }
    }
}
