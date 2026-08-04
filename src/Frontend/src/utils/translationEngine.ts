/**
 * Zoho AI & Global Retail Standard Auto-Translation & Transliteration Engine
 * 
 * Commercial Supermarket NMT Architecture:
 * 1. Retail Quantity/Unit Tokenization (preserves and translates 1Kg -> 1 கிலோ, 500g -> 500 கிராம்).
 * 2. Grocery & Supermarket Commodity Dictionary for high-accuracy local retail terms.
 * 3. Fallback Online NMT/Phonetic API for brand names & regional variants.
 */

// ─── Retail Unit & Quantity Dictionary ────────────────────────────────────────

const TA_UNITS: Array<[RegExp, string]> = [
  [/\b(\d+(?:\.\d+)?)\s*(?:kg|kilo|kilos|k)\b/gi, '$1 கிலோ'],
  [/\b(\d+(?:\.\d+)?)\s*(?:grm|gram|grams|gm|g)\b/gi, '$1 கிராம்'],
  [/\b(\d+(?:\.\d+)?)\s*(?:ltr|liter|liters|l)\b/gi, '$1 லிட்டர்'],
  [/\b(\d+(?:\.\d+)?)\s*(?:ml|milliliter)\b/gi, '$1 மி.லி'],
  [/\b(\d+(?:\.\d+)?)\s*(?:pkt|packet|packets)\b/gi, '$1 பாக்கெட்'],
  [/\b(\d+(?:\.\d+)?)\s*(?:pc|pcs|piece|pieces)\b/gi, '$1 பீஸ்'],
  [/\b(\d+(?:\.\d+)?)\s*(?:box|boxes)\b/gi, '$1 பாட்டிலா/பாக்ஸ்'],
];

const HI_UNITS: Array<[RegExp, string]> = [
  [/\b(\d+(?:\.\d+)?)\s*(?:kg|kilo|kilos|k)\b/gi, '$1 किलो'],
  [/\b(\d+(?:\.\d+)?)\s*(?:grm|gram|grams|gm|g)\b/gi, '$1 ग्राम'],
  [/\b(\d+(?:\.\d+)?)\s*(?:ltr|liter|liters|l)\b/gi, '$1 लीटर'],
  [/\b(\d+(?:\.\d+)?)\s*(?:ml|milliliter)\b/gi, '$1 मिली'],
];

// ─── Supermarket Commodity Glossaries ──────────────────────────────────────────

const TAMIL_GROCERY_DICT: Record<string, string> = {
  // Spices & Condiments
  'SALT': 'உப்பு',
  'ROCK SALT': 'இந்துப்பு',
  'HIMALAYAN ROCK SALT': 'இமயமலை இந்துப்பு',
  'DRY GINGER': 'சுக்கு',
  'GINGER': 'இஞ்சி',
  'GARLIC': 'பூண்டு',
  'POONDU': 'பூண்டு',
  'PEPPER': 'மிளகு',
  'BLACK PEPPER': 'கருப்பு மிளகு',
  'TURMERIC': 'மஞ்சள்',
  'CHILLI': 'மிளகாய்',
  'RED CHILLI': 'சிவப்பு மிளகாய்',
  'MUSTARD': 'கடுகு',
  'CUMIN': 'சீரகம்',
  'SOMBU': 'பெருஞ்சீரகம் / சோம்பு',
  'SWEET SOMBU': 'இனிப்பு சோம்பு',
  'CORIANDER': 'கொத்தமல்லி',
  'CARDAMOM': 'ஏலக்காய்',
  'CLOVE': 'கிராம்பு',
  'CINNAMON': 'லவங்கப்பட்டை',
  'FENUGREEK': 'வெந்தயம்',
  'HING': 'பெருங்காயம்',
  'ASAFOETIDA': 'பெருங்காயம்',

  // Nuts, Dry Fruits & Seeds
  'PISTA': 'பிஸ்தா',
  'SALT PISTHA': 'உப்பு பிஸ்தா',
  'SALT PISTA': 'உப்பு பிஸ்தா',
  'CASHEW': 'முந்திரி',
  'ALMOND': 'பாதாம்',
  'DATES': 'பேரீச்சம்பழம்',
  'QURANIA DATES': 'குரேனியா பேரீச்சை',
  'KISMIS': 'கிஸ்மிஸ் உலர் திராட்சை',
  'RAISINS': 'உலர் திராட்சை',
  'SUN SEEDS': 'சூரியகாந்தி விதை',
  'SUNFLOWER SEEDS': 'சூரியகாந்தி விதை',

  // Grains, Pulses & Flour
  'RICE': 'அரிசி',
  'BASMATHI RICE': 'பாஸ்மதி அரிசி',
  'BASMATI RICE': 'பாஸ்மதி அரிசி',
  'RAW RICE': 'பச்சரிசி',
  'BOILED RICE': 'புழுங்கல் அரிசி',
  'WHEAT': 'கோதுமை',
  'MAIDA': 'மைதா',
  'RAVA': 'ரவை',
  'SEMAI': 'சேமியா',
  'DAL': 'பருப்பு',
  'TOOR DAL': 'துவரம் பருப்பு',
  'URAD DAL': 'உளுந்தம் பருப்பு',
  'MOONG DAL': 'பாசிப் பருப்பு',
  'CHANNA': 'கொண்டைக்கடலை',

  // Oils & Dairy
  'OIL': 'எண்ணெய்',
  'SUNFLOWER OIL': 'சூரியகாந்தி எண்ணெய்',
  'SESAME OIL': 'நல்லெண்ணெய்',
  'COCONUT OIL': 'தேங்காய் எண்ணெய்',
  'MILK': 'பால்',
  'CURD': 'தயிர்',
  'BUTTER': 'வெண்ணெய்',
  'GHEE': 'நெய்',
  'CHEESE': 'சீஸ்',

  // Beverages & Snacks
  'SUGAR': 'சர்க்கரை',
  'JAGGERY': 'வெல்லம்',
  'TEA': 'தேயிலை',
  'COFFEE': 'காபி',
  'ORANGE': 'ஆரஞ்சு',
  'PULPY ORANGE': 'பல்பி ஆரஞ்சு',
  'BOURBON': 'பர்பன் பிஸ்கட்',
  'PARLE BOURBON': 'பர்லே பர்பன்',
  'BISCUIT': 'பிஸ்கட்',
  'SOAP': 'சோப்',
  'SHAMPOO': 'ஷாம்பு',
  'PASTE': 'டூத்பேஸ்ட்',
};

