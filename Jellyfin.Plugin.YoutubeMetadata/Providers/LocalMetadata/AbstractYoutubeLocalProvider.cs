using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.YoutubeMetadata.Providers;

public abstract class AbstractYoutubeLocalProvider<B, T> : ILocalMetadataProvider<T>, IHasItemChangeMonitor where T : BaseItem {
    protected readonly ILogger<B> _logger;
    protected readonly IFileSystem _fileSystem;

    /// <summary>
    /// Providers name, this appears in the library metadata settings.
    /// </summary>
    public abstract string Name { get; }

    protected AbstractYoutubeLocalProvider(IFileSystem fileSystem, ILogger<B> logger) {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    protected FileSystemMetadata GetInfoJson(string path) {
        _logger.LogDebug("YTLocal GetInfoJson: {Path}", path);
        var fileInfo = _fileSystem.GetFileSystemInfo(path);
        var directoryInfo = fileInfo.IsDirectory ? fileInfo : _fileSystem.GetDirectoryInfo(Path.GetDirectoryName(path));
        var directoryPath = directoryInfo.FullName;

        var files = _fileSystem.GetFiles(directoryPath);

        var specificFile = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(path) + ".info.json");

        var file = _fileSystem.GetFileInfo(specificFile);

        if (file.Exists) {
            _logger.LogInformation("Found info file of the same name as containing folder {FileName}", file.Name);
            return file;
        }

        var infoFiles = files.Where(a => a.Name.EndsWith(".info.json")).ToArray();

        if (infoFiles.Length == 1) {
            _logger.LogInformation("Found info file {FileName} within {Directory}", infoFiles[0].Name, directoryPath);
            return infoFiles[0];
        }
        _logger.LogInformation("No .info.json Found in {Directory}", directoryPath);

        return file;
    }

    /// <summary>
    /// Returns boolean if item has changed since last recorded.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="directoryService"></param>
    /// <returns></returns>
    public bool HasChanged(BaseItem item, IDirectoryService directoryService) {
        var infoJson = GetInfoJson(item.Path);
        var filetime = _fileSystem.GetLastWriteTimeUtc(infoJson);
        var result = infoJson.Exists && filetime > item.DateLastSaved;
        return result;
    }

    /// <summary>
    /// Retrieves metadata of item.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="directoryService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<MetadataResult<T>> GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken) {
        _logger.LogDebug("YTLocal GetMetadata: {Path}", info.Path);
        var result = new MetadataResult<T>();

        var infoFile = Path.ChangeExtension(info.Path, "info.json");
        if (!File.Exists(infoFile))
        {
            return Task.FromResult(result);
        }

        var jsonObj = Utils.ReadYTDLInfo(infoFile, cancellationToken);
        if (jsonObj != null) {
            _logger.LogDebug("YTLocal GetMetadata Result: {JSON}", jsonObj.ToString());
            result = this.GetMetadataImpl(jsonObj);
        }

        return Task.FromResult(result);
    }

    internal abstract MetadataResult<T> GetMetadataImpl(YTDLData jsonObj);
}
