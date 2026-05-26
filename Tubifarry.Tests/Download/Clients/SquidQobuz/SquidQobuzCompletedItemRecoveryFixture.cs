using Moq;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using Tubifarry.Download.Clients.SquidQobuz;
using Xunit;

namespace Tubifarry.Tests.Download.Clients.SquidQobuz;

public class SquidQobuzCompletedItemRecoveryFixture
{
    [Fact]
    public void GetCompletedItems_returns_only_download_folders_with_grab_history()
    {
        const string root = @"C:\downloads\squid";
        const string grabbedFolder = @"C:\downloads\squid\Artist - Album";
        const string unrelatedFolder = @"C:\downloads\squid\Unrelated";

        Mock<IDiskProvider> diskProvider = new();
        Mock<IDownloadHistoryService> downloadHistoryService = new();

        diskProvider.Setup(x => x.FolderExists(root)).Returns(true);
        diskProvider.Setup(x => x.GetDirectories(root)).Returns([grabbedFolder, unrelatedFolder]);
        diskProvider.Setup(x => x.GetFiles(grabbedFolder, true)).Returns([grabbedFolder + @"\01 - Track.flac"]);
        diskProvider.Setup(x => x.GetFileSize(grabbedFolder + @"\01 - Track.flac")).Returns(4096);
        downloadHistoryService.Setup(x => x.GetLatestGrab(grabbedFolder)).Returns(new DownloadHistory
        {
            DownloadId = grabbedFolder,
            SourceTitle = "Artist - Album [FLAC 16bit] [WEB]"
        });

        SquidQobuzCompletedItemRecovery recovery = new(diskProvider.Object, downloadHistoryService.Object);

        DownloadClientItem result = Assert.Single(recovery.GetCompletedItems(root, new DownloadClientItemClientInfo()));

        Assert.Equal(grabbedFolder, result.DownloadId);
        Assert.Equal("Artist - Album [FLAC 16bit] [WEB]", result.Title);
        Assert.Equal(DownloadItemStatus.Completed, result.Status);
        Assert.Equal(4096, result.TotalSize);
        Assert.Equal(grabbedFolder, result.OutputPath.FullPath);
    }
}
