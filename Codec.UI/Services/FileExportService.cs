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
        public async Task SaveSingleAsync(NestedFileSystemManager.Entry entry, Func<string, EntryTypeDetector.EntryType, string?, Task<string?>> pickSavePath)
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
                        case EntryTypeDetector.EntryType.Image:
                            if (fsm.Resolve<MagickImage>(entry.Path) is MagickImage image)
                            {
                                image.Write(path);
                                return;
                            }

                            break;

                        case EntryTypeDetector.EntryType.Model:
                            if (fsm.Resolve<RenderableScene>(entry.Path) is RenderableScene scene)
                            {
                                // TODO: Handle linked image exports & renames.
                                var context = new AssimpContext();
                                var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                                var formatId = context.GetSupportedExportFormats()
                                    .FirstOrDefault(f => f.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                                    ?.FormatId;
                                if (formatId != null)
                                {
                                    context.ExportFile(scene.Scene, path, formatId);
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

        public async Task SaveMultipleAsync(IEnumerable<NestedFileSystemManager.Entry> entries, Func<Task<string?>> pickFolder, Func<string, Task<bool>> confirmOverwrite)
        {
            var path = await pickFolder().ConfigureAwait(false);
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
    }
}
