import React, { useCallback, useEffect, useState } from 'react';
import Button from 'Components/Link/Button';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import useSeries from 'Series/useSeries';
import formatBytes from 'Utilities/Number/formatBytes';
import styles from './LibraryTools.module.css';

interface RecycleFile {
  token: string;
  name: string;
  size: number;
  recycledAt: string;
}
interface Recycle {
  configured: boolean;
  path: string;
  truncated: boolean;
  files: RecycleFile[];
}

function RestoreFile({
  file,
  seriesId,
  seasonNumber,
  onRestored,
}: {
  file: RecycleFile;
  seriesId: number;
  seasonNumber: number;
  onRestored: () => void;
}) {
  const [confirm, setConfirm] = useState(false);
  const [commandId, setCommandId] = useState(0);
  const command = useApiQuery<{ status: string; message: string }>({
    path: `/command/${commandId}`,
    queryOptions: {
      enabled: commandId > 0,
      refetchInterval: (query) =>
        ['completed', 'failed', 'cancelled', 'aborted', 'orphaned'].includes(
          query.state.data?.status ?? ''
        )
          ? false
          : 2000,
    },
  });
  useEffect(() => {
    if (command.data?.status === 'completed') {
      onRestored();
    }
  }, [command.data?.status, onRestored]);
  const restoring =
    commandId > 0 &&
    !['completed', 'failed', 'cancelled', 'aborted', 'orphaned'].includes(
      command.data?.status ?? ''
    );
  const restore = useApiMutation<
    {
      commandId: number;
      message: string;
    },
    {
      token: string;
      seriesId: number;
      seasonNumber: number;
    }
  >({
    path: '/librarytools/recycle/restore',
    method: 'POST',
    mutationOptions: {
      onSuccess: (result) => {
        setCommandId(result.commandId);
        setConfirm(false);
      },
    },
  });
  const handlePress1 = useCallback(
    () => restore.mutate({ token: file.token, seriesId, seasonNumber }),
    [file, restore, seasonNumber, seriesId]
  );
  const handlePress2 = useCallback(() => setConfirm(false), [setConfirm]);
  const handlePress3 = useCallback(() => setConfirm(true), [setConfirm]);
  return (
    <article className={styles.storage}>
      <strong className={styles.path}>{file.name}</strong>
      <p>
        {formatBytes(file.size)} Â· Recycled{' '}
        {new Date(file.recycledAt).toLocaleDateString()}
      </p>
      {confirm ? (
        <>
          <p>
            Move this file to the selected show's season folder. Existing files
            are kept; a library scan will match its filename to episodes.
          </p>
          <Button
            isDisabled={!seriesId || restore.isPending || restoring}
            onPress={handlePress1}
          >
            Restore file
          </Button>
          <Button onPress={handlePress2}>Cancel</Button>
        </>
      ) : (
        <Button isDisabled={!seriesId || restoring} onPress={handlePress3}>
          Restoreâ€¦
        </Button>
      )}
      {commandId > 0 ? (
        <p role="status">
          {command.data?.message ||
            `Restore ${command.data?.status ?? 'queued'}`}
        </p>
      ) : null}
      {command.error ? (
        <p role="alert">
          Could not read progress. Check System → Tasks before retrying.
        </p>
      ) : null}
      {restore.error ? <p role="alert">{restore.error.message}</p> : null}
    </article>
  );
}

export default function RecycleBrowser() {
  const [open, setOpen] = useState(false);
  const [seriesId, setSeriesId] = useState(0);
  const [seasonNumber, setSeasonNumber] = useState(1);
  const series = useSeries();
  const recycle = useApiQuery<Recycle>({
    path: '/librarytools/recycle',
    queryOptions: { enabled: open },
  });
  const selectedSeries = series.data.find((show) => {
    return show.id === seriesId;
  });
  const handleToggle4 = useCallback(
    (event: React.SyntheticEvent<HTMLDetailsElement>) =>
      setOpen(event.currentTarget.open),
    [setOpen]
  );
  const handleChange5 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      const id = Number(event.target.value);
      setSeriesId(id);
      setSeasonNumber(
        series.data
          .find((show) => show.id === id)
          ?.seasons.find((season) => season.seasonNumber > 0)?.seasonNumber ?? 0
      );
    },
    [series, setSeasonNumber, setSeriesId]
  );
  const handleChange6 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setSeasonNumber(Number(event.target.value)),
    [setSeasonNumber]
  );
  const handlePress7 = useCallback(() => recycle.refetch(), [recycle]);
  return (
    <section>
      <h2>Recycle bin</h2>
      <details onToggle={handleToggle4}>
        <summary>Browse retained deleted files</summary>
        {recycle.error ? <p role="alert">{recycle.error.message}</p> : null}
        {recycle.data && !recycle.data.configured ? (
          <p>
            Set a recycle-bin folder in Settings â†’ Media Management to retain
            files when they are deleted or replaced.
          </p>
        ) : null}
        {recycle.data?.configured ? (
          <>
            <small className={styles.path}>{recycle.data.path}</small>
            <p>
              Compression replaces files directly and does not create
              recycle-bin copies. Existing recycle-bin cleanup rules still
              apply.
            </p>
            <div className={styles.form}>
              <label>
                Restore into show
                <select value={seriesId} onChange={handleChange5}>
                  <option value={0}>Choose a show</option>
                  {[...series.data]
                    .sort((a, b) => {
                      return a.title.localeCompare(b.title);
                    })
                    .map((show) => {
                      return (
                        <option key={show.id} value={show.id}>
                          {show.title} ({show.year})
                        </option>
                      );
                    })}
                </select>
              </label>
              <label>
                Season folder
                <select value={seasonNumber} onChange={handleChange6}>
                  {selectedSeries?.seasons.map((season) => {
                    return (
                      <option
                        key={season.seasonNumber}
                        value={season.seasonNumber}
                      >
                        Season {season.seasonNumber}
                      </option>
                    );
                  })}
                </select>
              </label>
            </div>
            <Button isDisabled={recycle.isFetching} onPress={handlePress7}>
              Refresh files
            </Button>
            {recycle.data.truncated ? (
              <p>Showing the first 2,000 files.</p>
            ) : null}
            {recycle.data.files.length === 0 ? (
              <p>No recycled media files are available.</p>
            ) : null}
            {recycle.data.files.map((file) => {
              return (
                <RestoreFile
                  key={`${file.token}-${seriesId}-${seasonNumber}`}
                  file={file}
                  seriesId={seriesId}
                  seasonNumber={seasonNumber}
                  onRestored={handlePress7}
                />
              );
            })}
          </>
        ) : null}
      </details>
    </section>
  );
}
