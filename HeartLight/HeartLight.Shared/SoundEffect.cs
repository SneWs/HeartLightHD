using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace HeartLight
{
    public interface ISoundEffect
    {
        void Play();
    }

    public class SoundEffect : ISoundEffect
    {
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly IRandomAccessStream _stream;
        private readonly string _contentType;

        public SoundEffect(IMainThreadDispatcher dispatcher, IRandomAccessStream stream, string contentType)
        {
            _dispatcher = dispatcher;
            _stream = stream;
            _contentType = contentType;

            // ReSharper disable CSharpWarnings::CS4014
            CreateElement(0.0f);
            // ReSharper restore CSharpWarnings::CS4014
        }

        public async void Play()
        {
            var elem = await CreateElement();

            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                elem.MediaOpened += (sender, args) => elem.Play();
            });
        }

        private async Task<MediaElement> CreateElement(float volume = 1.0f)
        {
            MediaElement elem = null;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                elem = new MediaElement {
                    AudioCategory = AudioCategory.GameEffects,
                    IsLooping = false,
                    AutoPlay = true,
                    Volume = volume
                };

                elem.SetSource(_stream, _contentType);
            });

            return elem;
        }
    }
}
