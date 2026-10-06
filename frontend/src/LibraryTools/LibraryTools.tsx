import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import formatBytes from 'Utilities/Number/formatBytes';
import ListPreview from './ListPreview';
import RecycleBrowser from './RecycleBrowser';
import SearchActivity from './SearchActivity';
import styles from './LibraryTools.module.css';

export interface ProOptions {
  automationPaused: boolean;
  hardlinkOnly: boolean;
  matchExternalIds: boolean;
  preferAnimeSeasonPacks: boolean;
}
interface CompanionKey {
  id: string;
  name: string;
  permission: string;
  expires: string;
}
interface Storage {
  path: string;
  freeSpace: number | null;
  queuedDownloads: number;
  downloadRemaining: number;
  importSpaceEstimate: number;
  mayRunOutOfSpace: boolean;
  importMode: string;
  explanation: string;
  error: string | null;
}
interface Audit {
  checkedAt: string;
  seriesChecked: number;
  filesChecked: number;
  issues: Array<{
    seriesId: number;
    title: string;
    path: string;
    reason: string;
  }>;
}
const SETTINGS: Array<{
  key: keyof ProOptions;
  label: string;
  help: string;
}> = [
  {
    key: 'automationPaused',
    label: 'Pause automation',
    help: 'Pause new automatic searches, RSS grabs and list additions. Manual searches, imports, current downloads and compression continue.',
  },
  {
    key: 'hardlinkOnly',
    label: 'Hardlink-only imports',
    help: 'Avoid a second copy while seeding. Imports fail with a visible error if the source and library are on different filesystems or linking is unavailable.',
  },
  {
    key: 'matchExternalIds',
    label: 'Check show IDs from indexers',
    help: 'Reject different TVDB or IMDb IDs when both sides supply one. Leave off for indexers that send unreliable IDs.',
  },
  {
    key: 'preferAnimeSeasonPacks',
    label: 'Search anime packs first',
    help: 'Look for a safely matched complete season before searching individual episodes. Incomplete or ambiguous bundles still use episode searches.',
  },
];

