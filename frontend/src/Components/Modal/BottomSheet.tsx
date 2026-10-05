import React, { ReactNode } from 'react';
import Modal from './Modal';
import ModalContent from './ModalContent';
import ModalHeader from './ModalHeader';
import styles from './BottomSheet.module.css';

interface BottomSheetProps {
  isOpen: boolean;
  title: ReactNode;
  children: ReactNode;
  onModalClose: () => void;
}

function BottomSheet({
  isOpen,
  title,
  children,
  onModalClose,
}: BottomSheetProps) {
  return (
    <Modal
      className={styles.sheet}
      backdropClassName={styles.backdrop}
      size="extraSmall"
      style={{ width: '100%', maxWidth: 560, maxHeight: '85dvh' }}
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <ModalContent className={styles.content} onModalClose={onModalClose}>
        <ModalHeader className={styles.header}>{title}</ModalHeader>
        <div className={styles.body}>{children}</div>
      </ModalContent>
    </Modal>
  );
}

export default BottomSheet;
