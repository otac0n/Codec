namespace Codec.Services
{
    using System.IO.Abstractions;
    using Codec.Archives;
    using Entry = Codec.Archives.NestedFileSystemManager.Entry;

    public sealed class EntryTypeDetector(NestedFileSystemManager fsm)
    {
        public EntryType Detect(Entry entry)
        {
            if (entry.CanEnumerateEntries && !entry.CanOpen)
            {
                return EntryType.Folder;
            }

            if (entry.CanEnumerateEntries)
            {
                return EntryType.Archive;
            }

            // TODO: Run through our collection of FileHandlerResolvers here.

            return fsm.GetExtension(entry.Path).ToUpperInvariant() switch
            {
                ".BMP" or
                ".CTXR" or
                ".GIF" or
                ".IMG" or
                ".TIF" or ".TIFF" or
                ".TM2" or
                ".PCX" or
                ".PNG" or
                ".JPG" or ".JPEG" or
                ".WEBP" => EntryType.Image,

                ".AVI" or
                ".MOV" or
                ".MP4" or
                ".MKV" or
                ".WEBM" => EntryType.Video,

                ".CDA" or ".CDDA" or
                ".MID" or ".MIDI" or
                ".MP3" or
                ".OGG" or
                ".WAV" or
                ".WMA" or ".XWMA" => EntryType.Audio,

                _ => EntryType.File,
            };
        }

        public enum EntryType
        {
            Folder = 0,
            File = 1,
            Archive = 2,
            Image = 3,
            Video = 4,
            Audio = 5,
        }
    }
}
