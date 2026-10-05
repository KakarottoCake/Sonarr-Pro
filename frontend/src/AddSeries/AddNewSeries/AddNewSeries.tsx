import React, { useCallback, useEffect, useRef, useState } from 'react';
import SelectInput from 'Components/Form/SelectInput';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useDebounce from 'Helpers/Hooks/useDebounce';
import useQueryParams from 'Helpers/Hooks/useQueryParams';
import { icons, kinds } from 'Helpers/Props';
import { useHasSeries } from 'Series/useSeries';
import { useMetadataSourceSettings } from 'Settings/MetadataSource/useMetadataSourceSettings';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddNewSeriesSearchResult from './AddNewSeriesSearchResult';
import {
  setMetadataSourceOption,
  useMetadataSourceOption,
} from './metadataSourceOptionsStore';
import { MetadataSource, useLookupSeries } from './useAddSeries';
import styles from './AddNewSeries.module.css';

const METADATA_SOURCES: { key: MetadataSource; value: string }[] = [
  { key: 'tvdb', value: 'TheTVDB' },
  { key: 'tmdb', value: 'TMDB' },
  { key: 'aniList', value: 'AniList' },

  // Offered alongside AniList rather than instead of it: AniList knows more
  // alternative titles, but only MyAnimeList publishes per-episode titles.
  { key: 'myAnimeList', value: 'MyAnimeList' },
];

const SOURCE_DESCRIPTIONS: Record<MetadataSource, string> = {
  tvdb: 'MetadataSourceTvdbDescription',
  tmdb: 'MetadataSourceTmdbDescription',
  aniList: 'MetadataSourceAniListDescription',
  myAnimeList: 'MetadataSourceMyAnimeListDescription',
};

function AddNewSeries() {
  const { term: initialTerm = '' } = useQueryParams<{ term: string }>();
  const hasSeries = useHasSeries();
  const [term, setTerm] = useState(initialTerm);
  const savedSource = useMetadataSourceOption();
  const metadataSource = METADATA_SOURCES.some(({ key }) => key === savedSource)
    ? savedSource
    : 'tvdb';
  const { data: metadataSettings } = useMetadataSourceSettings();
  const searchInputRef = useRef<HTMLInputElement>(null);
  const searchTerm = term.trim();
  const query = useDebounce(searchTerm, searchTerm ? 300 : 0);

  const handleMetadataSourceChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setMetadataSourceOption(value as MetadataSource);
      searchInputRef.current?.focus();
    },
    []
  );

  const handleSearchInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTerm(value);
    },
    []
  );

  const handleClearSeriesLookupPress = useCallback(() => {
    setTerm('');
    searchInputRef.current?.focus();
  }, []);

  const {
    isFetching: isFetchingApi,
    error,
    data,
  } = useLookupSeries(query, { metadataSource });

  const isFetching = !!searchTerm && (query !== searchTerm || isFetchingApi);
  const needsTmdbKey =
    metadataSource === 'tmdb' &&
    metadataSettings != null &&
    !metadataSettings.tmdbApiKeyConfigured;

  useEffect(() => {
    setTerm(initialTerm);
  }, [initialTerm]);

  return (
    <PageContent title={translate('AddNewSeries')}>
      <PageContentBody>
        <PageHeading
          scope={translate('Media')}
          title={translate('AddNewSeries')}
        />

        <div className={styles.searchSticky}>
          <div className={styles.searchWrap}>
            <Icon className={styles.searchIcon} name={icons.SEARCH} size={18} />

            <TextInput
              ref={searchInputRef}
              className={styles.searchInput}
              name="seriesLookup"
              value={term}
              placeholder={
                metadataSource === 'tvdb'
                  ? 'eg. Breaking Bad, tvdb:####'
                  : 'eg. One Piece'
              }
              autoFocus={true}
              onChange={handleSearchInputChange}
            />

            {term ? (
              <Button
                className={styles.clearLookupButton}
                title={translate('Clear')}
                onPress={handleClearSeriesLookupPress}
              >
                <Icon name={icons.REMOVE} size={14} />
              </Button>
            ) : null}
          </div>
          <div className={styles.metadataSourceContainer}>
            <label className={styles.metadataSourceLabel}>
              {translate('MetadataSource')}
              <SelectInput
                className={styles.metadataSourceSelect}
                name="metadataSource"
                value={metadataSource}
                values={METADATA_SOURCES}
                onChange={handleMetadataSourceChange}
              />
            </label>
            <span className={styles.sourceHelp}>
              {translate(SOURCE_DESCRIPTIONS[metadataSource])}
            </span>
          </div>
          {needsTmdbKey ? (
            <Link
              className={styles.sourceWarning}
              to="/settings/metadatasource"
            >
              {translate('MetadataSourceNeedsApiKey')} · {translate('Settings')}
            </Link>
          ) : null}
        </div>

        {isFetching ? <LoadingIndicator /> : null}

        {searchTerm && !isFetching && !!error ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('AddNewSeriesError')}
            </div>
            <p className={styles.error}>{getErrorMessage(error)}</p>
          </div>
        ) : null}

        {searchTerm && !isFetching && !error && !!data.length ? (
          <div className={styles.searchResults}>
            <div className={styles.resultsLabel}>
              {data.length === 1
                ? translate('CountResult', { count: data.length })
                : translate('CountResults', { count: data.length })}
            </div>

            {data.map((item) => (
              <AddNewSeriesSearchResult key={item.tvdbId} series={item} />
            ))}
          </div>
        ) : null}

        {!isFetching && !error && !data.length && searchTerm ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('CouldNotFindResults', { term })}
            </div>
            <div className={styles.emptyBody}>
              {metadataSource === 'tvdb'
                ? translate('SearchByTvdbId')
                : translate(SOURCE_DESCRIPTIONS[metadataSource])}
            </div>
            <div>
              <Link
                className={styles.emptyLink}
                to="https://wiki.servarr.com/sonarr/faq#why-cant-i-add-a-new-series-when-i-know-the-tvdb-id"
              >
                {translate('WhyCantIFindMyShow')}
              </Link>
            </div>
          </div>
        ) : null}

        {searchTerm ? null : (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('AddNewSeriesHelpText')}
            </div>
            <div className={styles.emptyBody}>
              {metadataSource === 'tvdb'
                ? translate('SearchByTvdbId')
                : translate(SOURCE_DESCRIPTIONS[metadataSource])}
            </div>

            {hasSeries ? null : (
              <>
                <div className={styles.emptyDivider} />
                <div className={styles.emptyNoLibrary}>
                  {translate('NoSeriesHaveBeenAdded')}
                </div>
                <Button to="/add/import" kind={kinds.PRIMARY}>
                  {translate('ImportExistingSeries')}
                </Button>
              </>
            )}
          </div>
        )}
      </PageContentBody>
    </PageContent>
  );
}

export default AddNewSeries;
