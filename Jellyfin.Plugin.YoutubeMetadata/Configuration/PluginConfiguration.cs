using MediaBrowser.Model.Plugins;


namespace Jellyfin.Plugin.YoutubeMetadata.Configuration;

public class PluginConfiguration : BasePluginConfiguration {
	public IDTypes IDType { get; set; }
	public PluginConfiguration() {
		// defaults
		IDType = IDTypes.YTDLP;
	}


}