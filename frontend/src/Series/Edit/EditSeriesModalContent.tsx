import React, { useCallback, useEffect, useMemo, useState } from 'react';
import SeriesMonitorNewItemsOptionsPopoverContent from 'AddSeries/SeriesMonitorNewItemsOptionsPopoverContent';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputButton from 'Components/Form/FormInputButton';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Popover from 'Components/Tooltip/Popover';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { usePendingChangesStore } from 'Helpers/Hooks/usePendingChangesStore';
import usePrevious from 'Helpers/Hooks/usePrevious';
import {
  icons,
  inputTypes,
  kinds,
  sizes,
  tooltipPositions,
} from 'Helpers/Props';
import MoveSeriesModal from 'Series/MoveSeries/MoveSeriesModal';
import Series from 'Series/Series';
import { useSaveSeries, useSingleSeries } from 'Series/useSeries';
import { InputChanged } from 'typings/inputs';
import selectSettings from 'Utilities/selectSettings';
import translate from 'Utilities/String/translate';
import RootFolderModal from './RootFolder/RootFolderModal';
import { RootFolderUpdated } from './RootFolder/RootFolderModalContent';
import styles from './EditSeriesModalContent.module.css';

export interface EditSeriesModalContentProps {
  seriesId: number;
  onModalClose: () => void;
  onDeleteSeriesPress: () => void;
}

