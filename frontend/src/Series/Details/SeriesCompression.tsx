import { useQueryClient } from '@tanstack/react-query';
import React, { ChangeEvent, useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { kinds } from 'Helpers/Props';
import formatBytes from 'Utilities/Number/formatBytes';
import styles from './SeriesCompression.module.css';

interface CompressionMode {
  id: string;
  name: string;
  description: string;
  available: boolean;
  minSizeMb: number;
}

interface CompressionJob {
  id: string;
  status: string;
  modeName: string;
  phase: string;
  percent: number;
  filePercent: number;
  currentFile: string;
  totalFiles: number;
  completedFiles: number;
  compressedFiles: number;
  skippedFiles: number;
  failedFiles: number;
  savedBytes: number;
  message: string;
  results: { file: string; status: string; reason: string }[];
}

interface CompressionState {
  available: boolean;
  job: CompressionJob | null;
}

const activeStatuses = ['queued', 'running', 'cancelling'];
const statusLabels: Record<string, string> = {
  queued: 'Queued',
  running: 'Compressing',
  cancelling: 'Stopping',
  cancelled: 'Cancelled',
  completed: 'Finished',
  completedWithErrors: 'Finished with errors',
  interrupted: 'Interrupted',
  failed: 'Failed',
};

export default function SeriesCompression({ seriesId }: { seriesId: number }) {
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const [mode, setMode] = useState('software-fast');
  const path = `/series/${seriesId}/compression`;
  const capabilities = useApiQuery<{
    available: boolean;
    cpu: string;
    threads: number;
    hardware: string;
    modes: CompressionMode[];
    message?: string;
  }>({
    path: '/series/compression/capabilities',
    queryOptions: { refetchInterval: 30000 },
  });
  const state = useApiQuery<CompressionState>({
    path,
    queryOptions: {
      refetchInterval: (query) =>
        activeStatuses.includes(query.state.data?.job?.status ?? '')
          ? 2000
          : 10000,
    },
  });
  const start = useApiMutation<
    CompressionState,
    { mode: string; minSizeMb: number }
  >({
    path,
    method: 'POST',
    mutationOptions: {
      onSuccess: (data) => {
        queryClient.setQueryData([path], data);
        setIsOpen(false);
      },
    },
  });
  const cancel = useApiMutation<CompressionState, void>({
    path,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: (data) => queryClient.setQueryData([path], data),
    },
  });
  const job = state.data?.job;
  const isActive = !!job && activeStatuses.includes(job.status);
  const isAvailable = capabilities.data?.available && state.data?.available;
  const selectedMode = capabilities.data?.modes.find((m) => m.id === mode);
  const [showDetails, setShowDetails] = useState(false);
  const jobId = job?.id;
  const jobStatus = job?.status;

  useEffect(() => {
    if (jobStatus && !activeStatuses.includes(jobStatus)) {
      queryClient.invalidateQueries({ queryKey: ['/series'] });
      queryClient.invalidateQueries({ queryKey: ['/episodeFile'] });
    }
  }, [jobId, jobStatus, queryClient]);

  const handleOpen = useCallback(() => {
    start.reset();
    cancel.reset();
    setIsOpen(true);
  }, [start, cancel]);

  const handleClose = useCallback(() => {
    setIsOpen(false);
  }, []);

  const handleCancel = useCallback(() => {
    cancel.mutate();
  }, [cancel]);

  const handleDetails = useCallback(() => {
    setShowDetails(!showDetails);
  }, [showDetails]);

  const handleMode = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    setMode(event.target.value);
  }, []);

  const handleStart = useCallback(() => {
    start.mutate({ mode, minSizeMb: selectedMode?.minSizeMb ?? 0 });
  }, [start, mode, selectedMode]);

  const error =
    start.error ?? cancel.error ?? state.error ?? capabilities.error;

  return (
    <section className={styles.panel} aria-label="Show compression">
      <div className={styles.heading}>
        <div>
          <h3>Save space</h3>
          <span>Compress this show’s imported episodes.</span>
        </div>
        <Button
          kind={kinds.PRIMARY}
          isDisabled={!isAvailable || isActive || start.isPending}
          onPress={handleOpen}
        >
          Compress show
        </Button>
      </div>
      {isAvailable ? null : (
        <p>
          {capabilities.isLoading
            ? 'Checking server encoders…'
            : 'Compression worker is not connected.'}
        </p>
      )}
      {error ? (
        <Alert kind={kinds.DANGER}>
          {error.statusBody?.message ??
            'Unable to contact the compression worker.'}
        </Alert>
      ) : null}
      {job ? (
        <div className={styles.job} aria-live="polite">
          <div className={styles.heading}>
            <strong>
              {statusLabels[job.status] ?? job.status} · {job.modeName}
            </strong>
            <span>{Math.round(job.percent)}%</span>
          </div>
          <progress
            className={styles.progress}
            aria-label="Show compression progress"
            value={job.percent}
            max={100}
          />
          <span>
            {job.completedFiles} / {job.totalFiles} files checked ·{' '}
            {formatBytes(job.savedBytes)} library reduction
          </span>
          {job.currentFile ? (
            <div className={styles.currentFile}>
              <span>
                {job.phase === 'validating' ? 'Validating' : 'Current file'}:{' '}
                {job.currentFile}
              </span>
              <progress
                className={styles.progress}
                aria-label="Current file progress"
                value={job.filePercent}
                max={100}
              />
            </div>
          ) : null}
          <p>{job.message}</p>
          <span>
            {job.compressedFiles} compressed · {job.skippedFiles} skipped ·{' '}
            {job.failedFiles} errors
          </span>
          <div className={styles.actions}>
            {isActive ? (
              <Button
                isDisabled={cancel.isPending || job.status === 'cancelling'}
                onPress={handleCancel}
              >
                Stop compression
              </Button>
            ) : null}
            {job.results.length ? (
              <Button onPress={handleDetails}>
                {showDetails ? 'Hide' : 'Show'} file results
              </Button>
            ) : null}
          </div>
          {showDetails ? (
            <ul className={styles.results}>
              {job.results.map((result, index) => (
                <li key={index}>
                  <strong>{result.file}</strong>
                  <span>{result.reason}</span>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}
      <Modal isOpen={isOpen} onModalClose={handleClose}>
        <ModalContent onModalClose={handleClose}>
          <ModalHeader>Compress this show</ModalHeader>
          <ModalBody>
            <p>
              {capabilities.data?.cpu} · {capabilities.data?.threads} CPU
              encoding threads
            </p>
            <p>
              {capabilities.data?.hardware}. This server’s hardware encoder uses
              H.264.
            </p>
            <div className={styles.modes}>
              {capabilities.data?.modes.map((item) => (
                <label key={item.id} className={styles.mode}>
                  <input
                    type="radio"
                    name="compression-mode"
                    value={item.id}
                    checked={mode === item.id}
                    disabled={!item.available}
                    onChange={handleMode}
                  />
                  <div>
                    <strong>{item.name}</strong>
                    <span>
                      {item.description}
                      {item.available ? '' : ' Unavailable on this server.'}
                    </span>
                  </div>
                </label>
              ))}
            </div>
            <p>
              Audio, subtitles and resolution are preserved. HDR, high bit-depth
              video and files already encoded with HEVC or AV1 are skipped.
              Originals are replaced only after a complete validation and at
              least 2% savings.
            </p>
            <p>
              You can leave this page. Progress continues on the server, one
              show at a time.
            </p>
            <p>
              Hard-linked download originals stay intact. Their disk space is
              reclaimed only when those downloads are removed.
            </p>
            {start.error ? (
              <Alert kind={kinds.DANGER}>
                {start.error.statusBody?.message ??
                  'Could not start compression.'}
              </Alert>
            ) : null}
          </ModalBody>
          <ModalFooter>
            <Button onPress={handleClose}>Close</Button>
            <Button
              kind={kinds.PRIMARY}
              isDisabled={!selectedMode?.available || start.isPending}
              onPress={handleStart}
            >
              {start.isPending ? 'Starting…' : 'Start compression'}
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </section>
  );
}
