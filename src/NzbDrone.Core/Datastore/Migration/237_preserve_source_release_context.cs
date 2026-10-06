using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(237)]
    public class preserve_source_release_context : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Retain the original pack size: a 30 GB pack must not become a
            // preferred 1 GB release after it is imported or compressed.
            Alter.Table("EpisodeFiles")
                 .AddColumn("SourceReleaseSize").AsInt64().Nullable()
                 .AddColumn("SourceReleaseTitle").AsString(int.MaxValue).Nullable();

            IfDatabase(ProcessorIdConstants.SQLite).Execute.Sql("""
                WITH candidates AS (
                    SELECT CAST(COALESCE(json_extract(i."Data", '$.fileId'), json_extract(i."Data", '$.FileId')) AS BIGINT) AS file_id,
                           i."SeriesId" AS series_id, CAST(COALESCE(json_extract(g."Data", '$.size'), json_extract(g."Data", '$.Size')) AS BIGINT) AS release_size,
                           g."SourceTitle" AS release_title,
                           ROW_NUMBER() OVER (PARTITION BY i."SeriesId", CAST(COALESCE(json_extract(i."Data", '$.fileId'), json_extract(i."Data", '$.FileId')) AS BIGINT)
                                              ORDER BY i."Date" DESC, g."Date" DESC, i."Id" DESC, g."Id" DESC) AS preference
                    FROM "History" i JOIN "History" g
                      ON g."DownloadId" = i."DownloadId" AND g."SeriesId" = i."SeriesId" AND g."EpisodeId" = i."EpisodeId"
                    WHERE i."EventType" = 3 AND g."EventType" = 1 AND i."DownloadId" <> ''
                      AND g."Date" <= i."Date" AND CAST(COALESCE(json_extract(g."Data", '$.size'), json_extract(g."Data", '$.Size')) AS BIGINT) > 0
                )
                UPDATE "EpisodeFiles" SET "SourceReleaseSize" = c.release_size, "SourceReleaseTitle" = c.release_title
                FROM candidates c
                WHERE c.preference = 1 AND c.file_id = "EpisodeFiles"."Id" AND c.series_id = "EpisodeFiles"."SeriesId";
                """);

            IfDatabase(ProcessorIdConstants.PostgreSQL).Execute.Sql("""
                WITH candidates AS (
                    SELECT CAST(CASE WHEN COALESCE(i."Data"::jsonb ->> 'fileId', i."Data"::jsonb ->> 'FileId') ~ '^[0-9]{1,18}$' THEN COALESCE(i."Data"::jsonb ->> 'fileId', i."Data"::jsonb ->> 'FileId') ELSE NULL END AS BIGINT) AS file_id,
                           i."SeriesId" AS series_id, CAST(CASE WHEN COALESCE(g."Data"::jsonb ->> 'size', g."Data"::jsonb ->> 'Size') ~ '^[0-9]{1,18}$' THEN COALESCE(g."Data"::jsonb ->> 'size', g."Data"::jsonb ->> 'Size') ELSE NULL END AS BIGINT) AS release_size,
                           g."SourceTitle" AS release_title,
                           ROW_NUMBER() OVER (PARTITION BY i."SeriesId", CAST(CASE WHEN COALESCE(i."Data"::jsonb ->> 'fileId', i."Data"::jsonb ->> 'FileId') ~ '^[0-9]{1,18}$' THEN COALESCE(i."Data"::jsonb ->> 'fileId', i."Data"::jsonb ->> 'FileId') ELSE NULL END AS BIGINT)
                                              ORDER BY i."Date" DESC, g."Date" DESC, i."Id" DESC, g."Id" DESC) AS preference
                    FROM "History" i JOIN "History" g
                      ON g."DownloadId" = i."DownloadId" AND g."SeriesId" = i."SeriesId" AND g."EpisodeId" = i."EpisodeId"
                    WHERE i."EventType" = 3 AND g."EventType" = 1 AND i."DownloadId" <> ''
                      AND g."Date" <= i."Date" AND CAST(CASE WHEN COALESCE(g."Data"::jsonb ->> 'size', g."Data"::jsonb ->> 'Size') ~ '^[0-9]{1,18}$' THEN COALESCE(g."Data"::jsonb ->> 'size', g."Data"::jsonb ->> 'Size') ELSE NULL END AS BIGINT) > 0
                )
                UPDATE "EpisodeFiles" SET "SourceReleaseSize" = c.release_size, "SourceReleaseTitle" = c.release_title
                FROM candidates c
                WHERE c.preference = 1 AND c.file_id = "EpisodeFiles"."Id" AND c.series_id = "EpisodeFiles"."SeriesId";
                """);
        }
    }
}
