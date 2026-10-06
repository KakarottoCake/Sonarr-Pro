import React, { useCallback } from 'react';
import Button from 'Components/Link/Button';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import styles from './LibraryTools.module.css';

interface SearchSummary {
  date: string;
  query: string;
  found: number;
  accepted: number;
  indexers: number;
  rejections: Record<string, number>;
  providerFailures: string[];
}

export default function SearchActivity() {
  const searches = useApiQuery<SearchSummary[]>({
    path: '/librarytools/searches',
  });
  const handlePress1 = useCallback(() => searches.refetch(), [searches]);
  return (
    <section>
      <h2>Recent searches</h2>
      <p>
        The last 100 indexer queries, including why releases were skipped.
        Accepted releases pass the checks; an automatic search still selects the
        best result and may skip duplicates or episodes already queued.
      </p>
      <Button isDisabled={searches.isFetching} onPress={handlePress1}>
        Refresh searches
      </Button>
      {searches.error ? <p role="alert">{searches.error.message}</p> : null}
      {searches.data?.length === 0 ? (
        <p>New searches will appear here.</p>
      ) : null}
      {searches.data?.map((search, index) => {
        return (
          <details key={`${search.date}-${index}`} className={styles.storage}>
            <summary>
              {search.query} · {search.accepted} eligible / {search.found} found
            </summary>
            <small>
              {new Date(search.date).toLocaleString()} · {search.indexers}{' '}
              indexers searched
            </small>
            {search.found === 0 ? (
              <p>
                No releases found. Check show aliases and indexer search
                support.
              </p>
            ) : null}
            <ul>
              {Object.entries(search.rejections).map(([reason, count]) => {
                return (
                  <li key={reason}>
                    {reason} ({count})
                  </li>
                );
              })}
            </ul>
            {search.providerFailures.map((failure) => {
              return (
                <p key={failure} role="alert">
                  {failure}
                </p>
              );
            })}
          </details>
        );
      })}
    </section>
  );
}
