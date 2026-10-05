using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.QBittorrentTests
{
    [TestFixture]
    public class QBittorrentReuseFixture : DownloadClientFixtureBase<QBittorrent>
    {
        private const string Hash = "CBC2F069FE8BB2F544EAE707D75BCD3DE9DCF951";
        private const string ReuseTag = "sonarr-pro-reuse-tv-sonarr-pro";

        [SetUp]
        public void Setup()
        {
            Subject.Definition = new DownloadClientDefinition
            {
                RemoveCompletedDownloads = true,
                Settings = new QBittorrentSettings
                {
                    Host = "localhost",
                    TvCategory = "tv-sonarr-pro",
                    TvImportedCategory = "imported",
                    InitialState = (int)QBittorrentState.ForceStart,
                    RecentTvPriority = (int)QBittorrentPriority.First,
                    OlderTvPriority = (int)QBittorrentPriority.First
                }
            };

            Mocker.GetMock<IQBittorrentProxySelector>()
                .Setup(p => p.GetProxy(It.IsAny<QBittorrentSettings>(), It.IsAny<bool>()))
                .Returns(Mocker.GetMock<IQBittorrentProxy>().Object);
            Mocker.GetMock<IQBittorrentProxySelector>()
                .Setup(p => p.GetApiVersion(It.IsAny<QBittorrentSettings>()))
                .Returns(new Version(2, 8, 1));
            Mocker.GetMock<IQBittorrentProxy>()
                .Setup(p => p.GetApiVersion(It.IsAny<QBittorrentSettings>()))
                .Returns(new Version(2, 8, 1));
            Mocker.GetMock<IQBittorrentProxy>()
                .Setup(p => p.GetConfig(It.IsAny<QBittorrentSettings>()))
                .Returns(new QBittorrentPreferences { DhtEnabled = false, MaxRatio = 1, MaxRatioEnabled = true });
            Mocker.GetMock<IQBittorrentProxy>()
                .Setup(p => p.IsTorrentLoaded(Hash.ToLowerInvariant(), It.IsAny<QBittorrentSettings>()))
                .Returns(true);
        }

        [Test]
        public async Task should_reuse_known_hash_without_fetching_torrent_or_changing_download_settings()
        {
            var episode = CreateRemoteEpisode();
            episode.Release = new TorrentInfo { Title = _title, InfoHash = Hash.ToLowerInvariant(), DownloadUrl = _downloadUrl };
            episode.SeedConfiguration = new TorrentSeedConfiguration { Ratio = 2, SeedTime = TimeSpan.FromMinutes(100) };

            var id = await Subject.Download(episode, CreateIndexer());

            id.Should().Be(Hash);
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.AddTags(Hash.ToLowerInvariant(), It.Is<IEnumerable<string>>(tags => tags.Contains(ReuseTag)), It.IsAny<QBittorrentSettings>()), Times.Once());
            VerifyNoTorrentAdd();
            Mocker.GetMock<IHttpClient>().Verify(p => p.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()), Times.Never());
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.SetTorrentSeedingConfiguration(It.IsAny<string>(), It.IsAny<TorrentSeedConfiguration>(), It.IsAny<QBittorrentSettings>()), Times.Never());
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.MoveTorrentToTopInQueue(It.IsAny<string>(), It.IsAny<QBittorrentSettings>()), Times.Never());
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.SetForceStart(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<QBittorrentSettings>()), Times.Never());
        }

        [Test]
        public async Task should_reuse_magnet_without_dht_or_trackers()
        {
            var episode = CreateRemoteEpisode();
            episode.Release.DownloadUrl = "magnet:?xt=urn:btih:" + Hash;

            (await Subject.Download(episode, CreateIndexer())).Should().Be(Hash);
            VerifyNoTorrentAdd();
        }

        [Test]
        public async Task should_reuse_torrent_file_after_reading_its_hash()
        {
            Mocker.GetMock<ITorrentFileInfoReader>()
                .Setup(p => p.GetHashFromTorrentFile(It.IsAny<byte[]>())).Returns(Hash);

            (await Subject.Download(CreateRemoteEpisode(), CreateIndexer())).Should().Be(Hash);
            VerifyNoTorrentAdd();
        }

        [TestCase(2, 8, false)]
        [TestCase(2, 2, true)]
        public async Task should_keep_normal_add_behavior_for_new_torrents_and_clients_without_tags(int major, int minor, bool loaded)
        {
            Mocker.GetMock<IQBittorrentProxySelector>()
                .Setup(p => p.GetApiVersion(It.IsAny<QBittorrentSettings>())).Returns(new Version(major, minor));
            Mocker.GetMock<IQBittorrentProxy>()
                .Setup(p => p.IsTorrentLoaded(It.IsAny<string>(), It.IsAny<QBittorrentSettings>())).Returns(loaded);
            Mocker.GetMock<IQBittorrentProxy>()
                .Setup(p => p.GetConfig(It.IsAny<QBittorrentSettings>())).Returns(new QBittorrentPreferences { DhtEnabled = true });
            var settings = (QBittorrentSettings)Subject.Definition.Settings;
            settings.InitialState = (int)QBittorrentState.Start;
            settings.RecentTvPriority = (int)QBittorrentPriority.Last;
            settings.OlderTvPriority = (int)QBittorrentPriority.Last;
            var episode = CreateRemoteEpisode();
            episode.Release.DownloadUrl = "magnet:?xt=urn:btih:" + Hash;

            (await Subject.Download(episode, CreateIndexer())).Should().Be(Hash);

            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.AddTorrentFromUrl(episode.Release.DownloadUrl, null, It.IsAny<QBittorrentSettings>()), Times.Once());
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.AddTags(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<QBittorrentSettings>()), Times.Never());
        }

        [Test]
        public void should_not_report_success_when_tagging_fails()
        {
            var episode = CreateRemoteEpisode();
            episode.Release = new TorrentInfo { Title = _title, InfoHash = Hash, DownloadUrl = _downloadUrl };
            Mocker.GetMock<IQBittorrentProxy>()
                .Setup(p => p.AddTags(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<QBittorrentSettings>()))
                .Throws(new DownloadClientException("Tagging failed"));

            Assert.ThrowsAsync<DownloadClientException>(async () => await Subject.Download(episode, CreateIndexer()));
            VerifyNoTorrentAdd();
        }

        [TestCase("uploading", DownloadItemStatus.Completed)]
        [TestCase("stoppedUP", DownloadItemStatus.Completed)]
        [TestCase("downloading", DownloadItemStatus.Downloading)]
        public void reused_torrents_should_use_normal_import_paths_and_preserve_source_files(string state, DownloadItemStatus expectedStatus)
        {
            GivenReusedTorrent(state);

            var item = Subject.GetItems().Single();

            item.Status.Should().Be(expectedStatus);
            item.Category.Should().Be("upstream-tv");
            item.CanMoveFiles.Should().BeFalse();
            item.CanBeRemoved.Should().BeFalse();
            if (expectedStatus == DownloadItemStatus.Completed)
            {
                item.OutputPath.FullPath.Should().Be("/downloads/" + _title);
            }

            Subject.MarkItemAsImported(item);
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.SetTorrentLabel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<QBittorrentSettings>()), Times.Never());
        }

        [Test]
        public void reused_torrents_should_not_be_deleted_even_if_failed_handling_enables_removal()
        {
            GivenReusedTorrent("stoppedUP");
            var item = Subject.GetItems().Single();
            item.CanBeRemoved = true;

            Assert.Throws<NotSupportedException>(() => Subject.RemoveItem(item, true));
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.RemoveTorrent(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<QBittorrentSettings>()), Times.Never());
        }

        private void GivenReusedTorrent(string state)
        {
            Mocker.GetMock<IQBittorrentProxy>().Setup(p => p.GetTorrents(It.IsAny<QBittorrentSettings>()))
                .Returns(new List<QBittorrentTorrent>
                {
                    new QBittorrentTorrent
                    {
                        Hash = Hash,
                        Name = _title,
                        Tags = "other-tag, " + ReuseTag,
                        Category = "upstream-tv",
                        Size = 1000,
                        Progress = state == "downloading" ? 0.5 : 1,
                        State = state,
                        Ratio = 2,
                        SavePath = "/downloads/",
                        ContentPath = "/downloads/" + _title
                    }
                });
        }

        private void VerifyNoTorrentAdd()
        {
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.AddTorrentFromUrl(It.IsAny<string>(), It.IsAny<TorrentSeedConfiguration>(), It.IsAny<QBittorrentSettings>()), Times.Never());
            Mocker.GetMock<IQBittorrentProxy>().Verify(p => p.AddTorrentFromFile(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<TorrentSeedConfiguration>(), It.IsAny<QBittorrentSettings>()), Times.Never());
        }
    }
}
