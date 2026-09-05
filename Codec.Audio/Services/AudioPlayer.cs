// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Services
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Codec.Files;
    using NAudio.Wave;
    using NAudio.Wave.SampleProviders;

    public sealed class AudioPlayer : IDisposable, INotifyPropertyChanged
    {
        private readonly long start;
        private WaveOutEvent? waveOut;
        private AudioStream stream;
        private WaveStream? reader;
        private Timer? timer;
        private TaskCompletionSource<bool>? tcs;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TimeSpan CurrentTime => this.reader?.CurrentTime ?? TimeSpan.Zero;

        public TimeSpan TotalTime => this.reader?.TotalTime ?? TimeSpan.FromTicks(1);

        public bool Playing => this.waveOut?.PlaybackState == PlaybackState.Playing;

        public AudioPlayer(AudioStream stream)
        {
            this.stream = stream;
            this.reader = new WaveFileReader(stream);
            this.waveOut = new WaveOutEvent();
            if (this.reader.WaveFormat.Channels > 2)
            {
                var stereo = new MultiplexingSampleProvider([this.reader.ToSampleProvider()], 2);
                this.waveOut.Init(stereo.ToWaveProvider());
            }
            else
            {
                this.waveOut.Init(this.reader);
            }

            this.start = this.reader.Position;
            this.waveOut.PlaybackStopped += this.WaveOut_PlaybackStopped;
        }

        public Task<bool> PlayAsync()
        {
            this.waveOut?.Play();
            this.FinalTick(false);
            var tcs = new TaskCompletionSource<bool>();
            this.timer = new Timer(this.Timer_Tick, null, TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.2));
            this.tcs = tcs;
            return tcs.Task;
        }

        public static async Task<bool> PlayAsync(AudioStream mediaStream)
        {
            using var audio = new AudioPlayer(mediaStream);
            return await audio.PlayAsync().ConfigureAwait(false);
        }

        public void Play()
        {
            _ = this.PlayAsync();
        }

        public void Pause()
        {
            this.waveOut?.Pause();
            this.FinalTick(false);
        }

        public void Stop()
        {
            this.tcs?.TrySetResult(false);
            this.tcs = null;
            this.waveOut?.Stop();
        }

        public void Dispose()
        {
            this.Stop();
            this.waveOut?.Dispose();
            this.waveOut = null;
            this.reader?.Dispose();
            this.reader = null;
            this.stream.Stream.Dispose();
        }

        private void FinalTick(bool stoppedNormally)
        {
            this.tcs?.TrySetResult(stoppedNormally);
            this.tcs = null;
            this.timer?.Dispose();
            this.timer = null;
            this.PropertyChanged?.Invoke(this, new(nameof(this.Playing)));
        }

        private void Timer_Tick(object? state = null)
        {
            this.PropertyChanged?.Invoke(this, new(nameof(this.CurrentTime)));
        }

        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            this.reader?.Position = this.start;
            this.Timer_Tick();
            this.FinalTick(true);
        }
    }
}
