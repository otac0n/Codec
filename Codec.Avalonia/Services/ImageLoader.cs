namespace Codec.Avalonia.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Avalonia.Media.Imaging;
    using Codec.Archives;
    using Codec.Files;
    using Entry = Codec.Archives.NestedFileSystemManager.Entry;

    public sealed class ImageLoader(
        IServiceProvider serviceProvider,
        NestedFileSystemManager fsm,
        IEnumerable<FileHandlerResolver<System.Drawing.Bitmap>> resolvers) : IDisposable
    {
        private readonly SemaphoreSlim semaphore = new(5);

        public async Task<Bitmap?> LoadAsync(Entry entry, CancellationToken cancel = default)
        {
            await this.semaphore.WaitAsync(cancel).ConfigureAwait(false);
            Bitmap bmp;
            System.Drawing.Bitmap? drawingBitmap = null;
            try
            {
                try
                {
                    drawingBitmap = fsm.Resolve<System.Drawing.Bitmap>(entry.Path);
                }
                finally
                {
                    this.semaphore.Release();
                }

                bmp = ConvertToAvaloniaBitmap(drawingBitmap);
            }
            finally
            {
                drawingBitmap?.Dispose();
            }

            return bmp;
        }

        private static Bitmap? ConvertToAvaloniaBitmap(System.Drawing.Bitmap? src)
        {
            if (src is null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            src.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            return new Bitmap(ms);
        }

        public void Dispose()
        {
            this.semaphore.Dispose();
        }
    }
}
