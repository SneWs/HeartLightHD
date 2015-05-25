using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using HeartLight.Game;
using HeartLight.Game.Objects;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Numerics;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace HeartLight
{
    public enum GameState
    {
        None,
        Initializing,
        InMenus,
        Running,
        Paused,
        LevelLoading,
        LevelLoaded,
        LevelUnloading,
        LevelUnloaded,
        PlayerSolvedLevel,
        PlayerDied,
        GameOver,
        PlayerSolvedAllAvailableLevels
    }

    public class PageDataContext : INotifyPropertyChanged
    {
        private int _currentLevel = 1;
        private int _numberOfLevels = 3;

        public Player Player { get; set; }
        public Game.Level Level { get; set; }
        public LevelData LevelData { get; set; }

        public void RefreshBindings()
        {
            OnPropertyChanged("Player");
            OnPropertyChanged("Level");
            OnPropertyChanged("LevelData");
            OnPropertyChanged("NumberOfLevels");
            OnPropertyChanged("CurrentLevel");
        }

        public int NumberOfLevels
        {
            get { return _numberOfLevels; }
            set
            {
                if (value != _numberOfLevels)
                {
                    _numberOfLevels = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentLevel
        {
            get { return _currentLevel; }
            set
            {
                if (value != _currentLevel)
                {
                    _currentLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        private static bool DebugAudioMuted = false;

        private readonly List<GameComponent> _components;
        private readonly IMainThreadDispatcher _wrappedDispatcher;
        private readonly List<string> _levelNames;
        private readonly PageDataContext _pageData;

        private GameState _currentState = GameState.None;
        private GameState _prevState = GameState.None;
        private float _windowWidth;
        private float _windowHeight;

        CanvasLinearGradientBrush _menuBackgroundBrush;
        CanvasLinearGradientBrush _levelBackgroundBrush;
        private MenuRenderer _mainMenu;
        private Game.Level _currentLevel;
        private Player _player;

        private Dictionary<int, CanvasBitmap> _objectTypeImages;
        private Dictionary<int, ISoundEffect> _objectSoundEffects;
        private CanvasBitmap _playerLeft;
        private CanvasBitmap _playerRight;

        // Effects
        private SaturationEffect _bombEffect;
        private SaturationEffect _bombExplosionEffect;

        public MainPage()
        {
            InitializeComponent();
            Loaded += OnPageLoaded;

            _components = new List<GameComponent>();
            _objectTypeImages = new Dictionary<int, CanvasBitmap>(10);
            _objectSoundEffects = new Dictionary<int, ISoundEffect>(5);
            _levelNames = new List<string>();
            _pageData = new PageDataContext();

            GoToState(GameState.Initializing);

            // Hook up window events that we need to listen to for a smooth user experience.
            Window.Current.CoreWindow.Activated += OnWindowActivated;
            Window.Current.CoreWindow.KeyDown += OnWindowKeyDown;
            Window.Current.CoreWindow.KeyUp += OnWindowKeyUp;
            Window.Current.CoreWindow.PointerPressed += OnWindowPointerPressed;

            _wrappedDispatcher = new MainThreadDispatcher(Dispatcher);
        }

        public object ComponentsSyncRoot
        {
            get { return ((ICollection)_components).SyncRoot; }
        }

        private void ResumeGame()
        {
            DebugRenderer.Instance.DrawDebugText("ResumeGame() called");

            StartMusicIfNotPlaying();
            Window.Current.CoreWindow.SetPointerCapture();
        }

        private void EnterInMenuState()
        {
            _mainMenu.IsEnabled = true;
            BackgroundSoundPlayer.Volume = 0.075f;
            if (_currentLevel != null)
                _currentLevel.IsEnabled = false;

            BottomScoreArea.Visibility = Visibility.Collapsed;
            BackgroundVideo.Source = new Uri("ms-appx:///Assets/Movies/menu_background.wmv");
        }

        private void EnterLevelLoadingState()
        {
            LoadLevel(String.Format("Level{0:D3}", _pageData.CurrentLevel));

            Dispatcher.RunAsync(CoreDispatcherPriority.Low,
                () => BackgroundVideo.Source = new Uri("ms-appx:///Assets/Movies/level_background.wmv"));
        }

        private void EnterLevelLoadedState()
        {
            _mainMenu.IsEnabled = false;
            _currentLevel.IsEnabled = true;

            Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => {
                BottomScoreArea.Visibility = Visibility.Visible;
            });
        }

        private void EnterPlayerSolvedLevelState()
        {
            ++_pageData.CurrentLevel;
            if (_pageData.CurrentLevel > _pageData.NumberOfLevels)
            {
                // Player have solved all available levels.
                GoToState(GameState.PlayerSolvedAllAvailableLevels);
                return;
            }

            GoToState(GameState.LevelLoading);
        }

        private void EnterPlayerDiedState()
        {
            GoToState(GameState.LevelLoading);
        }

        private void EnterPlayerSolvedAllAvailableLevelsState()
        {
            _currentLevel.IsEnabled = false;
            _player.IsEnabled = false;
            _pageData.CurrentLevel = 1;

            Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () => {
                BackgroundVideo.Source = new Uri("ms-appx:///Assets/Movies/all_levels_completed.wmv");
                BottomScoreArea.Visibility = Visibility.Collapsed;
                
                ReadLevelsMetaData();

                await Task.Delay(1000);
                _objectSoundEffects[ObjectTypes.DoorEnter].Play();
            });
        }

        private void StartNewGame()
        {
            DebugRenderer.Instance.DrawDebugText("StartNewGame() called");
            GoToState(GameState.LevelLoading);
        }

        private async void LoadLevel(string levelName = "Level001")
        {
            DebugRenderer.Instance.DrawDebugText(String.Format("Request to load level '{0}'", levelName));

            lock (ComponentsSyncRoot)
            {
                if (_player != null)
                    _components.Remove(_player);

                if (_currentLevel != null)
                    _components.Remove(_currentLevel);
            }

            var loader = new LevelLoader(String.Format("Levels/New/{0}.lvl", levelName));
            var levelData = await loader.LoadLevel();

            if (!levelData.Succeeded)
            {
                DebugRenderer.Instance.DrawDebugText(String.Format("====== FAILURE: Failed to load level: {0}", levelData.Error));
                GoToState(GameState.InMenus);
                return;
            }

            var data = levelData.Data;
            _currentLevel = new Game.Level(data, ref _objectTypeImages, ref _objectSoundEffects) {
                PlayerLeftImage = _playerLeft,
                PlayerRightImage = _playerRight,
                BombEffect = _bombEffect,
                BombExplosionEffect = _bombExplosionEffect
            };
            
            _player = new Player(_currentLevel, data, ref _objectSoundEffects);
            _currentLevel.Player = _player;

            _player.PlayerSolvedLevel += OnLevelSolvedByPlayer;
            _player.PlayerDied += OnPlayerDied;

            _pageData.Player = _player;
            _pageData.Level = _currentLevel;
            _pageData.LevelData = data;

            Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => {
                DataContext = _pageData;
                _pageData.RefreshBindings();
            });

            lock (ComponentsSyncRoot)
            {
                var newIndex = _components.IndexOf(DebugRenderer.Instance);
                _components.Insert(newIndex, _player);
                _components.Insert(newIndex, _currentLevel);
            }

            GoToState(GameState.LevelLoaded);
        }

        private async void ShowLoadGame()
        {
            DebugRenderer.Instance.DrawDebugText("ShowLoadGame() called");

            var folder = Package.Current.InstalledLocation;
            var levelsFolder = await folder.GetFolderAsync("Levels");
            var newLevelsFolder = await levelsFolder.GetFolderAsync("New");
            var levels = await newLevelsFolder.GetFilesAsync();
            foreach (var lvl in levels)
            {
                DebugRenderer.Instance.DrawDebugText("Level: " + lvl.Name);
            }
        }

#if DEBUG || _DEBUG || DEBUG_LEVEL_EDITOR
        private async void ShowLoadLevel()
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
                    lock (ComponentsSyncRoot)
                    {
                        _components.Remove(_player);
                        _components.Remove(_currentLevel);

                        var data = levelResult.Data;
                        _currentLevel = new Game.Level(data, ref _objectTypeImages, ref _objectSoundEffects) {
                            PlayerLeftImage = _playerLeft,
                            PlayerRightImage = _playerRight
                        };

                        _player = new Player(_currentLevel, data, ref _objectSoundEffects);
                        _currentLevel.Player = _player;

                        var newIndex = _components.IndexOf(DebugRenderer.Instance);
                        _components.Insert(newIndex, _player);
                        _components.Insert(newIndex, _currentLevel);
                    }

                    GoToState(GameState.LevelLoaded);
                }
            }
        }
#endif

        private void ShowSettings()
        {
            DebugRenderer.Instance.DrawDebugText("ShowSettings() called");
        }

        private void ShowAbout()
        {
            DebugRenderer.Instance.DrawDebugText("ShowAbout() called");
        }

        private void PauseGame()
        {
            DebugRenderer.Instance.DrawDebugText("PauseGame() called");

            BackgroundSoundPlayer.Pause();
        }

        private void GoToState(GameState newState)
        {
            if (newState == GameState.None)
                return;

            _prevState = _currentState;
            _currentState = newState;

            DebugRenderer.Instance.DrawDebugText(String.Format("GoToState() called. Current State(New): {0}, PrevState(Old): {1}", _currentState, _prevState));

            switch (_currentState)
            {
                case GameState.Initializing:
                    ResumeGame();
                    break;

                case GameState.InMenus:
                    EnterInMenuState();
                    break;

                case GameState.Paused:
                    PauseGame();
                    break;

                case GameState.LevelLoading:
                    EnterLevelLoadingState();
                    break;

                case GameState.LevelLoaded:
                    EnterLevelLoadedState();
                    break;

                case GameState.PlayerSolvedLevel:
                    EnterPlayerSolvedLevelState();
                    break;

                case GameState.PlayerDied:
                    EnterPlayerDiedState();
                    break;

                case GameState.PlayerSolvedAllAvailableLevels:
                    EnterPlayerSolvedAllAvailableLevelsState();
                    break;
            }
        }

        private void StartMusicIfNotPlaying()
        {
            if (BackgroundSoundPlayer.CurrentState != MediaElementState.Playing)
            {
                BackgroundSoundPlayer.IsMuted = DebugAudioMuted;

                DebugRenderer.Instance.DrawDebugText(String.Format("Calling BackgroundSoundPlayer.Play(). Current state for BackgroundSoundPlayer: {0}", BackgroundSoundPlayer.CurrentState));
                BackgroundSoundPlayer.Play();
            }
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            DebugRenderer.Instance.DrawDebugText("OnPageLoaded() called");
            GrabWindowSize();

            InitializeSoundEffects();

            InitializeMainMenu();
            // ReSharper disable once InconsistentlySynchronizedField
            _components.Add(DebugRenderer.Instance);

            ReadLevelsMetaData();

            GoToState(GameState.InMenus);
            ResumeGame();
        }

        private async void ReadLevelsMetaData()
        {
            var folder = Package.Current.InstalledLocation;
            var levelsFolder = await folder.GetFolderAsync("Levels");
            var newLevelsFolder = await levelsFolder.GetFolderAsync("New");
            var levels = await newLevelsFolder.GetFilesAsync();

            foreach (var lvl in levels)
            {
                var lvlName = Path.GetFileNameWithoutExtension(lvl.Name);
                DebugRenderer.Instance.DrawDebugText("Level: " + lvlName);

                _levelNames.Add(lvlName);
            }

            _pageData.NumberOfLevels = _levelNames.Count;
        }

        private async void InitializeSoundEffects()
        {
            var installedLocation = Package.Current.InstalledLocation;
            _objectSoundEffects.Add(ObjectTypes.Heart, await LoadSoundEffect(installedLocation, "Assets\\Sound\\HeartTaken.wav"));
            _objectSoundEffects.Add(ObjectTypes.Stone, await LoadSoundEffect(installedLocation, "Assets\\Sound\\StoneImpact.wav"));
            _objectSoundEffects.Add(ObjectTypes.Door, await LoadSoundEffect(installedLocation, "Assets\\Sound\\DoorOpen.wav"));
            _objectSoundEffects.Add(ObjectTypes.DoorEnter, await LoadSoundEffect(installedLocation, "Assets\\Sound\\Applause.wav"));
            _objectSoundEffects.Add(ObjectTypes.BombExplode, await LoadSoundEffect(installedLocation, "Assets\\Sound\\Explosion.wav"));
        }

        private async Task<SoundEffect> LoadSoundEffect(StorageFolder root, string filename)
        {
            var heartSound = await root.GetFileAsync(filename);
            var heartStream = await heartSound.OpenAsync(FileAccessMode.Read);
            
            return new SoundEffect(_wrappedDispatcher, heartStream, heartSound.ContentType);
        }

        private void InitializeMainMenu()
        {
            var regularColor = Color.FromArgb(225, 255, 255, 255);

            _mainMenu = new MenuRenderer("Verdana", 48.0f);
            _mainMenu.AddItem(new GameMenuItem("Start", regularColor, Colors.YellowGreen, (o, args) => StartNewGame()));
            _mainMenu.AddItem(new GameMenuItem("Load Game", regularColor, Colors.YellowGreen, (o, args) => ShowLoadGame()));
#if DEBUG || _DEBUG || DEBUG_LEVEL_EDITOR
            _mainMenu.AddItem(new GameMenuItem("Load Level", regularColor, Colors.YellowGreen, (o, args) => ShowLoadLevel()));
#endif
            _mainMenu.AddItem(new GameMenuItem("Settings", regularColor, Colors.YellowGreen, (o, args) => ShowSettings()));
            _mainMenu.AddItem(new GameMenuItem("About", regularColor, Colors.YellowGreen, (o, args) => ShowAbout()));
            _components.Add(_mainMenu);
        }

        private void OnWindowActivated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                Window.Current.CoreWindow.ReleasePointerCapture();
                GoToState(GameState.Paused);
            }
            else
            {
                // When we come back from a deactivated state we go into the pause menu or resume to the bootup menu
                GoToState(_prevState);
            }
        }

        private void GrabWindowSize()
        {
            _windowWidth = (float)CanvasArea.ActualWidth;
            _windowHeight = (float)CanvasArea.ActualHeight;
        }

        private void OnWindowKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            lock (ComponentsSyncRoot)
            {
                for (var index = 0; index < _components.Count; index++)
                {
                    var comp = _components[index];
                    if (!comp.IsEnabled)
                        continue;

                    comp.OnKeyDown(args);
                }
            }
        }

        private void OnWindowKeyUp(CoreWindow sender, KeyEventArgs args)
        {
            lock (ComponentsSyncRoot)
            {
                for (var index = 0; index < _components.Count; index++)
                {
                    var comp = _components[index];
                    if (!comp.IsEnabled)
                        continue;

                    comp.OnKeyUp(args);
                }
            }

            if (_currentState == GameState.PlayerSolvedAllAvailableLevels)
            {
                if (args.VirtualKey == VirtualKey.Enter)
                {
                    GoToState(GameState.InMenus);
                }
            }
        }

        private void OnWindowPointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            lock (ComponentsSyncRoot)
            {
                for (var index = 0; index < _components.Count; index++)
                {
                    var comp = _components[index];
                    if (!comp.IsEnabled)
                        continue;

                    comp.OnPointerPressed(args);
                }
            }
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
            var bombExplodeImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Explosion_1024x1024.png");
            var doorImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/Door_1024x1024.png");
            var doorOpenImage = await CanvasBitmap.LoadAsync(ctrl, "Assets/Images/DoorOpen_1024x1024.png");

            _objectTypeImages.Add(ObjectTypes.SolidWall, stoneWallImage);
            _objectTypeImages.Add(ObjectTypes.BrickWall, brickWallImage);
            _objectTypeImages.Add(ObjectTypes.Heart, heartImage);
            _objectTypeImages.Add(ObjectTypes.Grass, grassImage);
            _objectTypeImages.Add(ObjectTypes.Stone, stoneImage);
            _objectTypeImages.Add(ObjectTypes.Bomb, bombImage);
            _objectTypeImages.Add(ObjectTypes.BombExplode, bombExplodeImage);
            _objectTypeImages.Add(ObjectTypes.Door, doorImage);
            _objectTypeImages.Add(ObjectTypes.DoorEnter, doorOpenImage);

            // And Image effects
            _bombEffect = new SaturationEffect {
                Saturation = 0.8f,
                Source = _objectTypeImages[ObjectTypes.Bomb]
            };

            _bombExplosionEffect = new SaturationEffect {
                Saturation = 0.8f,
                Source = _objectTypeImages[ObjectTypes.BombExplode]
            };
        }

        private void OnCanvasCreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Custom, 101); // Custom ARROW pointer

            // Main menu background
            var stops = new[] {
                new CanvasGradientStop { Color = Color.FromArgb(205, 0, 0, 0), Position = 0.0f },
                new CanvasGradientStop { Color = Color.FromArgb(205, 0, 0, 0), Position = 0.50f },
                new CanvasGradientStop { Color = Color.FromArgb(205, 33, 33, 33), Position = 0.72f},
                new CanvasGradientStop { Color = Color.FromArgb(205, 0, 0, 0), Position = 0.92f},
                new CanvasGradientStop { Color = Color.FromArgb(205, 0, 0, 0), Position = 1.0f },
            };

            _menuBackgroundBrush = new CanvasLinearGradientBrush(CanvasArea, stops, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied) {
                StartPoint = new Vector2 {X = 0, Y = 0},
                EndPoint = new Vector2 {X = 0, Y = _windowHeight}
            };

            // Levels background
            var levelStops = new[] {
                new CanvasGradientStop { Color = Color.FromArgb(120, 0, 0, 0), Position = 0.0f },
                new CanvasGradientStop { Color = Color.FromArgb(120, 0, 0, 0), Position = 0.50f },
                new CanvasGradientStop { Color = Color.FromArgb(120, 0, 0, 0), Position = 0.92f},
                new CanvasGradientStop { Color = Color.FromArgb(120, 0, 0, 0), Position = 1.0f },
            };

            _levelBackgroundBrush = new CanvasLinearGradientBrush(CanvasArea, levelStops, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied) {
                StartPoint = new Vector2 { X = 0, Y = 0 },
                EndPoint = new Vector2 { X = 0, Y = _windowHeight }
            };

            args.TrackAsyncAction(LoadImagesAsync(sender).AsAsyncAction());

            lock (ComponentsSyncRoot)
            {
                foreach (var comp in _components)
                    comp.CreateResources(sender, args);
            }
        }

        private void OnCanvasUpdate(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            if (_currentState == GameState.Paused)
                return;

            var updateInfo = new UpdateInfo(args.Timing.ElapsedTime, _windowWidth, _windowHeight);

            Dispatcher.RunAsync(CoreDispatcherPriority.Low, StartMusicIfNotPlaying);
            Dispatcher.RunAsync(CoreDispatcherPriority.High, GrabWindowSize);

            lock (ComponentsSyncRoot)
            {
                foreach (var comp in _components)
                {
                    if (!comp.IsEnabled)
                        continue;
                    comp.Update(updateInfo);
                }
            }
        }

        private void OnCanvasDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            using (var g = args.DrawingSession)
            {
                g.TextAntialiasing = CanvasTextAntialiasing.ClearType;
                g.Antialiasing = CanvasAntialiasing.Antialiased;
                g.Blend = CanvasBlend.SourceOver;

                DebugRenderer.Instance.GfxSession = g;

                if (_currentState == GameState.PlayerSolvedAllAvailableLevels)
                {
                    var rc = new Rect(0, 0, _windowWidth, _windowHeight);
                    var textFormat = new CanvasTextFormat();
                    textFormat.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                    textFormat.VerticalAlignment = CanvasVerticalAlignment.Center;
                    textFormat.Options = CanvasDrawTextOptions.NoPixelSnap;
                    textFormat.WordWrapping = CanvasWordWrapping.WholeWord;
                    textFormat.FontSize = 65.0f;

                    g.FillRectangle(rc, _menuBackgroundBrush);
                    g.DrawText("Congratulations, you have solved all levels.", rc, Colors.White, textFormat);
                }
                else if (_currentState == GameState.LevelLoaded)
                {
                    g.FillRectangle(0, 0, _windowWidth, _windowHeight, _levelBackgroundBrush);
                }
                else
                {
                    g.FillRectangle(0, 0, _windowWidth, _windowHeight, _menuBackgroundBrush);
                }

                lock (ComponentsSyncRoot)
                {
                    foreach (var comp in _components)
                    {
                        if (!comp.IsEnabled)
                            continue;

                        comp.Draw(g);
                    }
                }

                if (_currentState == GameState.Paused)
                {
                    var rc = new Rect(0, 0, _windowWidth, _windowHeight);
                    var textFormat = new CanvasTextFormat();
                    textFormat.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                    textFormat.VerticalAlignment = CanvasVerticalAlignment.Center;
                    textFormat.FontSize = 72.0f;

                    g.FillRectangle(rc, _menuBackgroundBrush);
                    g.DrawText("Game has been Paused.", rc, Colors.White, textFormat);
                }
            }
        }

        private void OnToggleOutlineDebug(object sender, RoutedEventArgs e)
        {
            DebugRenderer.DebugRendererEnabled = !DebugRenderer.DebugRendererEnabled;
        }

        private void BackgroundVideo_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        private void OnLevelSolvedByPlayer(object sender, EventArgs e)
        {
            _player.PlayerSolvedLevel -= OnLevelSolvedByPlayer;
            _player.PlayerDied -= OnPlayerDied;

            GoToState(GameState.PlayerSolvedLevel);
        }

        private void OnPlayerDied(object sender, EventArgs e)
        {
            GoToState(GameState.PlayerDied);
        }

        private void OnPlayerManualRestartOfLevel(object sender, RoutedEventArgs e)
        {
            if (_player != null)
                _player.CommitSuicide();
        }
    }
}
