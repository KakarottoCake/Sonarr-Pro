import React, { useCallback, useEffect } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
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
          <FormRow>
            <FormLabel>{translate('TmdbApiKey')}</FormLabel>

            <FormInputHelpText
              text={translate('TmdbApiKeyHelpText')}
              link="https://www.themoviedb.org/settings/api"
            />
            <FormInput
              type={inputTypes.PASSWORD}
              name="tmdbApiKey"
              onChange={handleInputChange}
              {...settings.tmdbApiKey}
            />
          </FormRow>
        </Form>
      ) : null}
    </FieldSet>
  );
}

export default Tmdb;
