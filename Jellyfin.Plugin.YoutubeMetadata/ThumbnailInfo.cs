namespace Jellyfin.Plugin.YoutubeMetadata;

/// <summary>
/// Object should match how YTDL json looks.
/// </summary>
#pragma warning disable IDE1006 // Naming Styles
public class ThumbnailInfo {
	public string url { get; set; }
	public int width { get; set; }
	public int height { get; set; }
	public string resolution { get; set; }
	public string id { get; set; }
}
#pragma warning restore IDE1006 // Naming Styles
