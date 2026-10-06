import React, { Fragment, useCallback, useEffect, useMemo } from 'react';
import { useLocation } from 'react-router';
import QueueStatus from 'Activity/Queue/Status/QueueStatus';
import {
  setIsSidebarVisible,
  useAppDimension,
  useAppValue,
} from 'App/appStore';
import { IconName } from 'Components/Icon';
import BottomSheet from 'Components/Modal/BottomSheet';
import OverlayScroller from 'Components/Scroller/OverlayScroller';
import { icons } from 'Helpers/Props';
import HealthStatus from 'System/Status/Health/HealthStatus';
import translate from 'Utilities/String/translate';
import Messages from './Messages/Messages';
import PageSidebarItem from './PageSidebarItem';
import styles from './PageSidebar.module.css';

interface SidebarItem {
  iconName?: IconName;
  title: string | (() => string);
  to: string;
  alias?: string;
  isActive?: boolean;
  isActiveParent?: boolean;
  isParentItem?: boolean;
  isChildItem?: boolean;
  isHidden?: boolean;
  statusComponent?: React.ElementType;
  children?: {
    title: string | (() => string);
    to: string;
    statusComponent?: React.ElementType;
  }[];
}

interface SidebarGroup {
  label?: () => string;
  items: SidebarItem[];
}

const GROUPS: SidebarGroup[] = [
  {
    label: () => translate('Media'),
    items: [
      {
        iconName: icons.HOME,
        title: () => translate('Home'),
        to: '/',
        isHidden: true,
      },
      {
        iconName: icons.SERIES_CONTINUING,
        title: () => translate('Series'),
        to: '/series',
        alias: '/serieseditor',
        children: [
          {
            title: () => translate('AddNew'),
            to: '/add/new',
          },
          {
            title: () => translate('LibraryImport'),
            to: '/add/import',
          },
        ],
      },
      {
        iconName: icons.CALENDAR,
        title: () => translate('Calendar'),
        to: '/calendar',
      },
      {
        iconName: icons.STATISTICS,
        title: () => translate('Statistics'),
        to: '/statistics',
      },
      {
        iconName: icons.WARNING,
        title: () => translate('Wanted'),
        to: '/wanted/missing',
        children: [
          {
            title: () => translate('Missing'),
            to: '/wanted/missing',
          },
          {
            title: () => translate('CutoffUnmet'),
            to: '/wanted/cutoffunmet',
          },
        ],
      },
    ],
  },
  {
    label: () => translate('Activity'),
    items: [
      {
        iconName: icons.DOWNLOAD,
        title: () => translate('Queue'),
        to: '/activity/queue',
        statusComponent: QueueStatus,
      },
      {
        iconName: icons.HISTORY,
        title: () => translate('History'),
        to: '/activity/history',
      },
      {
        iconName: icons.BLOCKLIST,
        title: () => translate('Blocklist'),
        to: '/activity/blocklist',
      },
    ],
  },
  {
    items: [
      {
        iconName: icons.SETTINGS,
        title: () => translate('Settings'),
        to: '/settings',
        children: [
          {
            title: () => translate('MediaManagement'),
            to: '/settings/mediamanagement',
          },
          { title: () => translate('Profiles'), to: '/settings/profiles' },
          { title: () => translate('Quality'), to: '/settings/quality' },
          {
            title: () => translate('CustomFormats'),
            to: '/settings/customformats',
          },
          { title: () => translate('Indexers'), to: '/settings/indexers' },
          {
            title: () => translate('DownloadClients'),
            to: '/settings/downloadclients',
          },
          {
            title: () => translate('ImportLists'),
            to: '/settings/importlists',
          },
          { title: () => translate('Connect'), to: '/settings/connect' },
          { title: () => translate('Metadata'), to: '/settings/metadata' },
          {
            title: () => translate('MetadataSource'),
            to: '/settings/metadatasource',
          },
          { title: () => translate('Tags'), to: '/settings/tags' },
          { title: () => translate('General'), to: '/settings/general' },
          { title: () => translate('Ui'), to: '/settings/ui' },
        ],
      },
      {
        iconName: icons.SYSTEM,
        title: () => translate('System'),
        to: '/system/status',
        children: [
          {
            title: () => translate('Status'),
            to: '/system/status',
            statusComponent: HealthStatus,
          },
          { title: 'Library tools', to: '/system/library' },
          { title: () => translate('Tasks'), to: '/system/tasks' },
          { title: () => translate('Backup'), to: '/system/backup' },
          { title: () => translate('Updates'), to: '/system/updates' },
          { title: () => translate('Events'), to: '/system/events' },
          { title: () => translate('LogFiles'), to: '/system/logs/files' },
        ],
      },
    ],
  },
];

