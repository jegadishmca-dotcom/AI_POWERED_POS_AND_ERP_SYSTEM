import { useState, useEffect, useRef } from 'react';

export interface VoiceCommandResult {
  rawText: string;
  parsedQuery: string;
  quantity: number;
  isQuantityOnly: boolean;
}

export const parseVoiceInput = (text: string): VoiceCommandResult => {
  const cleanedText = text.trim();
  
  // 1. Standalone quantity commands
  // English: "5 quantity", "quantity 5", "5 qty", "change quantity to 5", "make it 5"
  // Tamil: "5 அளவு", "அளவு 5", "5 எண்ணிக்கை", "5 கிலோ"
  const qtyOnlyRegexes = [
    /^(?:change\s+)?(?:quantity|qty|அளவு|எண்ணிக்கை)\s*(?:to\s+)?(\d+(?:\.\d+)?)$/i,
    /^(\d+(?:\.\d+)?)\s*(?:quantity|qty|units?|pcs|pieces?|கிலோ|கிராம்|அளவு|எண்ணிக்கை|பீஸ்|பாக்கெட்|லிட்டர்)$/i,
    /^(?:make\s+it\s+)?(\d+(?:\.\d+)?)$/i
  ];

  for (const regex of qtyOnlyRegexes) {
    const match = cleanedText.match(regex);
    if (match) {
      return {
        rawText: cleanedText,
        parsedQuery: '',
        quantity: parseFloat(match[1]),
        isQuantityOnly: true
      };
    }
  }

  // 2. Prefix quantity: e.g., "2 kg Thoor Dhall", "2 கிலோ துவரம் பருப்பு", "5 quantity Cadbury Silk"
  const productWithPrefixQtyRegex = /^(\d+(?:\.\d+)?)\s*(quantity|qty|units?|pcs|pieces?|kg|g|grams?|packet|pkts?|ltrs?|l|liters?|கிலோ|கிராம்|அளவு|எண்ணிக்கை|பீஸ்|பாக்கெட்|லிட்டர்)?\s+(.*?)$/i;
  const prefixMatch = cleanedText.match(productWithPrefixQtyRegex);
  if (prefixMatch) {
    const qty = parseFloat(prefixMatch[1]);
    const query = prefixMatch[3].trim();
    return {
      rawText: cleanedText,
      parsedQuery: query,
      quantity: qty,
      isQuantityOnly: false
    };
  }

  // 3. Suffix quantity: e.g., "Thoor Dhall 2 kg", "துவரம் பருப்பு 2 கிலோ"
  const productWithQtyRegex = /^(.*?)\s+(\d+(?:\.\d+)?)\s*(quantity|qty|units?|pcs|pieces?|kg|g|grams?|packet|pkts?|ltrs?|l|liters?|கிலோ|கிராம்|அளவு|எண்ணிக்கை|பீஸ்|பாக்கெட்|லிட்டர்)?$/i;
  const match = cleanedText.match(productWithQtyRegex);
  if (match) {
    const query = match[1].trim();
    const qty = parseFloat(match[2]);
    return {
      rawText: cleanedText,
      parsedQuery: query,
      quantity: qty,
      isQuantityOnly: false
    };
  }

  // 4. Default: treat entire text as query, default quantity is 1
  return {
    rawText: cleanedText,
    parsedQuery: cleanedText,
    quantity: 1,
    isQuantityOnly: false
  };
};

interface UseVoiceBillingProps {
  onVoiceCommand: (result: VoiceCommandResult) => void;
  language?: string; // e.g. 'en-IN' or 'ta-IN'
}

export const useVoiceBilling = ({
  onVoiceCommand,
  language = 'en-IN'
}: UseVoiceBillingProps) => {
  const [isListening, setIsListening] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const recognitionRef = useRef<any>(null);
  const onVoiceCommandRef = useRef(onVoiceCommand);

  // Keep callback ref current to prevent hook re-triggering SpeechRecognition updates
  useEffect(() => {
    onVoiceCommandRef.current = onVoiceCommand;
  }, [onVoiceCommand]);

  useEffect(() => {
    const SpeechRecognition =
      (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    if (!SpeechRecognition) {
      setError('Speech recognition is not supported in this browser.');
      return;
    }

    const recognition = new SpeechRecognition();
    recognition.continuous = false;
    recognition.interimResults = false;
    recognition.lang = language;
    recognition.maxAlternatives = 1;

    recognition.onstart = () => {
      setIsListening(true);
      setError(null);
    };

    recognition.onend = () => {
      setIsListening(false);
    };

    recognition.onerror = (event: any) => {
      console.warn('Speech recognition error:', event.error);
      if (event.error === 'not-allowed') {
        setError('Microphone permission denied.');
      } else {
        setError(event.error);
      }
      setIsListening(false);
    };

    recognition.onresult = (event: any) => {
      const resultText = event.results[0][0].transcript;
      if (resultText) {
        const parsed = parseVoiceInput(resultText);
        onVoiceCommandRef.current(parsed);
      }
    };

    recognitionRef.current = recognition;

    return () => {
      if (recognitionRef.current) {
        try {
          recognitionRef.current.abort();
        } catch (e) {
          // ignore
        }
      }
    };
  }, [language]); // Recreate instance if language changes

  const startListening = () => {
    if (recognitionRef.current) {
      try {
        recognitionRef.current.start();
      } catch (err) {
        console.error('Failed to start SpeechRecognition:', err);
      }
    } else {
      setError('Speech recognition not initialized.');
    }
  };

  const stopListening = () => {
    if (recognitionRef.current) {
      try {
        recognitionRef.current.stop();
      } catch (err) {
        console.error('Failed to stop SpeechRecognition:', err);
      }
    }
  };

  const toggleListening = () => {
    if (isListening) {
      stopListening();
    } else {
      startListening();
    }
  };

  return {
    isListening,
    error,
    toggleListening,
    startListening,
    stopListening
  };
};
