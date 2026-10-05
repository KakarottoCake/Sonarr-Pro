import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import { MetadataSource } from './useAddSeries';

const { useOption, setOption } = createOptionsStore<{ source: MetadataSource }>(
  'metadata_source_options',
  () => ({ source: 'tvdb' })
);

export const useMetadataSourceOption = () => useOption('source');
export const setMetadataSourceOption = (source: MetadataSource) =>
  setOption('source', source);
