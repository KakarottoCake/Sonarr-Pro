import React, { useCallback, useEffect, useMemo } from 'react';
import SelectInput from 'Components/Form/SelectInput';
import Button from 'Components/Link/Button';
import useRootFolders from 'RootFolder/useRootFolders';
import { setSeriesOption, useSeriesOption } from 'Series/seriesOptionsStore';
import useSeries from 'Series/useSeries';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './SeriesIndex.module.css';

export default function SeriesPathFilter({
  count,
  total,
}: {
  count: number;
  total: number;
}) {
  const pathFilter = useSeriesOption('pathFilter');
  const series = useSeries();
  const rootFolders = useRootFolders();
  const paths = useMemo(() => {
    const folders = new Set(rootFolders.data.map((folder) => folder.path));

    for (const show of series.data) {
      const parent = show.path.replace(/[\\/][^\\/]+[\\/]?$/, '');

      if (parent) {
        folders.add(parent);
      }
    }

    // Include the mounted drives as well as their individual library folders.
    for (const path of [...folders]) {
      const mount = path.match(/^\/(?:mnt|media)\/[^/]+/);
      const drive = path.match(/^[A-Za-z]:[\\/]/);

      if (mount || drive) {
        folders.add((mount ?? drive)![0]);
      }
    }

    return [...folders].sort((a, b) => a.localeCompare(b));
  }, [series.data, rootFolders.data]);
  const values = useMemo(
    () => [
      { key: '', value: 'All paths' },
      ...paths.map((path) => ({ key: path, value: path })),
    ],
    [paths]
  );
  useEffect(() => {
    // Old free-text filters must not leave an invisible filter behind the picker.
    if (
      series.isFetched &&
      rootFolders.isFetched &&
      pathFilter &&
      !paths.includes(pathFilter)
    ) {
      setSeriesOption('pathFilter', '');
    }
  }, [series.isFetched, rootFolders.isFetched, pathFilter, paths]);
  const handleChange = useCallback(({ value }: InputChanged<string>) => {
    setSeriesOption('pathFilter', value);
  }, []);
  const handleClear = useCallback(() => setSeriesOption('pathFilter', ''), []);

  return (
    <div className={styles.pathFilter}>
      <label className={styles.pathFilterPicker}>
        <span className={styles.pathFilterLabel}>Filter by path</span>
        <SelectInput
          name="series-path-filter"
          value={pathFilter}
          values={values}
          onChange={handleChange}
        />
      </label>
      {pathFilter ? (
        <Button onPress={handleClear}>{translate('Clear')}</Button>
      ) : null}
      <span>
        {count} / {total}
      </span>
    </div>
  );
}
