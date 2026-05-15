import { useEffect } from 'react';

type PosShortcuts = {
  onF1Search?: () => void;
  onF4Payment?: () => void;
  onF9Park?: () => void;
};

export const usePosKeyboardShortcuts = ({ onF1Search, onF4Payment, onF9Park }: PosShortcuts) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      switch (e.key) {
        case 'F1':
          e.preventDefault();
          onF1Search?.();
          break;
        case 'F4':
          e.preventDefault();
          onF4Payment?.();
          break;
        case 'F9':
          e.preventDefault();
          onF9Park?.();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onF1Search, onF4Payment, onF9Park]);
};
