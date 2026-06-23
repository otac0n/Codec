namespace Codec.Files
{
    using System.Linq;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;

    public static class ImageMagickBitmapResolver
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryTypeDetector.EntryType.Image, string.Join(";", MagickNET.SupportedFormats.Where(f => f.SupportsReading).Select(f => $"*.{f.Format.ToString().ToLowerInvariant()}"))));

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                MagickImageInfo? fileInfo = null;
                try
                {
                    using var input = parent.File.OpenRead(parentRelativePath);
                    fileInfo = new MagickImageInfo(input);
                }
                catch (MagickDelegateErrorException)
                {
                }
                catch (MagickMissingDelegateErrorException)
                {
                }

                if (fileInfo != null)
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var input = parent.File.OpenRead(parentRelativePath);
                        return new MagickImage(input);
                    };
                }

                return null;
            });
        }
    }
}
