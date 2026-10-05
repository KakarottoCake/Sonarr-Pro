import {
  autoUpdate,
  flip,
  FloatingPortal,
  shift,
  useClick,
  useDismiss,
  useFloating,
  useInteractions,
} from '@floating-ui/react';
import React, {
  ReactElement,
  useCallback,
  useEffect,
  useId,
  useMemo,
  useState,
} from 'react';
import { useAppDimension } from 'App/appStore';
import BottomSheet from 'Components/Modal/BottomSheet';
import translate from 'Utilities/String/translate';
import MenuContext from './MenuContext';
import styles from './Menu.module.css';

interface MenuProps {
  className?: string;
  children: React.ReactNode;
  alignMenu?: 'left' | 'right';
  enforceMaxHeight?: boolean;
}

function Menu({
  className = styles.menu,
  children,
  alignMenu = 'left',
  enforceMaxHeight = true,
}: MenuProps) {
  const isSmallScreen = useAppDimension('isSmallScreen');
  const menuButtonId = useId();
  const [maxHeight, setMaxHeight] = useState(0);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const updateMaxHeight = useCallback(() => {
    const menuButton = document.getElementById(menuButtonId);

    if (!menuButton) {
      setMaxHeight(0);

      return;
    }

    const { bottom } = menuButton.getBoundingClientRect();
    const height = window.innerHeight - bottom;

    setMaxHeight(height);
  }, [menuButtonId]);

  const handleMenuButtonPress = useCallback(() => {
    setIsMenuOpen((isOpen) => !isOpen);
  }, []);

  const childrenArray = React.Children.toArray(children);
  const button = React.cloneElement(childrenArray[0] as ReactElement, {
    onPress: handleMenuButtonPress,
    'aria-expanded': isMenuOpen,
    'aria-haspopup': isSmallScreen ? 'dialog' : undefined,
  });

  const handleWindowResize = useCallback(() => {
    updateMaxHeight();
  }, [updateMaxHeight]);

  const handleWindowScroll = useCallback(() => {
    if (isMenuOpen) {
      updateMaxHeight();
    }
  }, [isMenuOpen, updateMaxHeight]);

  useEffect(() => {
    if (enforceMaxHeight) {
      updateMaxHeight();
    }
  }, [enforceMaxHeight, updateMaxHeight]);

  useEffect(() => {
    // Listen to resize events on the window and scroll events
    // on all elements to ensure the menu is the best size possible.

    if (!isMenuOpen) {
      return;
    }

    window.addEventListener('resize', handleWindowResize);
    window.addEventListener('scroll', handleWindowScroll, { capture: true });

    return () => {
      window.removeEventListener('resize', handleWindowResize);
      window.removeEventListener('scroll', handleWindowScroll, {
        capture: true,
      });
    };
  }, [isMenuOpen, handleWindowResize, handleWindowScroll]);

  const { refs, context, floatingStyles } = useFloating({
    middleware: [
      flip({
        crossAxis: false,
        mainAxis: true,
      }),
      // offset({ mainAxis: 10 }),
      shift(),
    ],
    open: isMenuOpen,
    placement: alignMenu === 'left' ? 'bottom-start' : 'bottom-end',
    whileElementsMounted: autoUpdate,
    onOpenChange: setIsMenuOpen,
  });

  const click = useClick(context);
  const dismiss = useDismiss(context, {
    enabled: !isSmallScreen,
    outsidePressEvent: 'click',
  });

  const { getReferenceProps, getFloatingProps } = useInteractions([
    click,
    dismiss,
  ]);

  const closeMenu = useCallback(() => {
    setIsMenuOpen(false);
  }, []);

  const menuContext = useMemo(() => ({ closeMenu }), [closeMenu]);

  return (
    <>
      <div
        ref={refs.setReference}
        {...(isSmallScreen ? {} : getReferenceProps())}
        id={menuButtonId}
        className={className}
      >
        {button}
      </div>

      {isSmallScreen ? (
        <BottomSheet
          isOpen={isMenuOpen}
          title={
            button.props['aria-label'] ?? button.props.text ?? translate('Menu')
          }
          onModalClose={closeMenu}
        >
          <MenuContext.Provider value={menuContext}>
            {React.cloneElement(childrenArray[1] as ReactElement, {
              isOpen: isMenuOpen,
            })}
          </MenuContext.Provider>
        </BottomSheet>
      ) : null}

      {!isSmallScreen && isMenuOpen ? (
        <FloatingPortal id="portal-root">
          <MenuContext.Provider value={menuContext}>
            {React.cloneElement(childrenArray[1] as ReactElement, {
              forwardedRef: refs.setFloating,
              style: {
                maxHeight: enforceMaxHeight ? maxHeight : undefined,
                ...floatingStyles,
              },
              isOpen: isMenuOpen,
              ...getFloatingProps(),
            })}
          </MenuContext.Provider>
        </FloatingPortal>
      ) : null}
    </>
  );
}

export default Menu;
