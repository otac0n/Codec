namespace Codec.Files
{
    using System;
    using System.Linq;
    using Codec.Services;
    using ImageMagick;
    using Microsoft.Extensions.DependencyInjection;

    public static class ImageMagickBitmapResolver
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Image, string.Join(";", MagickNET.SupportedFormats.Where(f => f.SupportsReading).Select(f => $"*.{f.Format.ToString().ToLowerInvariant()}"))));

            services.AddSingleton<FileHandlerResolver<MagickImage>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var readSettings = new MagickReadSettings();
                MagickImageInfo? fileInfo = null;
                try
                {
                    using var input = parent.File.OpenRead(parentRelativePath);
                    fileInfo = new MagickImageInfo(input, readSettings);
                }
                catch (MagickDelegateErrorException)
                {
                }
                catch (MagickMissingDelegateErrorException)
                {
                    if (Enum.TryParse<MagickFormat>(PathExtensions.GetExtension(parentRelativePath)?.TrimStart('.'), true, out var detectedFormat))
                    {
                        readSettings.Format = detectedFormat;

                        try
                        {
                            using var input = parent.File.OpenRead(parentRelativePath);
                            fileInfo = new MagickImageInfo(input, readSettings);
                        }
                        catch (MagickDelegateErrorException)
                        {
                        }
                        catch (MagickMissingDelegateErrorException)
                        {
                        }
                    }
                }

                if (fileInfo != null)
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var input = parent.File.OpenRead(parentRelativePath);
                        return new MagickImage(input, readSettings);
                    };
                }

                return null;
            });
        }
    }
}
