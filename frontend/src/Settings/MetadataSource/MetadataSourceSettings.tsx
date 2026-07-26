import React, { useCallback, useRef, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SettingsToolbar from 'Settings/SettingsToolbar';
import {
  SaveCallback,
  SettingsStateChange,
} from 'typings/Settings/SettingsState';
import translate from 'Utilities/String/translate';
import Sources from './Sources';
import Tmdb from './Tmdb';

function MetadataSourceSettings() {
  const saveOptions = useRef<() => void>();

  const [isSaving, setIsSaving] = useState(false);
  const [hasPendingChanges, setHasPendingChanges] = useState(false);

  const handleSetChildSave = useCallback((saveCallback: SaveCallback) => {
    saveOptions.current = saveCallback;
  }, []);

  const handleChildStateChange = useCallback(
    ({ isSaving, hasPendingChanges }: SettingsStateChange) => {
      setIsSaving(isSaving);
      setHasPendingChanges(hasPendingChanges);
    },
    []
  );

  const handleSavePress = useCallback(() => {
    saveOptions.current?.();
  }, []);

  return (
    <PageContent title={translate('MetadataSourceSettings')}>
      <SettingsToolbar
        isSaving={isSaving}
        hasPendingChanges={hasPendingChanges}
        onSavePress={handleSavePress}
      />

      <PageContentBody>
        <Sources />

        <Tmdb
          setChildSave={handleSetChildSave}
          onChildStateChange={handleChildStateChange}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default MetadataSourceSettings;
