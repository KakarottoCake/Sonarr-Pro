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
 * Episode orderings published for a series: TMDB episode groups, and IMDb's own
 * numbering once the local IMDb index has been built.
 *
 * Both ids are sent because they are answered by different sources. A series known
 * to IMDb but not TMDB still has an ordering to offer, and asking only about TMDB
 * would hide it.
 *
 * An empty list means the series is added with the provider's default ordering,
 * which is the normal case for a series with nothing published and for an install
 * with no TMDB key.
 */
export const useEpisodeOrderings = (
  tmdbId: number,
  imdbId?: string,
  isEnabled = true
) => {
  const result = useApiQuery<EpisodeOrdering[]>({
    path: '/series/ordering',
    queryParams: {
      tmdbId,
      imdbId,
    },
    queryOptions: {
      enabled: isEnabled && (tmdbId > 0 || !!imdbId),
      refetchOnWindowFocus: false,
    },
  });

  return {
    ...result,
    data: result.data ?? DEFAULT_ORDERINGS,
  };
};
