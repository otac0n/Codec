namespace Codec.UI.Avalonia.Views
{
    using System;
    using System.Threading.Tasks;
    using Codec.UI.Avalonia.ViewModels;
    using global::Avalonia;
    using global::Avalonia.Controls;

    public partial class ThumbnailCell : UserControl
    {
        public ThumbnailCell() =>
            this.InitializeComponent();

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _ = this.BeginLoadAsync();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _ = this.BeginLoadAsync();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (this.DataContext is ThumbnailItemViewModel vm)
            {
                vm.Cancel();
            }
        }

        private async Task BeginLoadAsync()
        {
            if (this.DataContext is ThumbnailItemViewModel vm)
            {
                await vm.BeginLoad().ConfigureAwait(false);
            }
        }
    }
}
