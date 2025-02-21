using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.YoutubeMetadata;

public class EpisodeIndexer : ILibraryPostScanTask, IScheduledTask {
    protected readonly ILibraryManager _libmanager;
    protected readonly IItemRepository _repository;
    protected readonly ILogger<EpisodeIndexer> _logger;
    public EpisodeIndexer(
            ILibraryManager libmanager,
            IItemRepository repository,
            ILogger<EpisodeIndexer> logger) {
        _libmanager = libmanager;
        _repository = repository;
        _logger = logger;
    }

    public string Name => "Youtube Metadata Episode Indexer";

    public string Key => "Youtube Metadata Episode Indexer";

    public string Description => Constants.PluginName;

    public string Category => Constants.PluginName;

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) {
        await Run(progress, cancellationToken);
        return;
    }

    public async Task Execute(IProgress<double> progress, CancellationToken cancellationToken) {
        await Run(progress, cancellationToken);
        return;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() {
        return new[]
        {
            new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks }
        };
    }

    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken) {
        _logger.LogDebug("Starting Reindexing episodes");
        var shows = _repository.GetItems(new InternalItemsQuery {
            Recursive = true,
            IncludeItemTypes = new[] { BaseItemKind.Series },
            DtoOptions = new DtoOptions()
        });

        var count = 0;
        foreach (var show in shows.Items) {
            if (!show.ProviderIds.ContainsKey(Constants.ProviderId)) {
                _logger.LogDebug("Skipping show {Name}", show.Name);
                continue;
            }

            _logger.LogDebug("Indexing show {Name}", show.Name);
            var seasons = new List<BaseItem>(_repository.GetItems(new InternalItemsQuery {
                ParentId = show.Id,
                IncludeItemTypes = new[] { BaseItemKind.Season },
                DtoOptions = new DtoOptions()
            }).Items);

            seasons.Sort(delegate (BaseItem x, BaseItem y) {
                if (x.Name == null && y.Name == null) {
                    return 0;
                }

                if (x.Name == null) {
                    return -1;
                }

                if (y.Name == null) {
                    return 1;
                }

                return x.Name.CompareTo(y.Name);
            });
            var sindex = 1;
            foreach (var season in seasons) {
                season.IndexNumber = sindex;
                _logger.LogDebug("Indexing season {Name} as index {Index}", season.Name, sindex);
                await _libmanager.UpdateItemAsync(season, show, ItemUpdateType.MetadataEdit, cancellationToken);
                var episodes = new List<BaseItem>(_repository.GetItems(new InternalItemsQuery {
                    AncestorIds = new[] { season.Id },
                    IncludeItemTypes = new[] { BaseItemKind.Episode },
                    DtoOptions = new DtoOptions()
                }).Items);
                episodes.Sort(delegate (BaseItem x, BaseItem y) {
                    if (!x.PremiereDate.HasValue && !y.PremiereDate.HasValue) {
                        _logger.LogWarning("Episode [{Name}] does not have 'PremiereDate'", x.FileNameWithoutExtension);
                        _logger.LogWarning("Episode [{Name}] does not have 'PremiereDate'", y.FileNameWithoutExtension);
                        return 0;
                    }
                    if (!x.PremiereDate.HasValue) {
                        _logger.LogWarning("Episode [{Name}] does not have 'PremiereDate'", x.FileNameWithoutExtension);
                        return -1;
                    }
                    if (!y.PremiereDate.HasValue) {
                        _logger.LogWarning("Episode [{Name}] does not have 'PremiereDate'", y.FileNameWithoutExtension);
                        return 1;
                    }

                    return DateTime.Compare(x.PremiereDate.Value, y.PremiereDate.Value);
                });

                var should_use_date = true;
                foreach(var episode in episodes) {
                    if(!episode.PremiereDate.HasValue) {
                        _logger.LogInformation("Episode {name} does not have a premiere date; using simple indexing for season {season}", episode.Name, season.Name);
                        should_use_date = false;
                    }
                }

                if(should_use_date) {
                    var index_for_date = 0;
                    DateTime? most_recent_date = null;
                    foreach (var episode in episodes) {
                        if(episode.PremiereDate == most_recent_date) {
                            index_for_date++;
                            _logger.LogDebug("Episode [{Name}] already had one aired on the same date; incrementing index to {index_for_date}", episode.Name, index_for_date);
                        } else {
                            index_for_date = 0;
                            most_recent_date = episode.PremiereDate;
                        }

                        var eindex = (episode.PremiereDate.Value.Month * 1000) + (episode.PremiereDate.Value.Day * 10) + index_for_date;

                        episode.IndexNumber = eindex;
                        episode.ParentIndexNumber = episode.PremiereDate.Value.Year;

                        _logger.LogInformation("Episode [{Name} - {Date:MM/dd/yyyy}] should now be season {season} episode {Index}", episode.Name, episode.PremiereDate, episode.ParentIndexNumber, episode.IndexNumber);
                        await _libmanager.UpdateItemAsync(episode, season, ItemUpdateType.MetadataEdit, cancellationToken);
                    }
                } else {
                    var eindex = 1;
                    foreach(var episode in episodes) {
                        if (episode.PremiereDate.HasValue) {
                            _logger.LogInformation("Episode [{Name} - {Date:MM/dd/yyyy}] should now be index {Index}", episode.Name, episode.PremiereDate, eindex);
                        } else {
                            _logger.LogInformation("Episode {Name} should now be index {Index}", episode.Name, eindex);
                        }
                        episode.IndexNumber = eindex;
                        episode.ParentIndexNumber = sindex;
                        await _libmanager.UpdateItemAsync(episode, season, ItemUpdateType.MetadataEdit, cancellationToken);
                        eindex++;
                    }
                }
                sindex++;
            }
            count++;
            var percent = ((double)count / shows.Items.Count) * 100;
            progress.Report(percent);
        }
        return;
    }
}
