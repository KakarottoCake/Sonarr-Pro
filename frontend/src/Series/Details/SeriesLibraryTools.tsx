import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useEffect, useState } from 'react';
import Button from 'Components/Link/Button';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import styles from 'LibraryTools/LibraryTools.module.css';
import { useSingleSeries } from 'Series/useSeries';
import formatBytes from 'Utilities/Number/formatBytes';

interface SeriesOptions {
  keepVersions: boolean;
  automaticRenaming: string;
  seasonTitles: Record<string, string>;
}
interface Version {
  id: number;
  created: string;
  path: string;
  available: boolean;
  metadata: {
    size: number;
    quality: {
      quality: {
        name: string;
      };
    };
    releaseGroup: string;
  };
  episodeIds: number[];
}

function VersionRow({
  version,
  seriesId,
}: {
  version: Version;
  seriesId: number;
}) {
  const client = useQueryClient();
  const [confirm, setConfirm] = useState(false);
  const [commandId, setCommandId] = useState(0);
  const command = useApiQuery<{
    status: string;
    message: string;
  }>({
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
      client.invalidateQueries({ queryKey: ['/episodeFile'] });
      client.invalidateQueries({
        queryKey: [`/librarytools/series/${seriesId}/versions`],
      });
    }
  }, [command.data?.status, client, seriesId]);
  const changing =
    commandId > 0 &&
    !['completed', 'failed', 'cancelled', 'aborted', 'orphaned'].includes(
      command.data?.status ?? ''
    );
  const activate = useApiMutation<
    {
      commandId: number;
    },
    undefined
  >({
    path: `/librarytools/series/${seriesId}/versions/${version.id}/activate`,
    method: 'POST',
    mutationOptions: {
      onSuccess: (result) => {
        setCommandId(result.commandId);
        setConfirm(false);
      },
    },
  });
  const handlePress1 = useCallback(
    () => activate.mutate(undefined),
    [activate]
  );
  const handlePress2 = useCallback(() => setConfirm(false), [setConfirm]);
  const handlePress3 = useCallback(() => setConfirm(true), [setConfirm]);
  return (
    <article className={styles.storage}>
      <strong>
        {version.metadata.quality?.quality?.name ?? 'Unknown quality'} ·{' '}
        {formatBytes(version.metadata.size)} {version.metadata.releaseGroup}
      </strong>
      <small className={styles.path}>{version.path}</small>
      <small>
        Retained {new Date(version.created).toLocaleString()} ·{' '}
        {version.episodeIds.length} episodes
      </small>
      {confirm ? (
        <>
          <p>
            The current file will be retained before this version becomes
            active.
          </p>
          <Button
            isDisabled={!version.available || activate.isPending || changing}
            onPress={handlePress1}
          >
            Activate version
          </Button>
          <Button onPress={handlePress2}>Cancel</Button>
        </>
      ) : (
        <Button
          isDisabled={!version.available || changing}
          onPress={handlePress3}
        >
          {version.available ? 'Use this version' : 'File unavailable'}
        </Button>
      )}
      {activate.error ? <p role="alert">{activate.error.message}</p> : null}
      {commandId > 0 ? (
        <p role="status">
          {command.data?.message ||
            `Version change ${command.data?.status ?? 'queued'}`}
        </p>
      ) : null}
      {command.error ? (
        <p role="alert">
          Could not read progress. Check System → Tasks before retrying.
        </p>
      ) : null}
    </article>
  );
}

export default function SeriesLibraryTools({ seriesId }: { seriesId: number }) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<SeriesOptions | null>(null);
  const series = useSingleSeries(seriesId);
  const options = useApiQuery<SeriesOptions>({
    path: `/librarytools/series/${seriesId}`,
    queryOptions: { enabled: open },
  });
  const versions = useApiQuery<Version[]>({
    path: `/librarytools/series/${seriesId}/versions`,
    queryOptions: { enabled: open },
  });
  const save = useApiMutation<SeriesOptions, SeriesOptions>({
    path: `/librarytools/series/${seriesId}`,
    method: 'PUT',
    mutationOptions: {
      onSuccess: () => {
        setDraft(null);
        options.refetch();
      },
    },
  });
  const value = draft ?? options.data;
  const handleSeasonTitle = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      value &&
      setDraft({
        ...value,
        seasonTitles: {
          ...value.seasonTitles,
          [event.currentTarget.name]: event.currentTarget.value,
        },
      }),
    [setDraft, value]
  );
  const handleToggle4 = useCallback(
    (event: React.SyntheticEvent<HTMLDetailsElement>) =>
      setOpen(event.currentTarget.open),
    [setOpen]
  );
  const handleSubmit5 = useCallback(
    (event: React.FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (value) save.mutate(value);
    },
    [save, value]
  );
  const handleChange6 = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      value && setDraft({ ...value, keepVersions: event.target.checked }),
    [setDraft, value]
  );
  const handleChange7 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      value && setDraft({ ...value, automaticRenaming: event.target.value }),
    [setDraft, value]
  );
  return (
    <details className={styles.storage} onToggle={handleToggle4}>
      <summary>Files, naming and retained versions</summary>
      {options.error ? <p role="alert">{options.error.message}</p> : null}
      {value ? (
        <form className={styles.tools} onSubmit={handleSubmit5}>
          <label className={styles.setting}>
            <input
              type="checkbox"
              checked={value.keepVersions}
              onChange={handleChange6}
            />
            <span>
              <strong>Keep previous versions when upgrading</strong>
              <small>
                Retain replaced files in this show's Plex Versions folder. They
                can be made active here. Each retained copy uses disk space.
                Existing files are kept as they are until an upgrade occurs.
              </small>
            </span>
          </label>
          <label>
            Automatic file renaming
            <select value={value.automaticRenaming} onChange={handleChange7}>
              <option value="default">Use global setting</option>
              <option value="manual">Only rename when I request it</option>
              <option value="automatic">Rename on import</option>
            </select>
            <small>
              Preview Rename always works, including when automatic renaming is
              off.
            </small>
          </label>
          <details>
            <summary>Season folder titles</summary>
            <p>
              Optional names for the {'{Season Title}'} token. {'{Season Year}'}{' '}
              uses the first episode's air year. Set your folder format in
              Settings → Media Management, then use Preview Rename to apply it.
            </p>
            {series?.seasons.map((season) => {
              return (
                <label key={season.seasonNumber} className={styles.form}>
                  Season {season.seasonNumber}
                  <input
                    maxLength={200}
                    value={
                      value.seasonTitles[String(season.seasonNumber)] ?? ''
                    }
                    placeholder="Use metadata title"
                    name={String(season.seasonNumber)}
                    onChange={handleSeasonTitle}
                  />
                </label>
              );
            })}
          </details>
          <button type="submit" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save series options'}
          </button>
          {save.isSuccess ? <p role="status">Saved</p> : null}
          {save.error ? <p role="alert">{save.error.message}</p> : null}
        </form>
      ) : null}
      <h3>Retained files</h3>
      {versions.error ? <p role="alert">{versions.error.message}</p> : null}
      {versions.data?.length === 0 ? <p>No retained versions yet.</p> : null}
      {versions.data?.map((version) => {
        return (
          <VersionRow key={version.id} version={version} seriesId={seriesId} />
        );
      })}
    </details>
  );
}
