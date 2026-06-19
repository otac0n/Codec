namespace Codec.Files
{
    using System;
    using System.Collections.Generic;
    using Assimp;
    using Microsoft.Extensions.DependencyInjection;

    public class AssimpNetGeometryResolver
    {
        public static void Register(IServiceCollection services)
        {
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var probe = new AssimpContext())
            {
                supportedExtensions.UnionWith(probe.GetSupportedImportFormats());
            }

            services.AddSingleton<FileHandlerResolver<RenderableScene>>(
                (serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
                {
                    var extension = parent.Path.GetExtension(parentRelativePath);
                    if (supportedExtensions.Contains(extension))
                    {
                        return (fullPath, parentRelativePath, parent, parentPath) =>
                        {
                            using var context = new AssimpContext();
                            using var input = parent.File.OpenRead(parentRelativePath);

                            try
                            {
                                return (RenderableScene)context.ImportFileFromStream(
                                    input,
                                    PostProcessSteps.None,
                                    extension.TrimStart('.'));
                            }
                            catch (AssimpException)
                            {
                                return null;
                            }
                        };
                    }

                    return null;
                });
        }
    }
}
