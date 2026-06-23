namespace Codec.Services
{
    public enum EntryType
    {
        Folder = 0,
        File = 1,
        Archive = 2,
        Image = 3,
        Video = 4,
        Audio = 5,
        Model = 6,
    }

    public record EntryTypeMatcher(EntryType EntryType, string GlobPattern);
}
