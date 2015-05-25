using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using HeartLight.Game.Objects;

namespace HeartLight.Game
{
    public enum LevelLoadError
    {
        None = 0,
        EmptyLevelFile,
        InvalidHeader,
        InvalidFileType,
        BoardDataToSmall,
        UnbalancedHeaderInfoVsData,
        UnbalancedRowData,
        UnbalancedColumnData,
        InvalidFieldData,
        LevelNotFound
    }

    public class LevelLoader
    {
        private readonly string _filename;
        private StorageFile _file;

        public LevelLoader(string filename)
        {
            _filename = filename;
        }

        public LevelLoader(StorageFile file)
        {
            _file = file;
        }

        public async Task<LevelLoadResult> LoadLevel()
        {
            if (_file == null)
            {
                try
                {
                    _file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///" + _filename));
                }
                catch (Exception e)
                {
                    return new LevelLoadResult(null, LevelLoadError.LevelNotFound, 0);
                }
            }

            var lines = await FileIO.ReadLinesAsync(_file, UnicodeEncoding.Utf8);
            if (lines.Count < 1)
                return new LevelLoadResult(null, LevelLoadError.EmptyLevelFile, 1);

            var header = lines[0];
            var values = header.Split(",".ToCharArray(), StringSplitOptions.None);
            if (values.Length < 4)
                return new LevelLoadResult(null, LevelLoadError.InvalidHeader, 1);

            // Header values should be at least:
            // lvl,columns,rows,Date-Created
            // Where lvl is the file marker

            if (values[0] != "lvl")
                return new LevelLoadResult(null, LevelLoadError.InvalidFileType, 1);

            var columnsValue = values[1];
            var rowsValue = values[2];
            var dateCreatedValue = values[3];
            var creatorEmailValue = "";
            var creatorNameValue = "";

            if (values.Length >= 5)
                creatorEmailValue = values[4];
            if (values.Length >= 6)
                creatorNameValue = values[5];

            int columns;
            if (!Int32.TryParse(columnsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out columns))
                columns = 0;

            int rows;
            if (!Int32.TryParse(rowsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rows))
                rows = 0;

            DateTime created;
            if (!DateTime.TryParse(dateCreatedValue, out created))
                created = DateTime.MinValue;

            if (columns < 5 || rows < 5)
                return new LevelLoadResult(null, LevelLoadError.BoardDataToSmall, 1);

            // Do we have enough data rows?
            if (rows != lines.Count(x => !String.IsNullOrEmpty(x)) - 1)
                return new LevelLoadResult(null, LevelLoadError.UnbalancedRowData, 1);

            // Let's read all the level data
            var data = new Block[columns * rows];
            var playerStartPosition = -1;
            var doorPosition = -1;
            var numberOfHearts = 0;
            var activeElementIndex = 0;
            for (var i = 1; i < lines.Count; i++)
            {
                var line = lines[i];

                // We skip empty lines.
                if (String.IsNullOrEmpty(line))
                    continue;

                // We've found a line that does not have the expected number of column values.
                if (line.Length != columns)
                    return new LevelLoadResult(null, LevelLoadError.UnbalancedColumnData, i);

                // In each line, we extract each number representing the object type on the level.
                for (var ch = 0; ch < line.Length; ch++)
                {
                    int value;
                    if (!Int32.TryParse(line[ch].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                        return new LevelLoadResult(null, LevelLoadError.InvalidFieldData, i);

                    if (value == ObjectTypes.Player)
                        playerStartPosition = activeElementIndex;
                    else if (value == ObjectTypes.Door || value == ObjectTypes.DoorEnter)
                        doorPosition = activeElementIndex;
                    else if (value == ObjectTypes.Heart)
                        ++numberOfHearts;

                    var dir = Direction.None;
                    switch (value)
                    {
                        case ObjectTypes.Bomb:
                        case ObjectTypes.Stone:
                            dir = Direction.Left | Direction.Right | Direction.Down;
                            break;

                        case ObjectTypes.Player:
                            dir = Direction.All;
                            break;
                    }

                    data[activeElementIndex++] = new Block(value, dir) {
                        When = DateTime.UtcNow
                    };
                }
            }

            var levelData = new LevelData(columns, rows, data, playerStartPosition, numberOfHearts, created, creatorEmailValue, creatorNameValue);
            
            levelData.DoorPosition = doorPosition;

            return new LevelLoadResult(levelData, LevelLoadError.None, 0);
        }
    }
}




