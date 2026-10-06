import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import useRootFolders from 'RootFolder/useRootFolders';
import { useImportLists } from 'Settings/ImportLists/ImportLists/useImportLists';
import styles from './LibraryTools.module.css';

interface PreviewItem {
  key: number;
  title: string;
  year: number;
  alreadyAdded: boolean;
  supported: boolean;
}
interface Preview {
  token: string;
  partial: boolean;
  items: PreviewItem[];
}

export default function ListPreview() {
  const client = useQueryClient();
  const lists = useImportLists();
  const roots = useRootFolders();
  const profiles = useApiQuery<
    Array<{
      id: number;
      name: string;
    }>
  >({
    path: '/qualityprofile',
  });
  const [listId, setListId] = useState(0);
  const [selected, setSelected] = useState<number[]>([]);
  const [folderId, setFolderId] = useState(0);
  const [profileId, setProfileId] = useState(0);
  const [monitored, setMonitored] = useState(false);
  const preview = useApiQuery<Preview>({
    path: `/librarytools/lists/${listId}`,
    queryOptions: { enabled: listId > 0 },
  });
  const refresh = useApiMutation<void, undefined>({
    path: `/librarytools/lists/${listId}/preview`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        setSelected([]);
        preview.refetch();
      },
    },
  });
  const add = useApiMutation<
    Array<{
      key: number;
      title: string;
      status: string;
    }>,
    {
      token: string;
      keys: number[];
      rootFolderId: number;
      qualityProfileId: number;
      monitored: boolean;
    }
  >({
    path: `/librarytools/lists/${listId}/add`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        setSelected([]);
        preview.refetch();
        client.invalidateQueries({ queryKey: ['/series'] });
      },
    },
  });
  const list = lists.data.find((item) => {
    return item.id === listId;
  });
  const rootFolderId =
    folderId ||
    roots.data.find((root) => {
      return root.path === list?.rootFolderPath;
    })?.id ||
    roots.data[0]?.id ||
    0;
  const qualityProfileId =
    profileId || list?.qualityProfileId || profiles.data?.[0]?.id || 0;
  const error =
    preview.error ??
    add.error ??
    refresh.error ??
    profiles.error ??
    lists.error;
  const handleSelection = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setSelected(
        event.currentTarget.checked
          ? [...selected, Number(event.currentTarget.value)]
          : selected.filter((key) => key !== Number(event.currentTarget.value))
      ),
    [selected, setSelected]
  );
  const handleChange1 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setListId(Number(event.target.value));
      setSelected([]);
      setFolderId(0);
      setProfileId(0);
      add.reset();
    },
    [add, setFolderId, setListId, setProfileId, setSelected]
  );
  const handlePress2 = useCallback(() => refresh.mutate(undefined), [refresh]);
  const handleChange3 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setFolderId(Number(event.target.value)),
    [setFolderId]
  );
  const handleChange4 = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setProfileId(Number(event.target.value)),
    [setProfileId]
  );
  const handleChange5 = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setMonitored(event.target.checked),
    [setMonitored]
  );
  const handlePress7 = useCallback(
    () =>
      preview.data &&
      add.mutate({
        token: preview.data.token,
        keys: selected,
        rootFolderId,
        qualityProfileId,
        monitored,
      }),
    [add, monitored, preview, qualityProfileId, rootFolderId, selected]
  );
  return (
    <section>
      <h2>Preview import lists</h2>
      <p>
        Choose shows before adding them. Saving a list with automatic add off
        lets you use it here. Preview and adding selected shows do not start
        downloads.
      </p>
      <label className={styles.form}>
        List
        <select
          value={listId}
          disabled={add.isPending}
          onChange={handleChange1}
        >
          <option value={0}>Choose a list</option>
          {lists.data.map((item) => {
            return (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            );
          })}
        </select>
      </label>
      {listId > 0 ? (
        <Button
          isDisabled={preview.isFetching || refresh.isPending || add.isPending}
          onPress={handlePress2}
        >
          Refresh preview
        </Button>
      ) : null}
      {error ? <p role="alert">{error.message}</p> : null}
      {preview.isFetching ? <p role="status">Loading list…</p> : null}
      {preview.data ? (
        <>
          {preview.data.partial ? (
            <p role="alert">
              The provider returned a partial list or an error. These entries
              can still be reviewed; test the list in Settings to check access.
            </p>
          ) : null}
          <div className={styles.form}>
            <label>
              Library folder
              <select value={rootFolderId} onChange={handleChange3}>
                {roots.data.map((root) => {
                  return (
                    <option key={root.id} value={root.id}>
                      {root.path}
                    </option>
                  );
                })}
              </select>
            </label>
            <label>
              Quality profile
              <select value={qualityProfileId} onChange={handleChange4}>
                {profiles.data?.map((profile) => {
                  return (
                    <option key={profile.id} value={profile.id}>
                      {profile.name}
                    </option>
                  );
                })}
              </select>
            </label>
          </div>
          <label className={styles.setting}>
            <input
              type="checkbox"
              checked={monitored}
              onChange={handleChange5}
            />
            <span>Monitor added shows for future releases</span>
          </label>
          <div className={styles.previewItems}>
            {preview.data.items.map((item) => {
              return (
                <label key={item.key} className={styles.setting}>
                  <input
                    type="checkbox"
                    checked={selected.includes(item.key)}
                    disabled={
                      add.isPending ||
                      item.alreadyAdded ||
                      !item.supported ||
                      (!selected.includes(item.key) && selected.length >= 25)
                    }
                    value={item.key}
                    onChange={handleSelection}
                  />
                  <span>
                    {item.title}
                    {item.year ? ` (${item.year})` : ''}
                    <small>
                      {item.alreadyAdded ? 'Already in your library' : ''}
                      {item.supported
                        ? ''
                        : 'No supported show ID; add through Search instead'}
                    </small>
                  </span>
                </label>
              );
            })}
          </div>
          <div className={styles.selectionFooter}>
            <span>{selected.length} selected · up to 25 per batch</span>
            <Button
              isDisabled={
                selected.length === 0 ||
                add.isPending ||
                !rootFolderId ||
                !qualityProfileId
              }
              onPress={handlePress7}
            >
              {add.isPending ? 'Adding…' : 'Add selected shows'}
            </Button>
          </div>
          {add.data?.map((item) => {
            return (
              <p key={item.key} role="status">
                {item.title}: {item.status}
              </p>
            );
          })}
        </>
      ) : null}
    </section>
  );
}
