import React from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { useMetadataSourceSettings } from './useMetadataSourceSettings';
import styles from './Sources.module.css';

interface SourceProps {
  name: string;
  url: string;
  description: string;
  status?: string;
}

function Source({ name, url, description, status }: SourceProps) {
  return (
    <div className={styles.source}>
      <div className={styles.header}>
        <a className={styles.name} href={url} target="_blank" rel="noreferrer">
          {name}
        </a>

        {status ? <span className={styles.status}>{status}</span> : null}
      </div>

      <div className={styles.description}>{description}</div>
    </div>
  );
}

/**
 * What each metadata source is good for, and whether it is usable right now.
 * A source is chosen per series when adding it, from the dropdown above the
 * search box, so this page is about what the choices mean rather than a setting.
 */
function Sources() {
  const { data } = useMetadataSourceSettings();

  // The key itself comes back masked, so a separate flag says whether one is stored.
  const hasTmdbKey = !!data?.tmdbApiKeyConfigured;

  return (
    <FieldSet legend={translate('MetadataSources')}>
      <Alert kind={kinds.INFO}>{translate('MetadataSourcesInfo')}</Alert>

      <Source
        name="TheTVDB"
        url="https://www.thetvdb.com"
        description={translate('MetadataSourceTvdbDescription')}
        status={translate('MetadataSourceReady')}
      />

      <Source
        name="TMDB"
        url="https://www.themoviedb.org"
        description={translate('MetadataSourceTmdbDescription')}
        status={
          hasTmdbKey
            ? translate('MetadataSourceReady')
            : translate('MetadataSourceNeedsApiKey')
        }
      />

      <Source
        name="AniList"
        url="https://anilist.co"
        description={translate('MetadataSourceAniListDescription')}
        status={translate('MetadataSourceReady')}
      />

      <Source
        name="MyAnimeList"
        url="https://myanimelist.net"
        description={translate('MetadataSourceMyAnimeListDescription')}
        status={translate('MetadataSourceReady')}
      />

      <div className={styles.attribution}>
        <InlineMarkdown
          data={translate('SeriesAndEpisodeInformationIsProvidedByTheTVDB', {
            url: 'https://www.thetvdb.com/subscribe',
          })}
        />
      </div>
    </FieldSet>
  );
}

export default Sources;
