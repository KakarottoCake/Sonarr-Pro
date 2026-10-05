import classNames from 'classnames';
import React from 'react';
import { useLocation } from 'react-router';
import {
  toggleIsSidebarVisible,
  useAppDimension,
  useAppValue,
} from 'App/appStore';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './MobileNavigation.module.css';

const LINKS = [
  {
    to: '/series',
    icon: icons.SERIES_CONTINUING,
    title: 'Series',
    matches: (path: string) =>
      path === '/' || path.startsWith('/series') || path.startsWith('/add/'),
  },
  {
    to: '/calendar',
    icon: icons.CALENDAR,
    title: 'Calendar',
    matches: (path: string) => path.startsWith('/calendar'),
  },
  {
    to: '/wanted/missing',
    icon: icons.WARNING,
    title: 'Wanted',
    matches: (path: string) => path.startsWith('/wanted'),
  },
  {
    to: '/activity/queue',
    icon: icons.DOWNLOAD,
    title: 'Queue',
    matches: (path: string) => path.startsWith('/activity/queue'),
  },
];

function MobileNavigation() {
  const isSmallScreen = useAppDimension('isSmallScreen');
  const isSidebarVisible = useAppValue('isSidebarVisible');
  const { pathname } = useLocation();

  if (!isSmallScreen) {
    return null;
  }

  return (
    <nav className={styles.navigation} aria-label={translate('MainNavigation')}>
      {LINKS.map(({ to, icon, title, matches }) => {
        const isActive = matches(pathname);

        return (
          <Link
            key={to}
            className={classNames(styles.button, isActive && styles.active)}
            to={to}
            aria-current={isActive ? 'page' : undefined}
          >
            <Icon name={icon} size={21} />
            <span className={styles.label}>{translate(title)}</span>
          </Link>
        );
      })}

      <Link
        className={classNames(
          styles.button,
          !LINKS.some(({ matches }) => matches(pathname)) && styles.active
        )}
        aria-label={translate('More')}
        aria-expanded={isSidebarVisible}
        aria-haspopup="dialog"
        onPress={toggleIsSidebarVisible}
      >
        <Icon name={icons.NAVBAR_COLLAPSE} size={21} />
        <span className={styles.label}>{translate('More')}</span>
      </Link>
    </nav>
  );
}

export default MobileNavigation;
