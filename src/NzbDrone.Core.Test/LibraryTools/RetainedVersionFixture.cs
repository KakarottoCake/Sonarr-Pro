using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class RetainedVersionFixture : CoreTest<RetainedVersionService>
    {
        private Series _series;
        private EpisodeFile _active;
        private string _activePath;
        private Dictionary<string, long> _diskFiles;
        private List<RetainedVersion> _records;

        [SetUp]
        public void Setup()
        {
            _series = new Series { Id = 7, Path = Path.Combine(Path.GetTempPath(), "sonarr-version-fixture-" + Guid.NewGuid().ToString("N")) };
            _active = new EpisodeFile { Id = 10, SeriesId = 7, RelativePath = "episode.mkv", Size = 200 };
            _activePath = Path.Combine(_series.Path, _active.RelativePath);
            var selectedFile = new EpisodeFile { SeriesId = 7, RelativePath = "episode.mkv", Size = 100 };
            var selected = new RetainedVersion { Id = 1, SeriesId = 7, RelativePath = Path.Combine("Plex Versions", "old", "episode.mkv"), MetadataJson = selectedFile.ToJson(), EpisodeIdsJson = "[31]" };
            var episodes = new List<Episode> { new Episode { Id = 31, SeriesId = 7, EpisodeFileId = 10 } };
            _diskFiles = new Dictionary<string, long> { { _activePath, 200 }, { Path.Combine(_series.Path, selected.RelativePath), 100 } };
            _records = new List<RetainedVersion>();
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(7)).Returns(_series);
            Mocker.GetMock<IMediaFileService>().Setup(s => s.Get(10)).Returns(_active);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(episodes);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodesByFileId(10)).Returns(episodes);
            Mocker.GetMock<IRetainedVersionRepository>().Setup(s => s.Get(1)).Returns(selected);
            Mocker.GetMock<IRetainedVersionRepository>().Setup(s => s.Insert(It.IsAny<RetainedVersion>())).Returns((RetainedVersion record) =>
            {
                record.Id = _records.Count + 2;
                _records.Add(record);
                return record;
            });
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(It.IsAny<string>())).Returns((string path) => _diskFiles.ContainsKey(path));
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFileSize(It.IsAny<string>())).Returns((string path) => _diskFiles[path]);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileGetLastWrite(It.IsAny<string>())).Returns(new DateTime(2020, 1, 1));
            Mocker.GetMock<IDiskProvider>().Setup(d => d.DeleteFile(It.IsAny<string>())).Callback((string path) => _diskFiles.Remove(path));
            Mocker.GetMock<IDiskTransferService>().Setup(d => d.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>())).Returns((string source, string target, TransferMode mode, bool overwrite) =>
            {
                if (_diskFiles.ContainsKey(target))
                {
                    throw new IOException("Destination occupied");
                }

                _diskFiles.Add(target, _diskFiles[source]);
                if (mode == TransferMode.Move)
                {
                    _diskFiles.Remove(source);
                }

                return mode;
            });
        }

        [Test]
        public void activating_a_version_keeps_both_the_original_archive_and_previous_active_file()
        {
            Subject.Activate(7, 1);
            _diskFiles[_activePath].Should().Be(100);
            _records.Should().ContainSingle();
            _diskFiles[Path.Combine(_series.Path, _records.Single().RelativePath)].Should().Be(200);
            _diskFiles.Values.Should().BeEquivalentTo(new long[] { 100, 100, 200 });
            Mocker.GetMock<IMediaFileService>().Verify(s => s.Update(It.Is<EpisodeFile>(f => f.Id == 10 && f.Size == 100)), Times.Once());
        }

        [Test]
        public void database_update_failure_restores_the_original_active_file()
        {
            Mocker.GetMock<IMediaFileService>().Setup(s => s.Update(It.IsAny<EpisodeFile>())).Throws(new IOException("database unavailable"));
            Assert.Throws<IOException>(() => Subject.Activate(7, 1));
            _diskFiles[_activePath].Should().Be(200);
            _diskFiles[Path.Combine(_series.Path, _records.Single().RelativePath)].Should().Be(200);
        }

        [Test]
        public void a_pending_move_prevents_any_file_changes()
        {
            _series.PendingPath = Path.Combine(Path.GetTempPath(), "destination");
            Assert.Throws<IOException>(() => Subject.Activate(7, 1));
            _diskFiles.Values.Should().BeEquivalentTo(new long[] { 200, 100 });
            _records.Should().BeEmpty();
        }
    }
}
