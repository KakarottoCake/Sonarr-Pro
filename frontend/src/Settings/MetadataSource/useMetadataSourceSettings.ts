import { useManageSettings, useSettings } from 'Settings/useSettings';

export interface MetadataSourceSettingsModel {
  tmdbApiKey: string;
}

const PATH = '/settings/metadatasource';

export const useMetadataSourceSettings = () => {
  return useSettings<MetadataSourceSettingsModel>(PATH);
};

export const useManageMetadataSourceSettings = () => {
  return useManageSettings<MetadataSourceSettingsModel>(PATH);
};