export default function LibraryTools() {
  const options = useApiQuery<ProOptions>({ path: '/librarytools' });
  const keys = useApiQuery<CompanionKey[]>({ path: '/librarytools/keys' });
  const storage = useApiQuery<Storage[]>({ path: '/librarytools/storage' });
  const save = useApiMutation<ProOptions, ProOptions>({
    path: '/librarytools',
    method: 'PUT',
    mutationOptions: {
      onSuccess: () => {
        return options.refetch();
      },
    },
  });
  const audit = useApiMutation<Audit, undefined>({
    path: '/librarytools/audit',
    method: 'POST',
  });
  const create = useApiMutation<
    {
      token: string;
    },
    {
      name: string;
      permission: string;
      days: number;
    }
  >({
    path: '/librarytools/keys',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        return keys.refetch();
      },
    },
  });
  const [keyName, setKeyName] = useState('');
  const [permission, setPermission] = useState('read-library');
  const [days, setDays] = useState(90);
  const [revokeId, setRevokeId] = useState('');
  const revoke = useApiMutation<void, undefined>({
    path: `/librarytools/keys/${revokeId}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        setRevokeId('');
        keys.refetch();
      },
    },
  });
  const error =
    options.error ??
    storage.error ??
    keys.error ??
    save.error ??
    create.error ??
    revoke.error ??
    audit.error;
  const handleOptionChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      options.data &&
      save.mutate({
        ...options.data,
        [event.currentTarget.name]: event.currentTarget.checked,
      }),
    [options, save]
  );
  const handleRevokeChoice = useCallback(
    (event: React.MouseEvent<HTMLButtonElement>) =>
      setRevokeId(event.currentTarget.value),
    [setRevokeId]
  );
  const handleRevoke = useCallback(() => revoke.mutate(undefined), [revoke]);
  const handleCancelRevoke = useCallback(() => setRevokeId(''), [setRevokeId]);
  const handlePress2 = useCallback(() => storage.refetch(), [storage]);
  const handlePress3 = useCallback(() => audit.mutate(undefined), [audit]);
  const handleSubmit4 = useCallback(
    (event: React.FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      create.mutate({ name: keyName, permission, days });
    },
    [create, days, keyName, permission]
  );
  const handleChange5 = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setKeyName(event.target.value),
    [setKeyName]
  );
  const handleChange6 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setPermission(event.target.value),
    [setPermission]
  );
  const handleChange7 = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setDays(Number(event.target.value)),
    [setDays]
  );
  const handleFocus8 = useCallback(
    (event: React.FocusEvent<HTMLInputElement>) => event.target.select(),
    []
  );
  return (
    <PageContent title="Library tools">
      <PageHeading title="Library tools" />
      <PageContentBody>
        <div className={styles.tools}>
          {error ? <p role="alert">{error.message}</p> : null}
          <section>
            <h2>Automation and imports</h2>
            {SETTINGS.map(({ key, label, help }) => {
              return (
                <label key={key} className={styles.setting}>
                  <input
                    type="checkbox"
                    name={key}
                    checked={options.data?.[key] ?? false}
                    disabled={!options.data || save.isPending}
                    onChange={handleOptionChange}
                  />
                  <span>
                    <strong>{label}</strong>
                    <small>{help}</small>
                  </span>
                </label>
              );
            })}
            {save.isPending ? <p role="status">Saving…</p> : null}
          </section>
          <section>
            <h2>Storage outlook</h2>
            <Button isDisabled={storage.isFetching} onPress={handlePress2}>
              Refresh estimates
            </Button>
            {storage.data?.map((disk) => {
              return (
                <article key={disk.path} className={styles.storage}>
                  <strong className={styles.path}>{disk.path}</strong>
                  <p>
                    {disk.freeSpace === null
                      ? 'Free space unavailable'
                      : `${formatBytes(disk.freeSpace)} free`}{' '}
                    · {disk.queuedDownloads} queued
                  </p>
                  <p>
                    Possible import copies:{' '}
                    {formatBytes(disk.importSpaceEstimate)} · Downloads
                    remaining: {formatBytes(disk.downloadRemaining)}
                  </p>
                  {disk.mayRunOutOfSpace ? (
                    <p role="alert">
                      Queued imports may exceed available space.
                    </p>
                  ) : null}
                  <small>{disk.error ?? disk.importMode}</small>
                  <small>{disk.explanation}</small>
                </article>
              );
            })}
          </section>
          <section>
            <h2>Library health</h2>
            <p>
              Check registered folders and episode files. Checks leave your
              media untouched.
            </p>
            <Button isDisabled={audit.isPending} onPress={handlePress3}>
              {audit.isPending ? 'Checking…' : 'Check library'}
            </Button>
            {audit.data ? (
              <p role="status">
                Checked {audit.data.seriesChecked} shows and{' '}
                {audit.data.filesChecked} files. {audit.data.issues.length}{' '}
                issues found.
              </p>
            ) : null}
            {audit.data?.issues.map((issue, index) => {
              return (
                <article
                  key={`${issue.seriesId}-${index}`}
                  className={styles.storage}
                >
                  <strong>{issue.title}</strong>
                  <p>{issue.reason}</p>
                  <small className={styles.path}>{issue.path}</small>
                </article>
              );
            })}
          </section>
          <SearchActivity />
          <ListPreview />
          <RecycleBrowser />
          <section>
            <h2>Companion app keys</h2>
            <p>
              Read library keys can read shows, episodes, calendar, tags, root
              folders and quality profiles. Add shows keys can also add a show.
              Settings, logs, downloads, deletion and compression are
              unavailable.
            </p>
            <form className={styles.form} onSubmit={handleSubmit4}>
              <label>
                Name
                <input
                  value={keyName}
                  required={true}
                  maxLength={80}
                  onChange={handleChange5}
                />
              </label>
              <label>
                Permission
                <select value={permission} onChange={handleChange6}>
                  <option value="read-library">Read library</option>
                  <option value="add-series">Read library and add shows</option>
                </select>
              </label>
              <label>
                Expires in days
                <input
                  type="number"
                  value={days}
                  min={1}
                  max={365}
                  onChange={handleChange7}
                />
              </label>
              <button type="submit" disabled={create.isPending}>
                Create key
              </button>
            </form>
            {create.data ? (
              <label className={styles.token}>
                Copy this key now; it is shown once.
                <input
                  readOnly={true}
                  value={create.data.token}
                  onFocus={handleFocus8}
                />
              </label>
            ) : null}
            {keys.data?.map((key) => {
              return (
                <article key={key.id} className={styles.storage}>
                  <strong>{key.name}</strong>
                  <p>
                    {key.permission === 'add-series'
                      ? 'Read library and add shows'
                      : 'Read library'}{' '}
                    · Expires {new Date(key.expires).toLocaleDateString()}
                  </p>
                  {revokeId === key.id ? (
                    <>
                      <span>Revoke access for this app?</span>
                      <Button
                        isDisabled={revoke.isPending}
                        onPress={handleRevoke}
                      >
                        Revoke key
                      </Button>
                      <Button onPress={handleCancelRevoke}>Cancel</Button>
                    </>
                  ) : (
                    <button
                      type="button"
                      value={key.id}
                      onClick={handleRevokeChoice}
                    >
                      Revoke
                    </button>
                  )}
                </article>
              );
            })}
          </section>
        </div>
      </PageContentBody>
    </PageContent>
  );
}
