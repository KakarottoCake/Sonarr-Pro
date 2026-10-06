import React, { useCallback } from 'react';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import { setSeriesOption, useSeriesOption } from 'Series/seriesOptionsStore';
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
  const handleChange = useCallback(({ value }: InputChanged<string>) => {
    setSeriesOption('pathFilter', value);
  }, []);
  const handleClear = useCallback(() => setSeriesOption('pathFilter', ''), []);

  return (
    <div className={styles.pathFilter}>
      <span className={styles.pathFilterLabel}>Filter by path</span>
      <TextInput
        name="series-path-filter"
        aria-label="Filter by path"
        placeholder="All paths — type a drive or folder"
        value={pathFilter}
        onChange={handleChange}
      />
      {pathFilter ? (
        <Button onPress={handleClear}>{translate('Clear')}</Button>
      ) : null}
      <span>
        {count} / {total}
      </span>
    </div>
  );
}
