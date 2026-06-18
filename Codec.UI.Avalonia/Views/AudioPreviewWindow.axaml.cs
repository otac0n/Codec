namespace Codec.UI.Avalonia.Views
{
    using System;
    using System.ComponentModel;
    using Codec.Files;
    using Codec.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using global::Avalonia.Controls;
    using IconPacks.Avalonia.FontAwesome;

    public partial class AudioPreviewWindow : Window, IDisposable
    {
        private AudioPlayer AudioPlayer { get; }

        public AudioPreviewWindow(AudioStream mediaStream)
        {
            this.AudioPlayer = new AudioPlayer(mediaStream);
            this.InitializeComponent();
            this.DataContext = new ViewModel(this.AudioPlayer);
            this.AudioPlayer.Play();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.AudioPlayer.Dispose();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            this.Dispose();
        }

        public partial class ViewModel : ObservableObject
        {
            public AudioPlayer AudioPlayer { get; }

            [ObservableProperty]
            private double progress;

            [ObservableProperty]
            private PackIconFontAwesomeKind playPauseIcon;

            [ObservableProperty]
            private bool canStop;

            public ViewModel(AudioPlayer audioPlayer)
            {
                audioPlayer.PropertyChanged += this.AudioPlayer_PropertyChanged;
                this.AudioPlayer = audioPlayer;
            }

            [RelayCommand]
            public void PlayPause()
            {
                if (this.AudioPlayer.Playing)
                {
                    this.AudioPlayer.Pause();
                }
                else
                {
                    this.AudioPlayer.Play();
                }
            }

            [RelayCommand]
            public void Stop()
            {
                this.AudioPlayer.Stop();
            }

            private void AudioPlayer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                this.CanStop = this.AudioPlayer.Playing || this.AudioPlayer.CurrentTime.TotalSeconds > 0;
                this.Progress = this.AudioPlayer.CurrentTime.TotalSeconds / this.AudioPlayer.TotalTime.TotalSeconds * 100;
                this.PlayPauseIcon = this.AudioPlayer.Playing
                    ? PackIconFontAwesomeKind.PauseSolid
                    : PackIconFontAwesomeKind.PlaySolid;
            }
        }
    }
}
