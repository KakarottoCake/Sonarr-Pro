import React, { type ReactNode } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PendingChangesModal from './PendingChangesModal';
import SettingsSaveItems from './SettingsSaveItems';
import useSettingsSave from './useSettingsSave';
import styles from './SettingsPage.module.css';

interface SettingsPageProps {
  title: string;
  showSave?: boolean;
  isSaving?: boolean;
  hasPendingChanges?: boolean;
  onSavePress?: () => void;
  toolbarChildren?: ReactNode;
  children?: ReactNode;
}

function SettingsPage({
  title,
  showSave = true,
  isSaving = false,
  hasPendingChanges = false,
  onSavePress,
  toolbarChildren,
  children,
}: SettingsPageProps) {
  const { pendingChangesModalProps } = useSettingsSave({
    hasPendingChanges,
    onSavePress,
  });

  return (
    <PageContent className={styles.content} title={title}>
      <PageToolbar className={styles.toolbar}>
        <SettingsSaveItems
          showSave={showSave}
          isSaving={isSaving}
          hasPendingChanges={hasPendingChanges}
          onSavePress={onSavePress}
        />
        {toolbarChildren}
      </PageToolbar>

      <PendingChangesModal {...pendingChangesModalProps} />

      {children}
    </PageContent>
  );
}

export default SettingsPage;
