using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;

namespace Tubifarry.Download.Clients.SquidQobuz;

public class SquidQobuzCompletedItemRecovery(
    IDiskProvider diskProvider,
    IDownloadHistoryService downloadHistoryService)
{
    public IEnumerable<DownloadClientItem> GetCompletedItems(string downloadPath, DownloadClientItemClientInfo clientInfo)
    {
        if (!diskProvider.FolderExists(downloadPath))
            yield break;

        foreach (string folder in diskProvider.GetDirectories(downloadPath))
        {
            DownloadHistory? history = downloadHistoryService.GetLatestGrab(folder);
            if (history == null)
                continue;

            long totalSize = diskProvider.GetFiles(folder, true).Sum(diskProvider.GetFileSize);

            yield return new DownloadClientItem
            {
                DownloadClientInfo = clientInfo,
                DownloadId = folder,
                Title = history.Release?.Title ?? history.SourceTitle,
                TotalSize = totalSize,
                RemainingSize = 0,
                Status = DownloadItemStatus.Completed,
                OutputPath = new OsPath(folder),
                RemainingTime = TimeSpan.Zero,
                CanBeRemoved = true,
                CanMoveFiles = true
            };
        }
    }
}
