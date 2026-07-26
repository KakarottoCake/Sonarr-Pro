import { useManageSettings, useSettings } from 'Settings/useSettings';

export interface MetadataSourceSettingsModel {
  /**
   * Comes back masked once saved, never as the real key. Sending the mask back
   * leaves the stored key alone.
   */
  tmdbApiKey: string;
  tmdbApiKeyConfigured: boolean;
}

const PATH = '/settings/metadatasource';

export const useMetadataSourceSettings = () => {
  return useSettings<MetadataSourceSettingsModel>(PATH);
};

export const useManageMetadataSourceSettings = () => {
  return useManageSettings<MetadataSourceSettingsModel>(PATH);
};
