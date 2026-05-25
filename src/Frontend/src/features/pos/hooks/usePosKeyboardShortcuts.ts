import { useEffect } from 'react';

type PosShortcuts = {
  onF1Search?: () => void;
  onF2Product?: () => void;
    onF11Payment?: () => void;
  onF9Park?: () => void;
  onF10Reprint?: () => void;
};

export const usePosKeyboardShortcuts = ({ onF1Search, onF2Product, onF11Payment, onF9Park, onF10Reprint }: PosShortcuts) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      switch (e.key) {
        case 'F1':
          e.preventDefault();
          onF1Search?.();
          break;
        case 'F2':
          e.preventDefault();
          onF2Product?.();
          break;
        case 'F11':
          e.preventDefault();
          onF11Payment?.();
          break;
        case 'F9':
          e.preventDefault();
          onF9Park?.();
          break;
        case 'F10':
          e.preventDefault();
          onF10Reprint?.();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onF1Search, onF2Product, onF4Payment, onF9Park, onF10Reprint]);
};
