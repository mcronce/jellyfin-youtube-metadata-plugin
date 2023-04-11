using System.IO.Abstractions;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.YoutubeMetadata;

/// <summary>
/// Register webhook services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator {
	/// <inheritdoc />
	public void RegisterServices(IServiceCollection serviceCollection) {
		serviceCollection.AddScoped<IFileSystem, FileSystem>();
	}
}