function EditSeriesModalContent({
  seriesId,
  onModalClose,
  onDeleteSeriesPress,
}: EditSeriesModalContentProps) {
  const series = useSingleSeries(seriesId)!;

  const {
    title,
    monitored,
    monitorNewItems,
    seasonFolder,
    qualityProfileId,
    seriesType,
    path,
    tags,
    rootFolderPath: initialRootFolderPath,
  } = series;

  const { pendingChanges, setPendingChange } = usePendingChangesStore<Series>(
    {}
  );

  const [isRootFolderModalOpen, setIsRootFolderModalOpen] = useState(false);
  const [rootFolderPath, setRootFolderPath] = useState(initialRootFolderPath);
  const isPathChanging = !!(
    pendingChanges.path && path !== pendingChanges.path
  );
  const [isConfirmMoveModalOpen, setIsConfirmMoveModalOpen] = useState(false);

  const {
    saveSeries,
    isSaving: isSavingWithoutMove,
    saveError: saveWithoutMoveError,
  } = useSaveSeries(false);
  const {
    saveSeries: saveAndMoveSeries,
    isSaving: isSavingWithMove,
    saveError: saveWithMoveError,
  } = useSaveSeries(true);
  const [isMovingFiles, setIsMovingFiles] = useState(false);
  const isSaving = isSavingWithoutMove || isSavingWithMove;
  const saveError = isMovingFiles ? saveWithMoveError : saveWithoutMoveError;
  const { data: namingFolder, isFetching: isFolderFetching } = useApiQuery<{
    folder: string;
  }>({ path: `/series/${seriesId}/folder` });
  const wasSaving = usePrevious(isSaving);

  const { settings, ...otherSettings } = useMemo(() => {
    return selectSettings(
      {
        monitored,
        monitorNewItems,
        seasonFolder,
        qualityProfileId,
        seriesType,
        path,
        tags,
      },
      pendingChanges,
      saveError
    );
  }, [
    monitored,
    monitorNewItems,
    seasonFolder,
    qualityProfileId,
    seriesType,
    path,
    tags,
    pendingChanges,
    saveError,
  ]);

  const currentPath = settings.path.value;
  const separatorIndex = Math.max(
    currentPath.lastIndexOf('/'),
    currentPath.lastIndexOf('\\')
  );
  const parentPath = currentPath.slice(0, separatorIndex + 1);
  const folderName = currentPath.slice(separatorIndex + 1);
  const isFolderNameInvalid =
    !folderName.trim() || folderName === '.' || folderName === '..';

  const handleFolderNameChange = useCallback(
    ({ value }: InputChanged<string>) => {
      if (!/[\\/]/.test(value)) {
        setPendingChange('path', `${parentPath}${value}`);
      }
    },
    [parentPath, setPendingChange]
  );

  const handleUseNamingFormat = useCallback(() => {
    if (namingFolder?.folder && parentPath) {
      setPendingChange('path', `${parentPath}${namingFolder.folder}`);
    }
  }, [namingFolder, parentPath, setPendingChange]);

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged) => {
      // @ts-expect-error name needs to be keyof Series
      setPendingChange(name, value);
    },
    [setPendingChange]
  );

  const handleRootFolderPress = useCallback(() => {
    setIsRootFolderModalOpen(true);
  }, []);

  const handleRootFolderModalClose = useCallback(() => {
    setIsRootFolderModalOpen(false);
  }, []);

  const handleRootFolderChange = useCallback(
    ({
      path: newPath,
      rootFolderPath: newRootFolderPath,
    }: RootFolderUpdated) => {
      setIsRootFolderModalOpen(false);
      setRootFolderPath(newRootFolderPath);
      handleInputChange({ name: 'path', value: newPath });
    },
    [handleInputChange]
  );

  const handleCancelPress = useCallback(() => {
    setIsConfirmMoveModalOpen(false);
  }, []);

  const handleSavePress = useCallback(() => {
    if (isPathChanging && !isConfirmMoveModalOpen) {
      setIsConfirmMoveModalOpen(true);
    } else {
      setIsConfirmMoveModalOpen(false);

      setIsMovingFiles(false);

      saveSeries({
        ...series,
        ...pendingChanges,
      });
    }
  }, [
    series,
    isPathChanging,
    isConfirmMoveModalOpen,
    pendingChanges,
    saveSeries,
  ]);

  const handleMoveSeriesPress = useCallback(() => {
    setIsConfirmMoveModalOpen(false);
    setIsMovingFiles(true);

    saveAndMoveSeries({
      ...series,
      ...pendingChanges,
    });
  }, [series, pendingChanges, saveAndMoveSeries]);

  useEffect(() => {
    if (!isSaving && wasSaving && !saveError) {
      onModalClose();
    }
  }, [isSaving, wasSaving, saveError, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('EditSeriesModalHeader', { title })}</ModalHeader>

      <ModalBody>
        <Form {...otherSettings}>
          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('Monitored')}</FormLabel>

            <FormInputHelpText text={translate('MonitoredEpisodesHelpText')} />
            <FormInput
              type={inputTypes.CHECK}
              name="monitored"
              {...settings.monitored}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>
              {translate('MonitorNewSeasons')}
              <Popover
                anchor={<Icon className={styles.labelIcon} name={icons.INFO} />}
                title={translate('MonitorNewSeasons')}
                body={<SeriesMonitorNewItemsOptionsPopoverContent />}
                position={tooltipPositions.RIGHT}
              />
            </FormLabel>

            <FormInputHelpText text={translate('MonitorNewSeasonsHelpText')} />
            <FormInput
              type={inputTypes.MONITOR_NEW_ITEMS_SELECT}
              name="monitorNewItems"
              {...settings.monitorNewItems}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('UseSeasonFolder')}</FormLabel>

            <FormInputHelpText text={translate('UseSeasonFolderHelpText')} />
            <FormInput
              type={inputTypes.CHECK}
              name="seasonFolder"
              {...settings.seasonFolder}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('QualityProfile')}</FormLabel>

            <FormInput
              type={inputTypes.QUALITY_PROFILE_SELECT}
              name="qualityProfileId"
              {...settings.qualityProfileId}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('SeriesType')}</FormLabel>

            <FormInputHelpText text={translate('SeriesTypesHelpText')} />
            <FormInput
              type={inputTypes.SERIES_TYPE_SELECT}
              name="seriesType"
              {...settings.seriesType}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('SeriesFolderName')}</FormLabel>

            <FormInputHelpText text={translate('SeriesFolderRenameHelpText')} />
            <div className={styles.folderControls}>
              <FormInput
                type={inputTypes.TEXT}
                name="folderName"
                aria-label={translate('SeriesFolderName')}
                value={folderName}
                onChange={handleFolderNameChange}
              />
              <Button
                className={styles.namingFormatButton}
                isDisabled={
                  isFolderFetching || !namingFolder?.folder || !parentPath
                }
                onPress={handleUseNamingFormat}
              >
                {translate('UseNamingFormat')}
              </Button>
              <FormInputHelpText
                text={namingFolder?.folder}
                tooltip={translate('SeriesFolderNamingFormatHelpText')}
              />
              <FormInputHelpText
                text={
                  isFolderNameInvalid
                    ? translate('SeriesFolderNameInvalid')
                    : undefined
                }
                isError={true}
              />
            </div>
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('Path')}</FormLabel>

            <FormInput
              type={inputTypes.PATH}
              name="path"
              {...settings.path}
              buttons={[
                <FormInputButton
                  key="fileBrowser"
                  kind={kinds.DEFAULT}
                  title={translate('RootFolder')}
                  onPress={handleRootFolderPress}
                >
                  <Icon name={icons.ROOT_FOLDER} />
                </FormInputButton>,
              ]}
              includeFiles={false}
              onChange={handleInputChange}
            />
          </FormRow>

          <FormRow size={sizes.MEDIUM}>
            <FormLabel>{translate('Tags')}</FormLabel>

            <FormInput
              type={inputTypes.TAG}
              name="tags"
              {...settings.tags}
              onChange={handleInputChange}
            />
          </FormRow>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button
          className={styles.deleteButton}
          kind={kinds.DANGER}
          onPress={onDeleteSeriesPress}
        >
          {translate('Delete')}
        </Button>

        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerErrorButton
          error={saveError}
          isSpinning={isSaving}
          isDisabled={isFolderNameInvalid}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>

      <RootFolderModal
        isOpen={isRootFolderModalOpen}
        seriesId={seriesId}
        rootFolderPath={rootFolderPath}
        onSavePress={handleRootFolderChange}
        onModalClose={handleRootFolderModalClose}
      />

      <MoveSeriesModal
        originalPath={path}
        destinationPath={pendingChanges.path}
        isOpen={isConfirmMoveModalOpen}
        onModalClose={handleCancelPress}
        onSavePress={handleSavePress}
        onMoveSeriesPress={handleMoveSeriesPress}
      />
    </ModalContent>
  );
}

export default EditSeriesModalContent;
