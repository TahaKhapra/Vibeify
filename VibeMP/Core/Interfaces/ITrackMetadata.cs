namespace VibeMP.Core.Interfaces
{
    public interface ITrackMetadata
    {
        string FilePath { get; }
        string Title { get; }
        string Artist { get; }
        string Album { get; }
        TimeSpan Duration { get; }
        byte[]? AlbumArtRaw { get; }
    }
}