// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Assimp;
    using Codec.Archives;
    using Codec.Files;
    using ImageMagick;

    public sealed class FileExportService(NestedFileSystemManager fsm, EntryTypeDetector detector)
    {
        public async Task SaveSingleAsync(Entry entry, Func<string, EntryType, string?, Task<string?>> pickSavePath)
        {
            if (!fsm.FileExists(entry.Path))
            {
                return;
            }

            var type = detector.Detect(entry);
            var path = await pickSavePath(fsm.GetFileName(entry.Path), type, detector[type]).ConfigureAwait(false);
            if (path is null)
            {
                return;
            }

            {
                using var input = fsm.OpenRead(entry.Path);
                if (!string.Equals(Path.GetExtension(path), fsm.GetExtension(entry.Path), StringComparison.OrdinalIgnoreCase))
                {
                    switch (type)
                    {
                        case EntryType.Image:
                            if (fsm.Resolve<MagickImage>(entry.Path) is MagickImage image)
                            {
                                image.Write(path);
                                return;
                            }

                            break;

                        case EntryType.Model:
                            if (fsm.Resolve<RenderableScene>(entry.Path) is { Scene: var scene })
                            {
                                var parentFolder = Path.GetDirectoryName(path);
                                var imageMap = new Dictionary<string, string>();
                                void UpdateTexture(bool run, Func<TextureSlot> get, Action<TextureSlot> set)
                                {
                                    if (run)
                                    {
                                        var texture = get();
                                        if (!imageMap.TryGetValue(texture.FilePath, out var filename))
                                        {
                                            var imagePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(entry.Path), texture.FilePath));

                                            // TODO: Recursively determine file rename behavior.
                                            imageMap[texture.FilePath] = filename = Path.ChangeExtension(fsm.GetFileName(texture.FilePath), ".png");
                                            if (fsm.Resolve<MagickImage>(imagePath) is MagickImage image)
                                            {
                                                image.Write(Path.Combine(parentFolder, filename));
                                            }

                                            ////imageMap[texture.FilePath] = filename = fsm.GetFileName(texture.FilePath);
                                            ////if (fsm.FileExists(imagePath))
                                            ////{
                                            ////    using var input = fsm.OpenRead(imagePath);
                                            ////    using var output = File.Create(Path.Combine(parentFolder, filename));
                                            ////    input.CopyTo(output);
                                            ////}
                                        }

                                        texture.FilePath = filename;
                                        set(texture);
                                    }
                                }

                                foreach (var mat in scene.Materials)
                                {
                                    UpdateTexture(mat.HasTextureAmbient, () => mat.TextureAmbient, v => mat.TextureAmbient = v);
                                    UpdateTexture(mat.HasTextureAmbientOcclusion, () => mat.TextureAmbientOcclusion, v => mat.TextureAmbientOcclusion = v);
                                    UpdateTexture(mat.HasTextureDiffuse, () => mat.TextureDiffuse, v => mat.TextureDiffuse = v);
                                    UpdateTexture(mat.HasTextureDisplacement, () => mat.TextureDisplacement, v => mat.TextureDisplacement = v);
                                    UpdateTexture(mat.HasTextureEmissive, () => mat.TextureEmissive, v => mat.TextureEmissive = v);
                                    UpdateTexture(mat.HasTextureHeight, () => mat.TextureHeight, v => mat.TextureHeight = v);
                                    UpdateTexture(mat.HasTextureLightMap, () => mat.TextureLightMap, v => mat.TextureLightMap = v);
                                    UpdateTexture(mat.HasTextureNormal, () => mat.TextureNormal, v => mat.TextureNormal = v);
                                    UpdateTexture(mat.HasTextureOpacity, () => mat.TextureOpacity, v => mat.TextureOpacity = v);
                                    UpdateTexture(mat.HasTextureReflection, () => mat.TextureReflection, v => mat.TextureReflection = v);
                                    UpdateTexture(mat.HasTextureSpecular, () => mat.TextureSpecular, v => mat.TextureSpecular = v);
                                }

                                var context = new AssimpContext();
                                var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                                var formatId = context.GetSupportedExportFormats()
                                    .FirstOrDefault(f => f.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                                    ?.FormatId;
                                if (formatId != null)
                                {
                                    context.ExportFile(scene, path, formatId);
                                    return;
                                }
                            }

                            break;
                    }
                }

                using var output = File.Create(path);
                await input.CopyToAsync(output).ConfigureAwait(false);
            }
        }

        public async Task SaveMultipleAsync(IEnumerable<Entry> entries, Func<Task<string?>> pickFolder, Func<string, Task<bool>> confirmOverwrite)
        {
            var path = await pickFolder().ConfigureAwait(true);
            if (path is null)
            {
                return;
            }

            var targetFiles = entries.Select(e => (Source: e.Path, Target: Path.Combine(path, Path.GetFileName(e.Path)))).ToList();
            if (targetFiles.Any(t => File.Exists(t.Target)))
            {
                var confirmed = await confirmOverwrite(path).ConfigureAwait(false);
                if (!confirmed)
                {
                    return;
                }
            }

            foreach (var (source, target) in targetFiles)
            {
                using var input = fsm.OpenRead(source);
                using var output = File.Create(target);
                await input.CopyToAsync(output).ConfigureAwait(false);
            }
        }

        public async Task ReplaceSingleAsync(Entry entry, Func<string, EntryType, string?, Task<string?>> pickReplacement)
        {
            var type = detector.Detect(entry);
            var path = await pickReplacement(fsm.GetFileName(entry.Path), type, detector[type]).ConfigureAwait(false);
            if (path is null)
            {
                return;
            }

            if (!string.Equals(Path.GetExtension(path), fsm.GetExtension(entry.Path), StringComparison.OrdinalIgnoreCase))
            {
                switch (detector.Detect(entry))
                {
                    case EntryType.Image:
                        if (fsm.Resolve<MagickImage>(path) is MagickImage image)
                        {
                            if (fsm.ResolveWriter<MagickImage>(entry.Path) is Action<MagickImage> writer)
                            {
                                writer(image);
                                return;
                            }
                        }

                        break;

                    case EntryType.Audio:
                        {
                        }

                        break;

                    case EntryType.Model:
                        {
                        }

                        break;
                }
            }

            using var input = File.OpenRead(path);
            using var output = fsm.Open(path, new()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Write,
            });
            await input.CopyToAsync(output).ConfigureAwait(false);
        }
    }
}