const HINDI_GROCERY_DICT: Record<string, string> = {
  'SALT': 'नमक',
  'ROCK SALT': 'सेंधा नमक',
  'GINGER': 'अदरक',
  'DRY GINGER': 'सोंठ',
  'GARLIC': 'लहसुन',
  'SUGAR': 'चीनी',
  'RICE': 'चावल',
  'BASMATHI RICE': 'बासमती चावल',
  'OIL': 'तेल',
  'MILK': 'दूध',
  'CURD': 'दही',
  'GHEE': 'घी',
  'PISTA': 'पिस्ता',
  'CASHEW': 'काजू',
  'ALMOND': 'बादाम',
  'DATES': 'खजूर',
};

// ─── Auto-Translation Core Logic ──────────────────────────────────────────────

/**
 * Translates a supermarket product name into the target language.
 * Uses retail quantity tokenization + dictionary lookup + live NMT fallback.
 */
export async function translateProductName(
  englishName: string,
  targetLang: string = 'ta'
): Promise<string> {
  if (!englishName || !englishName.trim()) return '';

  const cleanName = englishName.trim();
  let resultName = cleanName;

  // Step 1: Pre-process & Extract Quantities/Units
  const unitPatterns = targetLang === 'hi' ? HI_UNITS : TA_UNITS;
  let translatedUnits: string[] = [];

  // Temporarily replace unit strings to protect them during commodity translation
  let textForDict = cleanName;
  unitPatterns.forEach(([pattern, replacement]) => {
    textForDict = textForDict.replace(pattern, (match) => {
      const translated = match.replace(pattern, replacement);
      translatedUnits.push(translated);
      return ' __UNIT__ ';
    });
  });

  // Step 2: Supermarket Dictionary Lookup (Case-Insensitive Exact & Token Matching)
  const dict = targetLang === 'hi' ? HINDI_GROCERY_DICT : TAMIL_GROCERY_DICT;
  const upperDictInput = textForDict.replace(/\s+/g, ' ').trim().toUpperCase();

  let commodityTranslated = '';

  if (dict[upperDictInput]) {
    commodityTranslated = dict[upperDictInput];
  } else {
    // Partial word token replacement from dictionary
    const tokens = upperDictInput.split(' ');
    const translatedTokens = tokens.map((token) => {
      if (token === '__UNIT__') return '__UNIT__';
      if (dict[token]) return dict[token];
      return token;
    });
    commodityTranslated = translatedTokens.join(' ');
  }

  // Re-insert translated units back into position
  let unitIndex = 0;
  resultName = commodityTranslated.replace(/__UNIT__/g, () => {
    return translatedUnits[unitIndex++] || '';
  });

  // Step 3: Check if there are un-translated English words remaining (e.g. unique brand names)
  const hasRemainingEnglishWords = /[a-zA-Z]{2,}/.test(resultName);

  if (hasRemainingEnglishWords) {
    try {
      // Use MyMemory NMT API for live, high-precision phonetic translation of remaining words
      const apiUrl = `https://api.mymemory.translated.net/get?q=${encodeURIComponent(cleanName)}&langpair=en|${targetLang}`;
      const resp = await fetch(apiUrl);
      if (resp.ok) {
        const data = await resp.json();
        if (data && data.responseData && data.responseData.translatedText) {
          const apiTrans = data.responseData.translatedText.trim();
          // Verify API didn't return an empty or error string
          if (apiTrans && apiTrans.toLowerCase() !== cleanName.toLowerCase()) {
            return apiTrans;
          }
        }
      }
    } catch {
      // Fallback gracefully to dictionary result if offline or API restricted
    }
  }

  return resultName.trim();
}
