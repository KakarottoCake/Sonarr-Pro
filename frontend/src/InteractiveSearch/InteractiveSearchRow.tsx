import React, {
  useCallback,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import IndexerFlags from 'Episode/IndexerFlags';
import { icons, kinds } from 'Helpers/Props';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatAge from 'Utilities/Number/formatAge';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import OverrideMatchModal from './OverrideMatch/OverrideMatchModal';
import Peers from './Peers';
import ReleaseSceneIndicator from './ReleaseSceneIndicator';
import { Release, useGrabRelease } from './useReleases';
import styles from './InteractiveSearchRow.module.css';

function getDownloadKind(isGrabbed: boolean, grabError?: string) {
  if (isGrabbed) {
    return kinds.SUCCESS;
  }

  if (grabError) {
    return kinds.DANGER;
  }

  return kinds.PRIMARY;
}

function getDownloadTooltip(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return '';
  } else if (isGrabbed) {
    return translate('AddedToDownloadQueue');
  } else if (grabError) {
    return grabError;
  }

  return translate('AddToDownloadQueue');
}

interface InteractiveSearchRowProps extends Release {
  index: number;
  style: React.CSSProperties;
  setRowHeight: (index: number, height: number) => void;
  searchPayload: InteractiveSearchPayload;
}

function InteractiveSearchRow(props: InteractiveSearchRowProps) {
  const { index, style, setRowHeight } = props;
  const {
    decision,
    history,
    parsedInfo,
    release,
    languages,
    customFormatScore,
    customFormats,
    sceneMapping,
    mappedSeriesId,
    mappedSeasonNumber,
    mappedEpisodeNumbers,
    mappedAbsoluteEpisodeNumbers,
    mappedEpisodeInfo,
    episodeRequested,
    downloadAllowed,
    searchPayload,
  } = props;

  const { rejections = [] } = decision;

  const {
    absoluteEpisodeNumbers,
    episodeNumbers,
    isDaily,
    seasonNumber,
    quality,
  } = parsedInfo;

  const {
    guid,
    indexerId,
    age,
    ageHours,
    ageMinutes,
    publishDate,
    title,
    infoUrl,
    indexer,
    size,
    seeders,
    leechers,
    protocol,
    indexerFlags = 0,
  } = release;

  const { longDateFormat, timeFormat } = useUiSettingsValues();

  const [isConfirmGrabModalOpen, setIsConfirmGrabModalOpen] = useState(false);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState(false);
  const { isGrabbing, isGrabbed, grabError, grabRelease } = useGrabRelease();

  const rowRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const element = rowRef.current;

    if (!element) {
      return;
    }

    const measure = () => setRowHeight(index, element.offsetHeight);

    measure();

    const observer = new ResizeObserver(measure);
    observer.observe(element);

    return () => observer.disconnect();
  }, [index, setRowHeight]);

  const isBlocklisted = useMemo(() => {
    return (
      decision.rejections.findIndex((r) => r.reason === 'blocklisted') >= 0
    );
  }, [decision]);

  const handleGrabPress = useCallback(() => {
    if (downloadAllowed) {
      grabRelease({
        guid,
        indexerId,
      });

      return;
    }

    setIsConfirmGrabModalOpen(true);
  }, [
    guid,
    indexerId,
    downloadAllowed,
    grabRelease,
    setIsConfirmGrabModalOpen,
  ]);

  const onGrabConfirm = useCallback(() => {
    setIsConfirmGrabModalOpen(false);

    grabRelease({
      guid,
      indexerId,
      searchInfo: searchPayload,
    });
  }, [guid, indexerId, searchPayload, grabRelease, setIsConfirmGrabModalOpen]);

  const onGrabCancel = useCallback(() => {
    setIsConfirmGrabModalOpen(false);
  }, [setIsConfirmGrabModalOpen]);

  const onOverridePress = useCallback(() => {
    setIsOverrideModalOpen(true);
  }, [setIsOverrideModalOpen]);

  const onOverrideModalClose = useCallback(() => {
    setIsOverrideModalOpen(false);
  }, [setIsOverrideModalOpen]);

  const { height: _height, ...positionStyle } = style;
  let statusKey = 'InteractiveSearchReady';

  if (rejections.length) {
    statusKey = 'InteractiveSearchRejected';
  } else if (!downloadAllowed) {
    statusKey = 'InteractiveSearchNeedsMatch';
  }

  return (
    <div ref={rowRef} className={styles.row} style={positionStyle}>
      <article className={styles.card} aria-label={title}>
        <div className={styles.content}>
          <div className={styles.heading}>
            <Link className={styles.releaseTitle} to={infoUrl}>
              {title}
            </Link>
            <ReleaseSceneIndicator
              className={styles.sceneMapping}
              seasonNumber={mappedSeasonNumber}
              episodeNumbers={mappedEpisodeNumbers}
              absoluteEpisodeNumbers={mappedAbsoluteEpisodeNumbers}
              sceneSeasonNumber={seasonNumber}
              sceneEpisodeNumbers={episodeNumbers}
              sceneAbsoluteEpisodeNumbers={absoluteEpisodeNumbers}
              sceneMapping={sceneMapping}
              episodeRequested={episodeRequested}
              isDaily={isDaily}
            />
          </div>

          <div className={styles.badges}>
            <ProtocolLabel protocol={protocol} />
            <EpisodeQuality quality={quality} showRevision={true} />
            <EpisodeLanguages languages={languages} />
            <span
              className={styles.score}
              title={translate('CustomFormatScore')}
            >
              <Icon name={icons.SCORE} size={14} />
              {translate('InteractiveSearchScore')}{' '}
              {formatCustomFormatScore(customFormatScore, customFormats.length)}
            </span>
          </div>

          <dl className={styles.metadata}>
            <div>
              <dt>{translate('Indexer')}</dt>
              <dd>{indexer}</dd>
            </div>
            <div>
              <dt>{translate('Size')}</dt>
              <dd>{formatBytes(size)}</dd>
            </div>
            <div>
              <dt>{translate('Age')}</dt>
              <dd
                title={formatDateTime(publishDate, longDateFormat, timeFormat, {
                  includeSeconds: true,
                })}
              >
                {formatAge(age, ageHours, ageMinutes)}
              </dd>
            </div>
            {protocol === 'torrent' ? (
              <div>
                <dt>{translate('Peers')}</dt>
                <dd>
                  <Peers seeders={seeders} leechers={leechers} />
                </dd>
              </div>
            ) : null}
          </dl>

          {history || isBlocklisted ? (
            <div className={styles.history}>
              {history ? (
                <span>
                  <Icon
                    name={icons.DOWNLOADING}
                    kind={history.failed ? kinds.DANGER : kinds.DEFAULT}
                  />
                  {history.failed
                    ? translate('FailedAt', {
                        date: formatDateTime(
                          history.failed,
                          longDateFormat,
                          timeFormat,
                          { includeSeconds: true }
                        ),
                      })
                    : translate('GrabbedAt', {
                        date: formatDateTime(
                          history.grabbed,
                          longDateFormat,
                          timeFormat,
                          { includeSeconds: true }
                        ),
                      })}
                </span>
              ) : null}
              {isBlocklisted ? (
                <span>
                  <Icon name={icons.BLOCKLIST} kind={kinds.DANGER} />
                  {translate('Blocklisted')}
                </span>
              ) : null}
            </div>
          ) : null}

          {rejections.length ? (
            <div className={styles.rejectionPreview}>
              {rejections[0].message}
            </div>
          ) : null}

          {rejections.length || customFormats.length || indexerFlags ? (
            <details className={styles.details}>
              <summary>{translate('Details')}</summary>
              {rejections.length ? (
                <div className={styles.detailSection}>
                  <strong>
                    {translate('Rejections')} ({rejections.length})
                  </strong>
                  <ul className={styles.rejectionList}>
                    {rejections.map((rejection, rejectionIndex) => (
                      <li key={rejectionIndex}>{rejection.message}</li>
                    ))}
                  </ul>
                </div>
              ) : null}
              {customFormats.length ? (
                <div className={styles.detailSection}>
                  <strong>{translate('CustomFormats')}</strong>
                  <div className={styles.formats}>
                    <EpisodeFormats formats={customFormats} />
                  </div>
                </div>
              ) : null}
              {indexerFlags ? (
                <div className={styles.detailSection}>
                  <strong>{translate('IndexerFlags')}</strong>
                  <IndexerFlags indexerFlags={indexerFlags} />
                </div>
              ) : null}
            </details>
          ) : null}

          {grabError ? (
            <div className={styles.grabError} role="alert">
              {grabError}
            </div>
          ) : null}
        </div>

        <div className={styles.actions}>
          <span
            className={
              rejections.length || !downloadAllowed
                ? styles.rejected
                : styles.approved
            }
          >
            <Icon
              name={
                rejections.length || !downloadAllowed
                  ? icons.DANGER
                  : icons.CHECK_CIRCLE
              }
              size={14}
            />
            {translate(statusKey)}
          </span>
          <div className={styles.buttons}>
            <SpinnerButton
              className={styles.actionButton}
              kind={getDownloadKind(isGrabbed, grabError)}
              title={getDownloadTooltip(isGrabbing, isGrabbed, grabError)}
              aria-label={`${translate('Download')}: ${title}`}
              isSpinning={isGrabbing}
              isDisabled={isGrabbed}
              onPress={handleGrabPress}
            >
              <Icon name={isGrabbed ? icons.CHECK : icons.DOWNLOAD} size={16} />
              {translate(isGrabbed ? 'Grabbed' : 'Download')}
            </SpinnerButton>
            <Button
              className={styles.actionButton}
              title={translate('OverrideAndAddToDownloadQueue')}
              isDisabled={isGrabbing || isGrabbed}
              onPress={onOverridePress}
            >
              <Icon name={icons.INTERACTIVE} size={16} />
              {translate('InteractiveSearchChooseEpisodes')}
            </Button>
          </div>
        </div>
      </article>

      <ConfirmModal
        isOpen={isConfirmGrabModalOpen}
        kind={kinds.WARNING}
        title={translate('GrabRelease')}
        message={translate('GrabReleaseUnknownSeriesOrEpisodeMessageText', {
          title,
        })}
        confirmLabel={translate('Grab')}
        onConfirm={onGrabConfirm}
        onCancel={onGrabCancel}
      />

      <OverrideMatchModal
        isOpen={isOverrideModalOpen}
        title={title}
        indexerId={indexerId}
        guid={guid}
        seriesId={mappedSeriesId}
        seasonNumber={mappedSeasonNumber}
        episodes={mappedEpisodeInfo}
        languages={languages}
        quality={quality}
        protocol={protocol}
        isGrabbing={isGrabbing}
        grabError={grabError}
        grabRelease={grabRelease}
        onModalClose={onOverrideModalClose}
      />
    </div>
  );
}

export default InteractiveSearchRow;
