import React, { useCallback, useEffect } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import {
  OnChildStateChange,
  SetChildSave,
} from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import { useManageMetadataSourceSettings } from './useMetadataSourceSettings';

interface TmdbProps {
  setChildSave: SetChildSave;
  onChildStateChange: OnChildStateChange;
}

function Tmdb({ setChildSave, onChildStateChange }: TmdbProps) {
  const {
    isFetching,
    isFetched,
    isSaving,
    error,
    settings,
    hasSettings,
    hasPendingChanges,
    saveSettings,
    updateSetting,
  } = useManageMetadataSourceSettings();

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      // @ts-expect-error - InputChanged name/value are not typed as keyof MetadataSourceSettingsModel
      updateSetting(name, value);
    },
    [updateSetting]
  );

  useEffect(() => {
    setChildSave(saveSettings);
  }, [saveSettings, setChildSave]);

  useEffect(() => {
    onChildStateChange({
      isSaving,
      hasPendingChanges,
    });
  }, [hasPendingChanges, isSaving, onChildStateChange]);

  return (
    <FieldSet legend={translate('Tmdb')}>
      {isFetching ? <LoadingIndicator /> : null}

      {!isFetching && error ? (
        <Alert kind={kinds.DANGER}>
          {translate('MetadataSourceSettingsLoadError')}
        </Alert>
      ) : null}

      {hasSettings && isFetched && !error ? (
        <Form>
          <FormGroup>
            <FormLabel>{translate('TmdbApiKey')}</FormLabel>

            <FormInputGroup
              type={inputTypes.PASSWORD}
              name="tmdbApiKey"
              helpText={translate('TmdbApiKeyHelpText')}
              helpLink="https://www.themoviedb.org/settings/api"
              onChange={handleInputChange}
              {...settings.tmdbApiKey}
            />
          </FormGroup>
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default Tmdb;
