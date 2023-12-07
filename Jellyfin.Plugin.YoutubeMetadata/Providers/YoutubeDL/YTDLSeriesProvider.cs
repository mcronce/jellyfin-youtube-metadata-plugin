using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace Jellyfin.Plugin.YoutubeMetadata.Providers;

/// <summary>
/// Tvdb series provider.
/// </summary>
public class YTDLSeriesProvider : AbstractYoutubeRemoteProvider<YTDLSeriesProvider, Series, SeriesInfo> {

    /// <summary>
    /// Initializes a new instance of the <see cref="TvdbSeriesProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TvdbSeriesProvider}"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public YTDLSeriesProvider(
            IFileSystem fileSystem,
            IHttpClientFactory httpClientFactory,
            ILogger<YTDLSeriesProvider> logger,
            IServerConfigurationManager config,
            System.IO.Abstractions.IFileSystem afs) : base(fileSystem, httpClientFactory, logger, config, afs) {
    }

    /// <inheritdoc />
    public override string Name => Constants.ProviderId;

    public override async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken) {
        _logger.LogDebug("YTDLSeries GetMetadata: {Path}", info.Path);
        MetadataResult<Series> result = new();
        var name = info.Name;
        if (string.IsNullOrWhiteSpace(name)) {
            _logger.LogDebug("YTDLSeries GetMetadata: No name found for media: ", info.Path);
            result.HasMetadata = false;
            return result;
        }
        var ytPath = GetVideoInfoPath(this._config.ApplicationPaths, name);
        _logger.LogDebug("YTDLSeries GetMetadata: path: {Path} ", ytPath);
        var fileInfo = _fileSystem.GetFileSystemInfo(ytPath);
        _logger.LogDebug("YTDLSeries GetMetadata: FileInfo: {Path} ", fileInfo.Name);

        YTDLData video = null;
        try {
            if (!IsFresh(fileInfo)) {
                _logger.LogDebug("YTDLSeries GetMetadata: {info.Name} is not fresh.", fileInfo.Name);
                await this.GetAndCacheMetadata(name, this._config.ApplicationPaths, cancellationToken);
            }
            video = Utils.ReadYTDLInfo(ytPath, cancellationToken);
        } catch(YoutubeDlMissingException) {
            _logger.LogWarning("YTDLSeries::GetMetadata():  Failed to download metadata for {Name}", name);
            foreach(var file in Directory.EnumerateFiles(info.Path, "*.info.json", SearchOption.AllDirectories)) {
                try {
                    video = Utils.ReadYTDLInfo(file, cancellationToken);
                    _logger.LogInformation("YTDLSeries::GetMetadata():  Read metadata from {file} as backup", file);
                    break;
                } catch(Exception e) {
                    _logger.LogError("YTDLSeries::GetMetadata():  Failed to read metadata from {file}: {e}", file, e);
                }
            }
        }
        _logger.LogDebug("YTDLSeries::GetMetadata():  video = {video}", video);

        if (video != null) {
            try {
                result = this.GetMetadataImpl(video, video.channel_id);
            }
            catch (System.ArgumentException e) {
                _logger.LogError("YTDLSeries GetMetadata: Error parsing json: ");
                _logger.LogError(video.ToString());
                _logger.LogError(video.title);
                _logger.LogError(e.Message);
            }
        } else {
            _logger.LogError("YTDLSeries::GetMetadata():  Failed to find metadata for {name}", name);
        }
        return result;
    }

    internal override MetadataResult<Series> GetMetadataImpl(YTDLData jsonObj, string id) {
        var result = Utils.YTDLJsonToSeries(jsonObj);
        _logger.LogDebug("YTDLSeries::GetMetadataImpl():  Show {Name} got provider {Provider}", result.Item.Name, result.Item.ProviderIds[Constants.ProviderId]);
        return result;
    }

    internal override async Task GetAndCacheMetadata(
            string name,
            IServerApplicationPaths appPaths,
            CancellationToken cancellationToken) {
        _logger.LogDebug("YTDLSeries GetMetadataImpl: GetAndCacheMetadata {Name}", name);
        var ytPath = GetVideoInfoPath(this._config.ApplicationPaths, name);
        var fileInfo = _fileSystem.GetFileSystemInfo(ytPath);
        if (!IsFresh(fileInfo)) {
            _logger.LogDebug("YTDLSeries GetMetadataImpl: {Name} is not fresh", fileInfo.Name);
            var searchResult = Utils.SearchChannel(name, appPaths, cancellationToken);
            await searchResult;
            await Utils.GetChannelInfo(searchResult.Result, name, appPaths, cancellationToken);
        }

    }
    public override Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) {
        _logger.LogDebug("YTDLSeries GetImageResponse: {URL}", url);
        return _httpClientFactory.CreateClient(Constants.ProviderId).GetAsync(url, cancellationToken);
    }
}
