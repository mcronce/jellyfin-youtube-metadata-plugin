using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.YoutubeMetadata.Providers;

public class YoutubeLocalMovieProvider : AbstractYoutubeLocalProvider<YoutubeLocalMovieProvider, Movie> {
    public YoutubeLocalMovieProvider(IFileSystem fileSystem, ILogger<YoutubeLocalMovieProvider> logger) : base(fileSystem, logger) { }

    public override string Name => Constants.ProviderId;

    internal override MetadataResult<Movie> GetMetadataImpl(YTDLData jsonObj, FileSystemMetadata _file) {
        return Utils.YTDLJsonToMovie(jsonObj);
    }
}
