namespace Codec.UI.WinForms
{
    using System.Windows.Forms;

    public partial class ProgressForm : Form
    {
        private readonly CancellationTokenSource cts = new();

        public ProgressForm()
        {
            this.InitializeComponent();
        }

        public CancellationToken Cancel => this.cts.Token;

        public float Progress
        {
            get;

            set
            {
                field = value;
                var target = (int)Math.Round(value * this.progressBar.Maximum);
                if (target == this.progressBar.Maximum && value < 1)
                {
                    target -= 1;
                }

                this.progressBar.Value = Math.Clamp(target, 0, this.progressBar.Maximum);
            }
        }

        public string ProgressText
        {
            get => this.progressText.Text;
            set => this.progressText.Text = value;
        }

        private void ProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.CancelButton_Click(sender, e);
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.cts.Cancel();
        }
    }
}
