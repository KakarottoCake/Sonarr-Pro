import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface EpisodeOrdering {
  id: string | null;
  name: string;
  description: string;
  episodeCount: number;
  seasonCount: number;
  isAbsolute: boolean;
  isDefault: boolean;
}

const DEFAULT_ORDERINGS: EpisodeOrdering[] = [];

/**
 * Episode orderings published for a series, such as TMDB episode groups.
 *
 * Returns an empty list when the series has no TMDB id or no API key is configured,
 * in which case the series is added with the provider's default ordering.
 */
export const useEpisodeOrderings = (tmdbId: number, isEnabled = true) => {
  const result = useApiQuery<EpisodeOrdering[]>({
    path: '/series/ordering',
    queryParams: {
      tmdbId,
    },
    queryOptions: {
      enabled: isEnabled && tmdbId > 0,
      refetchOnWindowFocus: false,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_ORDERINGS,
  };
};
