using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.YoutubeMetadata.Providers.LocalMetadata;

public class YoutubeLocalSeriesProvider : ILocalMetadataProvider<Series>, IHasItemChangeMonitor {
    protected readonly ILogger<YoutubeLocalSeriesProvider> _logger;
    protected readonly IFileSystem _fileSystem;
    public YoutubeLocalSeriesProvider(IFileSystem fileSystem, ILogger<YoutubeLocalSeriesProvider> logger) {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string Name => Constants.ProviderId;

    private string GetSeriesInfo(string path) {
        Matcher matcher = new();
        matcher.AddInclude("**/*.info.json");
        Regex rx = new Regex(Constants.YTCHANNEL_RE, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        foreach (string file in matcher.GetResultsInFullPath(path)) {
            if (rx.IsMatch(file)) {
                return file;
            }
        }
        return null;
    }
    public Task<MetadataResult<Series>> GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken) {
        MetadataResult<Series> result = new();
        string infoPath = GetSeriesInfo(info.Path);
        if (String.IsNullOrEmpty(infoPath)) {
            return Task.FromResult(result);
        }
        var infoJson = Utils.ReadYTDLInfo(infoPath, cancellationToken);
        result = Utils.YTDLJsonToSeries(infoJson);
        return Task.FromResult(result);
    }
    FileSystemMetadata GetInfoJson(string path) {
        var fileInfo = _fileSystem.GetFileSystemInfo(path);
        var directoryInfo = fileInfo.IsDirectory ? fileInfo : _fileSystem.GetDirectoryInfo(Path.GetDirectoryName(path));
        var directoryPath = directoryInfo.FullName;
        var specificFile = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(path) + ".info.json");
        var file = _fileSystem.GetFileInfo(specificFile);
        return file;
    }
    public bool HasChanged(BaseItem item, IDirectoryService directoryService) {
        var infoPath = GetSeriesInfo(item.Path);
        var result = false;
        if (!String.IsNullOrEmpty(infoPath)) {
            var infoJson = GetInfoJson(infoPath);
            result = infoJson.Exists && _fileSystem.GetLastWriteTimeUtc(infoJson) < item.DateLastSaved;
        }
        return result;

    }
}
