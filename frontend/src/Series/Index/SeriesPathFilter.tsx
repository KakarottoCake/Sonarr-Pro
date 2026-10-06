import React, { useCallback, useEffect, useMemo } from 'react';
import SelectInput from 'Components/Form/SelectInput';
import Button from 'Components/Link/Button';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import useRootFolders from 'RootFolder/useRootFolders';
import { setSeriesOption, useSeriesOption } from 'Series/seriesOptionsStore';
import useSeries, { FILTERS } from 'Series/useSeries';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './SeriesIndex.module.css';

const EMPTY_FILTER_KEYS: string[] = [];

export default function SeriesPathFilter({
  count,
  total,
}: {
  count: number;
  total: number;
}) {
  const pathFilter = useSeriesOption('pathFilter');
  const additionalFilterKeys =
    useSeriesOption('additionalFilterKeys') ?? EMPTY_FILTER_KEYS;
  const customFilters = useCustomFiltersList('series');
  const series = useSeries();
  const rootFolders = useRootFolders();
  const paths = useMemo(() => {
    const folders = new Set(
      rootFolders.data.map((folder) => {
        return folder.path;
      })
    );

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

    return [...folders].sort((a, b) => {
      return a.localeCompare(b);
    });
  }, [series.data, rootFolders.data]);
  const values = useMemo(() => {
    return [
      { key: '', value: 'All paths' },
      ...paths.map((path) => {
        return { key: path, value: path };
      }),
    ];
  }, [paths]);
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
  const handleClear = useCallback(() => {
    return setSeriesOption('pathFilter', '');
  }, []);
  const handleCombinedChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setSeriesOption(
        'additionalFilterKeys',
        event.currentTarget.checked
          ? [...additionalFilterKeys, event.currentTarget.value]
          : additionalFilterKeys.filter(
              (key) => key !== event.currentTarget.value
            )
      ),
    [additionalFilterKeys]
  );
  const handlePress2 = useCallback(
    () => setSeriesOption('additionalFilterKeys', []),
    []
  );
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
      <details className={styles.combinedFilters}>
        <summary>
          Combine filters{' '}
          {additionalFilterKeys.length
            ? `(${additionalFilterKeys.length})`
            : ''}
        </summary>
        <fieldset>
          <legend>Show series matching every selected filter</legend>
          {[
            ...FILTERS,
            ...customFilters.map((filter) => {
              return { ...filter, key: filter.id };
            }),
          ]
            .filter((filter) => {
              return filter.key !== 'all';
            })
            .map((filter) => {
              return (
                <label key={filter.key}>
                  <input
                    type="checkbox"
                    checked={additionalFilterKeys.includes(filter.key)}
                    value={filter.key}
                    onChange={handleCombinedChange}
                  />
                  {typeof filter.label === 'function'
                    ? filter.label()
                    : filter.label}
                </label>
              );
            })}
          <Button onPress={handlePress2}>Clear combined filters</Button>
        </fieldset>
      </details>
      <span>
        {count} / {total}
      </span>
    </div>
  );
}
