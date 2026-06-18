namespace Codec.Files
{
    using System.Drawing;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;

    public static class ImageMagickBitmapResolver
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<Bitmap>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                MagickImageInfo? fileInfo = null;
                try
                {
                    using var input = parent.File.OpenRead(parentRelativePath);
                    fileInfo = new MagickImageInfo(input);
                }
                catch (MagickMissingDelegateErrorException)
                {
                }

                if (fileInfo != null)
                {
                    var seed = serviceProvider.GetRequiredService<ArchiveOptions>().Key;
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var input = parent.File.OpenRead(parentRelativePath);
                        using var image = new MagickImage(input);
                        return image.ToBitmap();
                    };
                }

                return null;
            });
        }
    }
}
