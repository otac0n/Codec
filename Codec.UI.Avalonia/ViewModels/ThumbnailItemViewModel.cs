namespace Codec.UI.Avalonia.ViewModels
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Avalonia.Media.Imaging;
    using Codec.UI.Avalonia.Models;
    using Codec.UI.Avalonia.Services;
    using CommunityToolkit.Mvvm.ComponentModel;

    public sealed partial class ThumbnailItemViewModel : ObservableObject, IDisposable
    {
        private readonly ImageLoader loader;
        private readonly CancellationTokenSource cts;

        public EntryItem Item { get; }

        [ObservableProperty] private Bitmap? thumbnail;
        [ObservableProperty] private bool isLoading = true;

        public ThumbnailItemViewModel(EntryItem item, ImageLoader loader, CancellationToken ct)
        {
            this.Item = item;
            this.loader = loader;
            this.cts = new CancellationTokenSource();
            ct.Register(this.cts.Cancel);
        }

        public async Task BeginLoad()
        {
            try
            {
                this.Thumbnail = await this.loader.LoadAsync(this.Item.Entry, this.cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        internal void Cancel() =>
            this.cts.Cancel();

        public void Dispose()
        {
            this.cts.Cancel();
            this.cts.Dispose();
            this.Thumbnail?.Dispose();
#pragma warning disable MVVMTK0034
            this.thumbnail = null;
#pragma warning restore MVVMTK0034
        }
    }
}
