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
                return new((fullPath, parentRelativePath, parent, parentPath) =>
                {
                    var readSettings = new MagickReadSettings();
                    using var input = parent.File.OpenRead(parentRelativePath);

                    try
                    {
                        return new MagickImage(input, readSettings);
                    }
                    catch (MagickMissingDelegateErrorException)
                    {
                        if (Enum.TryParse<MagickFormat>(PathExtensions.GetExtension(parentRelativePath)?.TrimStart('.'), true, out var detectedFormat))
                        {
                            readSettings.Format = detectedFormat;
                            input.Position = 0;
                            return new MagickImage(input, readSettings);
                        }
                    }

                    return null;
                });
            });
        }
    }
}
