import { useQueryClient } from '@tanstack/react-query';
import AddSeries from 'AddSeries/AddSeries';
import { AddSeriesOptions } from 'AddSeries/addSeriesOptionsStore';
import useApiMutation, {
  addOrUpdateQueryClientItem,
} from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import Series from 'Series/Series';

interface AddSeriesPayload
  extends AddSeries,
    Omit<
      AddSeriesOptions,
      'monitor' | 'searchForMissingEpisodes' | 'searchForCutoffUnmetEpisodes'
    > {}

const DEFAULT_SERIES: AddSeries[] = [];

/**
 * The metadata source searched, which becomes the source that owns any series added
 * from the results. A series is owned by exactly one provider.
 */
export type MetadataSource = 'tvdb' | 'tmdb' | 'aniList' | 'myAnimeList';

interface LookupSeriesOptions {
  metadataSource?: MetadataSource;
  isEnabled?: boolean;
}

export const useLookupSeries = (
  query: string,
  { metadataSource = 'tvdb', isEnabled = true }: LookupSeriesOptions = {}
) => {
  const result = useApiQuery<AddSeries[]>({
    path: '/series/lookup',
    queryParams: {
      term: query,
      metadataSource,
    },
    queryOptions: {
      enabled: isEnabled && !!query,
      // Disable refetch on window focus to prevent refetching when the user switch tabs
      refetchOnWindowFocus: false,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_SERIES,
  };
};

export const useAddSeries = () => {
  const queryClient = useQueryClient();

  const { isPending, error, mutate } = useApiMutation<Series, AddSeriesPayload>(
    {
      path: '/series',
      method: 'POST',
      mutationOptions: {
        onSuccess: (newSeries) => {
          queryClient.setQueryData<Series[]>(['/series'], (oldSeries = []) =>
            addOrUpdateQueryClientItem(oldSeries, newSeries, 'id')
          );
        },
      },
    }
  );

  return {
    isAdding: isPending,
    addError: error,
    addSeries: mutate,
  };
};
