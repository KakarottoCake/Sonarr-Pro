using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.ImportLists.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.ImportLists.AniList.List
{
    public class AniListParser : IParseImportListResponse
    {
        private readonly AniListSettings _settings;

        public AniListParser(AniListSettings settings)
        {
            _settings = settings;
        }

        public virtual IList<ImportListItemInfo> ParseResponse(ImportListResponse importListResponse)
        {
            return ParseResponse(importListResponse, out _);
        }

        public IList<ImportListItemInfo> ParseResponse(ImportListResponse importListResponse, out PageInfo pageInfo)
        {
            var result = new List<ImportListItemInfo>();

            if (!PreProcess(importListResponse))
            {
                pageInfo = null;
                return result;
            }

            var jsonResponse = STJson.Deserialize<MediaPageResponse>(importListResponse.Content);

            if (jsonResponse?.Data?.Page?.MediaList == null)
            {
                pageInfo = null;
                return result;
            }

            // Anilist currently does not support filtering this at the query level, they will get filtered out here.
            var filtered = jsonResponse.Data.Page.MediaList
                .Where(x => ValidateMediaStatus(x.Media))
                .Where(IsOnSelectedCustomList);

            foreach (var item in filtered)
            {
                var media = item.Media;

                var entry = new ImportListItemInfo
                {
                    AniListId = media.Id,
                    Title = media.Title.UserPreferred ?? media.Title.UserRomaji
                };

                result.Add(entry);
            }

            pageInfo = jsonResponse.Data.Page.PageInfo;
            return result;
        }

        /// <summary>
        /// Restricts the list to one of the user's own custom lists, if they named one.
        /// The standard lists such as Planning and Watching cover everything the user has
        /// ever added, which is usually far more than they want Sonarr to manage.
        /// </summary>
        private bool IsOnSelectedCustomList(MediaList item)
        {
            if (_settings.CustomList.IsNullOrWhiteSpace())
            {
                return true;
            }

            if (item.CustomLists == null)
            {
                return false;
            }

            // AniList reports every list the user has defined on every entry, so the flag
            // rather than the key's presence decides membership. Names are matched
            // case-insensitively because the list name is typed by hand into settings.
            return item.CustomLists
                       .Any(l => l.Value && l.Key.Equals(_settings.CustomList.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private bool ValidateMediaStatus(MediaInfo media)
        {
            if (media.Status == MediaStatus.Finished && _settings.ImportFinished)
            {
                return true;
            }

            if (media.Status == MediaStatus.Releasing && _settings.ImportReleasing)
            {
                return true;
            }

            if (media.Status == MediaStatus.Unreleased && _settings.ImportUnreleased)
            {
                return true;
            }

            if (media.Status == MediaStatus.Cancelled && _settings.ImportCancelled)
            {
                return true;
            }

            if (media.Status == MediaStatus.Hiatus && _settings.ImportHiatus)
            {
                return true;
            }

            return false;
        }

        protected virtual bool PreProcess(ImportListResponse netImportResponse)
        {
            if (netImportResponse.HttpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new ImportListException(netImportResponse, "Anilist API call resulted in an unexpected StatusCode [{0}]", netImportResponse.HttpResponse.StatusCode);
            }

            if (netImportResponse.HttpResponse.Headers.ContentType != null && !netImportResponse.HttpResponse.Headers.ContentType.Contains("text/json") &&
                netImportResponse.HttpRequest.Headers.Accept != null && netImportResponse.HttpRequest.Headers.Accept.Contains("text/json"))
            {
                throw new ImportListException(netImportResponse, "Anilist API responded with html content. Site is likely blocked or unavailable.");
            }

            return true;
        }
    }
}
