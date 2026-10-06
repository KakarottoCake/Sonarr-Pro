import React, { useCallback } from 'react';
import { Link } from 'react-router-dom';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { ProOptions } from './LibraryTools';
import styles from './LibraryTools.module.css';

export default function AutomationBanner() {
  const options = useApiQuery<ProOptions>({
    path: '/librarytools',
    queryOptions: { staleTime: 30000, refetchInterval: 60000 },
  });
  const resume = useApiMutation<ProOptions, ProOptions>({
    path: '/librarytools',
    method: 'PUT',
    mutationOptions: {
      onSuccess: () => {
        return options.refetch();
      },
    },
  });
  const handleClick1 = useCallback(
    () =>
      options.data &&
      resume.mutate({ ...options.data, automationPaused: false }),
    [options, resume]
  );

  if (!options.data?.automationPaused) {
    return null;
  }

  return (
    <aside className={styles.automationBanner} aria-label="Automation paused">
      <span>
        Automation is paused. Manual actions and current downloads continue.
      </span>
      <Link to="/system/library">Library tools</Link>
      <button type="button" disabled={resume.isPending} onClick={handleClick1}>
        {resume.isPending ? 'Resuming…' : 'Resume automation'}
      </button>
      {resume.error ? (
        <span role="alert">Could not resume. Try again.</span>
      ) : null}
    </aside>
  );
}