const FLAT_LINKS: SidebarItem[] = GROUPS.flatMap((g) => g.items).filter(
  (link) => !link.isHidden
);

function hasActiveChildLink(link: SidebarItem, pathname: string) {
  const children = link.children;

  if (!children || !children.length) {
    return false;
  }

  return children.some((child) => {
    return child.to === pathname;
  });
}

function PageSidebar() {
  const isSidebarVisible = useAppValue('isSidebarVisible');
  const isSmallScreen = useAppDimension('isSmallScreen');
  const { pathname } = useLocation();

  useEffect(() => {
    setIsSidebarVisible({ isSidebarVisible: false });
  }, [isSmallScreen]);

  const activeParent = useMemo(() => {
    return (
      FLAT_LINKS.find(
        (link) =>
          link.to === pathname ||
          link.children?.some((child) => pathname.startsWith(child.to)) ||
          (link.to !== '/' && pathname.startsWith(link.to)) ||
          (link.alias && pathname.startsWith(link.alias))
      )?.to ?? FLAT_LINKS[0].to
    );
  }, [pathname]);

  const handleSidebarClose = useCallback(() => {
    setIsSidebarVisible({ isSidebarVisible: false });
  }, []);

  const navigation = (
    <>
      {GROUPS.map((group, groupIndex) => (
        <Fragment key={`group-${groupIndex}`}>
          {!group.label && groupIndex > 0 ? (
            <div className={styles.divider} aria-hidden="true" />
          ) : null}

          {group.label ? (
            <span className={styles.groupLabel}>{group.label()}</span>
          ) : null}

          {group.items
            .filter((link) => !link.isHidden)
            .map((link) => {
              const isActiveParent = activeParent === link.to;
              const childStatusComponent = link.children?.find(
                (child) => !!child.statusComponent
              )?.statusComponent;

              return (
                <PageSidebarItem
                  key={link.to}
                  iconName={link.iconName}
                  title={link.title}
                  to={link.to}
                  statusComponent={
                    isActiveParent || !childStatusComponent
                      ? link.statusComponent
                      : childStatusComponent
                  }
                  isActive={
                    pathname === link.to && !hasActiveChildLink(link, pathname)
                  }
                  isActiveParent={isActiveParent}
                  isParentItem={!isSmallScreen && !!link.children}
                  onPress={handleSidebarClose}
                >
                  {link.children && (isSmallScreen || isActiveParent)
                    ? link.children.map((child) => (
                        <PageSidebarItem
                          key={child.to}
                          title={child.title}
                          to={child.to}
                          isActive={pathname === child.to}
                          isChildItem={true}
                          statusComponent={child.statusComponent}
                          onPress={handleSidebarClose}
                        />
                      ))
                    : null}
                </PageSidebarItem>
              );
            })}
        </Fragment>
      ))}
      <Messages />
    </>
  );

  if (isSmallScreen) {
    return (
      <BottomSheet
        isOpen={isSidebarVisible}
        title={translate('Menu')}
        onModalClose={handleSidebarClose}
      >
        <nav aria-label={translate('MainNavigation')}>{navigation}</nav>
      </BottomSheet>
    );
  }

  return (
    <nav
      className={styles.sidebarContainer}
      aria-label={translate('MainNavigation')}
    >
      <OverlayScroller className={styles.sidebar} scrollDirection="vertical">
        <div>{navigation}</div>
      </OverlayScroller>
    </nav>
  );
}

export default PageSidebar;
