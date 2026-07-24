using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.TorrentInfo
{
    public class TorrentContents
    {
        public TorrentContents()
        {
            Files = new List<TorrentContentFile>();
        }

        public string Name { get; set; }
        public List<TorrentContentFile> Files { get; set; }

        public long TotalSize
        {
            get
            {
                long total = 0;

                foreach (var file in Files)
                {
                    total += file.Length;
                }

                return total;
            }
        }
    }

    public class TorrentContentFile
    {
        public TorrentContentFile(string path, long length)
        {
            Path = path;
            Length = length;
        }

        public string Path { get; set; }
        public long Length { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1} bytes)", Path, Length);
        }
    }
}
