// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Services
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Codec.Files;
    using NAudio.Wave;
    using NAudio.Wave.SampleProviders;
    using VgmSharp;

    public sealed class AudioPlayer : IDisposable, INotifyPropertyChanged
    {
        private readonly long start;
        private WaveOutEvent? waveOut;
        private WaveStream? reader;
        private Timer? timer;
        private TaskCompletionSource<bool>? tcs;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TimeSpan CurrentTime => this.reader?.CurrentTime ?? TimeSpan.Zero;

        public TimeSpan TotalTime => this.reader?.TotalTime ?? TimeSpan.FromTicks(1);

        public bool Playing => this.waveOut?.PlaybackState == PlaybackState.Playing;

        public AudioPlayer(AudioStream stream, bool ownsStream = true)
        {
            var ms = new MemoryStream();
            try
            {
                if (!TryDecodeWithVgmstream(stream, ms))
                {
                    if (stream.Stream.CanSeek)
                    {
                        stream.Stream.Position = 0;
                    }

                    using var reader = new StreamMediaFoundationReader(stream);
                    WaveFileWriter.WriteWavFileToStream(ms, reader);
                }

                ms.Seek(0, SeekOrigin.Begin);
            }
            finally
            {
                if (ownsStream)
                {
                    stream.Stream.Dispose();
                }
            }

            this.reader = new WaveFileReader(ms);
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

        private static bool TryDecodeWithVgmstream(AudioStream stream, MemoryStream destination)
        {
            if (string.IsNullOrEmpty(stream.FileName) || !stream.Stream.CanSeek)
            {
                return false;
            }

            try
            {
                using var vgm = VgmStreamReader.Open(
                    stream.Stream,
                    stream.FileName,
                    config: VgmStreamConfig.PlayOnceNoLoop());

                vgm.DecodeTo(destination);
                return true;
            }
            catch (VgmStreamException)
            {
                return false;
            }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            {
                return false;
            }
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
