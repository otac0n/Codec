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
    using EntryItem = (Codec.Archives.Entry Entry, EntryType EntryType);

    public sealed class FileExportService(NestedFileSystemManager fsm, EntryTypeDetector detector)
    {
        public async Task SaveSingleAsync(EntryItem item, Func<string, EntryType, string?, Task<string?>> pickSavePath)
        {
            if (!fsm.FileExists(item.Entry.Path))
            {
                return;
            }

            var path = await pickSavePath(fsm.GetFileName(item.Entry.Path), item.EntryType, detector[item.EntryType]).ConfigureAwait(false);
            if (path is null)
            {
                return;
            }

            var imageMap = new Dictionary<string, string>();
            var parentFolder = PathExtensions.GetDirectoryName(path);
            var isConverting = !string.Equals(PathExtensions.GetExtension(item.Entry.Path), PathExtensions.GetExtension(path), StringComparison.OrdinalIgnoreCase);
            async Task<string> SaveRelatedFileAsync(EntryItem subItem)
            {
                if (!imageMap.TryGetValue(subItem.Entry.Path, out var filename) && isConverting)
                {
                    imageMap[subItem.Entry.Path] = filename = Path.Combine(parentFolder, PathExtensions.ChangeExtension(fsm.GetFileName(subItem.Entry.Path), ".png"));

                    await this.SaveToDestination(subItem, filename, SaveRelatedFileAsync).ConfigureAwait(false);
                }

                return filename ?? subItem.Entry.Path;
            }

            await this.SaveToDestination(item, path, SaveRelatedFileAsync).ConfigureAwait(false);
        }

        public async Task SaveToDestination(EntryItem item, string destination, Func<EntryItem, Task<string>> getOtherResourceDestination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            var convertFormat = !string.Equals(Path.GetExtension(destination), fsm.GetExtension(item.Entry.Path), StringComparison.OrdinalIgnoreCase);

            if (item.EntryType == EntryType.Model)
            {
                if (fsm.Resolve<RenderableScene>(item.Entry.Path) is { Scene: var scene })
                {
                    var updated = false;

                    async Task UpdateTexture(bool run, Func<TextureSlot> get, Action<TextureSlot> set)
                    {
                        if (run)
                        {
                            var texture = get();
                            var parentFolder = PathExtensions.GetDirectoryName(item.Entry.Path);
                            var absolutePath = fsm.GetFullPath(texture.FilePath, parentFolder);

                            EntryType subEntryType;
                            EntryItem subItem =
                                fsm.TryGetEntry(absolutePath, out var subEntry)
                                    ? (subEntry, subEntryType = detector.Detect(subEntry))
                                    : (subEntry = new(absolutePath, true, false), subEntryType = EntryType.Image);

                            var newPath = await getOtherResourceDestination(subItem).ConfigureAwait(false);
                            newPath = fsm.GetRelativePath(PathExtensions.GetDirectoryName(destination), newPath);
                            if (newPath != texture.FilePath)
                            {
                                texture.FilePath = newPath;
                                updated = true;
                                set(texture);
                            }
                        }
                    }

                    foreach (var mat in scene.Materials)
                    {
                        await UpdateTexture(mat.HasTextureAmbient, () => mat.TextureAmbient, v => mat.TextureAmbient = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureAmbientOcclusion, () => mat.TextureAmbientOcclusion, v => mat.TextureAmbientOcclusion = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureDiffuse, () => mat.TextureDiffuse, v => mat.TextureDiffuse = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureDisplacement, () => mat.TextureDisplacement, v => mat.TextureDisplacement = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureEmissive, () => mat.TextureEmissive, v => mat.TextureEmissive = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureHeight, () => mat.TextureHeight, v => mat.TextureHeight = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureLightMap, () => mat.TextureLightMap, v => mat.TextureLightMap = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureNormal, () => mat.TextureNormal, v => mat.TextureNormal = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureOpacity, () => mat.TextureOpacity, v => mat.TextureOpacity = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureReflection, () => mat.TextureReflection, v => mat.TextureReflection = v).ConfigureAwait(false);
                        await UpdateTexture(mat.HasTextureSpecular, () => mat.TextureSpecular, v => mat.TextureSpecular = v).ConfigureAwait(false);
                    }

                    if (convertFormat || updated)
                    {
                        var context = new AssimpContext();
                        var ext = Path.GetExtension(destination).TrimStart('.').ToLowerInvariant();
                        var formatId = context.GetSupportedExportFormats()
                            .FirstOrDefault(f => f.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                            ?.FormatId;
                        if (formatId != null)
                        {
                            context.ExportFile(scene, destination, formatId);
                            return;
                        }
                    }
                }
            }
            else
            {
                if (convertFormat)
                {
                    switch (item.EntryType)
                    {
                        case EntryType.Audio:
                            // Not implemented.
                            break;

                        case EntryType.Image:
                            if (fsm.Resolve<MagickImage>(item.Entry.Path) is MagickImage image)
                            {
                                await image.WriteAsync(destination).ConfigureAwait(false);
                                return;
                            }

                            break;

                        case EntryType.Model:
                            // Handled above.
                            break;
                    }
                }
            }

            using var input = fsm.OpenRead(item.Entry.Path);
            using var output = File.Create(destination);
            await input.CopyToAsync(output).ConfigureAwait(false);
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
            using var output = fsm.Open(entry.Path, new()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Write,
            });
            await input.CopyToAsync(output).ConfigureAwait(false);
        }
    }
}
