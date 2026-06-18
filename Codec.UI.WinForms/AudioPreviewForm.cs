namespace Codec.UI.WinForms
{
    using System;
    using System.Windows.Forms;
    using Codec.Files;
    using Codec.Services;
    using Codec.UI.WinForms.Properties;

    public partial class AudioPreviewForm : Form
    {
        private readonly AudioPlayer audioPlayer;

        public AudioPreviewForm(AudioStream mediaStream)
        {
            this.audioPlayer = new AudioPlayer(mediaStream);
            this.InitializeComponent();
            this.audioPlayer.PropertyChanged += this.AudioPlayer_PropertyChanged;
        }

        private void AudioPlayer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.InvokeIfNotDisposed(() =>
            {
                if (this.audioPlayer.Playing)
                {
                    this.playPause.Text = "Pause";
                    this.playPause.Image = Resources.FontAwesome_PauseSolid_20x20;
                }
                else
                {
                    this.playPause.Text = "Play";
                    this.playPause.Image = Resources.FontAwesome_PlaySolid_20x20;
                }
                this.stop.Enabled = this.audioPlayer.Playing || this.audioPlayer.CurrentTime.TotalSeconds > 0;
                this.progress.Value = (int)Math.Clamp((this.audioPlayer.CurrentTime.TotalSeconds / this.audioPlayer.TotalTime.TotalSeconds * this.progress.Maximum) + 1, 0, this.progress.Maximum);
                this.progress.Value--;
            });
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            this.audioPlayer.Stop();
        }

        private void PlayPause_Click(object sender, EventArgs e)
        {
            if (this.audioPlayer.Playing)
            {
                this.audioPlayer.Pause();
            }
            else
            {
                this.audioPlayer.Play();
            }
        }

        private void Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.audioPlayer.Dispose();
        }

        private void AudioPreviewForm_Shown(object sender, EventArgs e)
        {
            this.audioPlayer.Play();
        }
    }
}
