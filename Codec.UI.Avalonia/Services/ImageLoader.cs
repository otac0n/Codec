namespace Codec.UI.Avalonia.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Avalonia.Media.Imaging;
    using Codec.Archives;
    using ImageMagick;

    public sealed class ImageLoader(NestedFileSystemManager fsm) : IDisposable
    {
        private readonly SemaphoreSlim semaphore = new(5);

        public async Task<Bitmap?> LoadAsync(Entry entry, CancellationToken cancel = default)
        {
            // TODO: Handle muti-frame images.
            await this.semaphore.WaitAsync(cancel).ConfigureAwait(false);
            MagickImage? magickImage = null;
            try
            {
                try
                {
                    magickImage = fsm.Resolve<MagickImage>(entry.Path);
                }
                finally
                {
                    this.semaphore.Release();
                }

                return ConvertToAvaloniaBitmap(magickImage);
            }
            finally
            {
                magickImage?.Dispose();
            }
        }

        private static Bitmap? ConvertToAvaloniaBitmap(MagickImage? src)
        {
            if (src is null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            src.Write(ms, MagickFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            return new Bitmap(ms);
        }

        public void Dispose()
        {
            this.semaphore.Dispose();
        }
    }
}
