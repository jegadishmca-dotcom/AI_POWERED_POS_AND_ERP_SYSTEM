import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award, Tag, Trash2, PlusCircle, MinusCircle, Hand, ShieldAlert, Printer, Clock, Maximize, Minimize, Mic, MicOff, Unlock, RotateCcw } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';
import { PaymentModal } from './PaymentModal';
import { searchProducts } from '../../catalog/api/catalog.api';
import { searchCustomers, registerCustomer } from '../../crm/api/crm.api';
import { createInvoice, closeShift, getZReport, getProductBatches, getCurrentSession, openSession, calculateCart, getActiveBusinessDate, openBusinessDate, holdInvoice } from '../api/pos.api';
import { getPosPermissions } from '../../settings/api/settings.api';
import { printReceipt } from '../utils/printReceipt';
import { printZReport } from '../utils/printZReport';
import { useBarcodeScanner } from '../hooks/useBarcodeScanner';
import { usePosKeyboardShortcuts } from '../hooks/usePosKeyboardShortcuts';
import { useVoiceBilling } from '../hooks/useVoiceBilling';
import { HoldResumeModal } from './modals/HoldResumeModal';
import { ManagerPinModal } from './modals/ManagerPinModal';
import { ReprintModal } from './modals/ReprintModal';
import { OpenShiftModal } from './modals/OpenShiftModal';
import { CloseShiftModal } from './modals/CloseShiftModal';
import { SalesReturnModal } from './modals/SalesReturnModal';
import { CancelInvoiceModal } from './modals/CancelInvoiceModal';
import { CANCELLATION_ALLOWED_ROLES } from '../constants/roles';
import { posDb } from '../db/pos.db';
import { useAuthStore } from '../../auth/store/auth.store';
import { syncInvoices } from '../api/pos.sync';
import { safeRandomUUID } from '../../../utils/uuid';
import { api } from '../../../utils/api';

export const PosTerminal = () => {
  const [customer, setCustomer] = useState<any>(null);
  const [customerQuery, setCustomerQuery] = useState('');
  const [customerSearchResults, setCustomerSearchResults] = useState<any[]>([]);
  const [showCustomerDropdown, setShowCustomerDropdown] = useState(false);
  const [focusedCustomerIndex, setFocusedCustomerIndex] = useState(-1);
  const customerDropdownRef = useRef<HTMLDivElement>(null);
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  const [promoCode, setPromoCode] = useState('');
  const [isPaymentModalOpen, setPaymentModalOpen] = useState(false);
  const [completedInvoice, setCompletedInvoice] = useState<any>(null);
  
  const [isProcessing, setIsProcessing] = useState(false);
  
  // Modals & Hooks State
  const [isHoldModalOpen, setHoldModalOpen] = useState(false);
  const [isReprintModalOpen, setReprintModalOpen] = useState(false);
  const [isManagerModalOpen, setManagerModalOpen] = useState(false);
  const [isReturnModalOpen, setReturnModalOpen] = useState(false);
  const [isCancelModalOpen, setCancelModalOpen] = useState(false);
  const [managerAction, setManagerAction] = useState<any>(null);
  const [selectedCartIndex, setSelectedCartIndex] = useState<number>(-1);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const requestPOSFullscreen = async () => {
    try {
      if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
        setIsFullscreen(true);
      }
    } catch (err) {
      console.log('Fullscreen request requires user interaction', err);
    }
  };

  const toggleFullscreen = () => {
    if (document.fullscreenElement) {
      document.exitFullscreen().catch(() => {});
    } else {
      document.documentElement.requestFullscreen().catch(() => {});
    }
  };

  useEffect(() => {
    requestPOSFullscreen();

    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    const handleFirstInteraction = () => {
      if (!document.fullscreenElement) {
        requestPOSFullscreen();
      }
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    window.addEventListener('click', handleFirstInteraction, { once: true });
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      window.removeEventListener('click', handleFirstInteraction);
    };
  }, []);

  const customerInputRef = useRef<HTMLInputElement>(null);
  const productInputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const cartContainerRef = useRef<HTMLDivElement>(null);
  const activeItemRowRef = useRef<HTMLTableRowElement>(null);
  const isVoiceSearchingRef = useRef(false);
  const itemQtyRefs = useRef<{ [key: string]: HTMLInputElement | null }>({});

  const [toastNotification, setToastNotification] = useState<{ message: string; type: 'error' | 'success' | 'info' } | null>(null);

  const showToastNotification = useCallback((message: string, type: 'error' | 'success' | 'info' = 'error') => {
    setToastNotification({ message, type });
    requestPOSFullscreen();
    setTimeout(() => {
      setToastNotification((prev) => (prev?.message === message ? null : prev));
    }, 3500);
  }, []);



  // Shift Management State
  const [activeSession, setActiveSession] = useState<any>(null);
  const [isOpenShiftModalOpen, setOpenShiftModalOpen] = useState(false);
  const [isCloseShiftModalOpen, setCloseShiftModalOpen] = useState(false);
  const { user } = useAuthStore();
  const isAuthorizedToCancel = !!(user?.role && CANCELLATION_ALLOWED_ROLES.includes(user.role));
  const terminalId = localStorage.getItem('pos_terminal_id') || '';
  const cashierId = user?.id || '';

  // Feature 2: Cashier line-item delete toggle — loaded from backend/localStorage cache
  const [cashierCanDeleteLineItem, setCashierCanDeleteLineItem] = useState<boolean>(() => {
    try {
      const cached = localStorage.getItem('pos_permissions_cache');
      if (cached) {
        const parsed = JSON.parse(cached);
        if (typeof parsed.cashierCanDeleteLineItem === 'boolean') {
          return parsed.cashierCanDeleteLineItem;
        }
      }
    } catch {}
    return false;
  });

  const refreshPosPermissions = useCallback(async () => {
    try {
      const posPerms = await getPosPermissions();
      if (posPerms && typeof posPerms.cashierCanDeleteLineItem === 'boolean') {
        setCashierCanDeleteLineItem(posPerms.cashierCanDeleteLineItem);
        localStorage.setItem('pos_permissions_cache', JSON.stringify(posPerms));
      }
    } catch (err) {
      console.warn('Could not load POS permissions from API', err);
    }
  }, []);

  // Independent Unconditional POS Permissions Sync Engine
  useEffect(() => {
    refreshPosPermissions();

    const handleFocus = () => refreshPosPermissions();
    const handleStorage = (e: StorageEvent) => {
      if (e.key === 'pos_permissions_cache' && e.newValue) {
        try {
          const parsed = JSON.parse(e.newValue);
          if (typeof parsed.cashierCanDeleteLineItem === 'boolean') {
            setCashierCanDeleteLineItem(parsed.cashierCanDeleteLineItem);
          }
        } catch {}
      }
    };

    window.addEventListener('focus', handleFocus);
    window.addEventListener('storage', handleStorage);
    const interval = setInterval(refreshPosPermissions, 10000);

    return () => {
      window.removeEventListener('focus', handleFocus);
      window.removeEventListener('storage', handleStorage);
      clearInterval(interval);
    };
  }, [refreshPosPermissions]);

  // Business Date State
  const [isBusinessDateOpen, setBusinessDateOpen] = useState(true);
  const [activeBusinessDate, setActiveBusinessDate] = useState<string | null>(null);
  const [dateLoading, setDateLoading] = useState(true);
  const [openingDateSubmitting, setOpeningDateSubmitting] = useState(false);
  const [selectedOpenDate, setSelectedOpenDate] = useState(() => {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  });

  // Background Sync for Offline Invoices
  useEffect(() => {
    // Sync immediately on mount
    syncInvoices();
    // Then sync every 15 seconds
    const interval = setInterval(() => {
      syncInvoices();
    }, 15000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    // Focus barcode scanner input on mount
    productInputRef.current?.focus();
    
    // Only check when cashierId is loaded and is not the fallback GUID
    if (!user?.id || cashierId === '00000000-0000-0000-0000-000000000001') {
      return;
    }

    const checkBusinessDate = async () => {
      try {
        setDateLoading(true);
        const activeState = await getActiveBusinessDate();
        setBusinessDateOpen(activeState.isOpen);
        setActiveBusinessDate(activeState.businessDate);
        
        if (activeState.isOpen) {
          // Check for active shift only if business day is open
          const sessionData = await getCurrentSession(terminalId, cashierId);
          if (sessionData && sessionData.status === 'OPEN') {
            setActiveSession(sessionData);
            setOpenShiftModalOpen(false);
          } else {
            setOpenShiftModalOpen(true);
          }
        }
      } catch (err) {
        console.error('Failed to fetch business date status', err);
      } finally {
        setDateLoading(false);
      }
    };
    checkBusinessDate();
  }, [cashierId, terminalId, user?.id]);
  
  const handleOpenShift = async (openingCash: number) => {
    try {
      const payload = {
        terminalId,
        cashierId,
        openingFloatCash: openingCash
      };
      const sessionId = await openSession(payload);
      setActiveSession({ id: sessionId, terminalId, cashierId, openingFloatCash: openingCash, status: 'OPEN' });
      setOpenShiftModalOpen(false);
    } catch (err) {
      console.error('Error opening shift', err);
      alert('Error opening shift');
    }
  };

  const handleCloseShift = async (closingCash: number) => {
    try {
      if (!activeSession?.id) throw new Error("No active session found.");
      await closeShift({
        sessionId: activeSession.id,
        actualClosingCash: closingCash
      });
      
      const today = new Date().toISOString().split('T')[0];
      const report = await getZReport(terminalId, today, cashierId, activeSession.id);
      printZReport(report, activeSession?.openingFloatCash || 0, closingCash, user?.fullName || 'Cashier');
      
      setActiveSession(null);
      setCloseShiftModalOpen(false);
      setOpenShiftModalOpen(true);
    } catch (err: any) {
      console.error('Error closing shift:', err);
      const errMsg = err.response?.data ? (typeof err.response.data === 'string' ? err.response.data : JSON.stringify(err.response.data)) : err.message;
      alert(`Failed to close shift. Reason: ${errMsg}`);
    }
  };

  // Product Search State
  const [productQuery, setProductQuery] = useState('');
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [showProductDropdown, setShowProductDropdown] = useState(false);
  const [focusedProductIndex, setFocusedProductIndex] = useState(-1);

  // Voice Billing States
  const [voiceLanguage, setVoiceLanguage] = useState<'en-IN' | 'ta-IN'>(() => {
    return (localStorage.getItem('pos_voice_language') as 'en-IN' | 'ta-IN') || 'en-IN';
  });
  const [voiceQuantity, setVoiceQuantity] = useState<number | null>(null);
  const [voiceStatus, setVoiceStatus] = useState<string | null>(null);
  const [voiceStatusType, setVoiceStatusType] = useState<'success' | 'error' | 'info' | 'listening'>('info');

  const handleVoiceLanguageToggle = () => {
    const nextLang = voiceLanguage === 'en-IN' ? 'ta-IN' : 'en-IN';
    setVoiceLanguage(nextLang);
    localStorage.setItem('pos_voice_language', nextLang);
    setVoiceStatus(nextLang === 'ta-IN' ? 'மொழி மாற்றப்பட்டது: தமிழ்' : 'Language changed: English (India)');
    setVoiceStatusType('info');
  };

  const handleVoiceCommand = async (result: any) => {
    isVoiceSearchingRef.current = true;
    try {
      setProductQuery(result.rawText);

      if (result.isQuantityOnly) {
        if (cart.items.length > 0) {
          const lastItem = cart.items[cart.items.length - 1];
          updateItemQtyExact(lastItem.productId, result.quantity);
          setVoiceStatus(voiceLanguage === 'ta-IN'
            ? `அளவு மாற்றப்பட்டது: ${lastItem.name} x${result.quantity}`
            : `Updated quantity: ${lastItem.name} to ${result.quantity}`
          );
          setVoiceStatusType('success');
          setProductQuery('');
        } else {
          setVoiceStatus(voiceLanguage === 'ta-IN'
            ? 'வண்டியில் பொருட்கள் இல்லை. அளவை மாற்ற முடியாது.'
            : 'Cart is empty. Cannot change quantity.'
          );
          setVoiceStatusType('error');
        }
        return;
      }

      try {
        setVoiceStatus(voiceLanguage === 'ta-IN' ? 'தேடுகிறது...' : 'Searching catalog...');
        setVoiceStatusType('info');

        const results = await searchProducts(result.parsedQuery);

        if (results.length === 1) {
          addProductToCart(results[0], result.quantity);
          setVoiceStatus(voiceLanguage === 'ta-IN'
            ? `சேர்க்கப்பட்டது: ${results[0].tamilName || results[0].name} x${result.quantity}`
            : `Added: ${results[0].name} x${result.quantity}`
          );
          setVoiceStatusType('success');
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
        } else if (results.length > 1) {
          setSearchResults(results);
          setShowProductDropdown(true);
          setFocusedProductIndex(0);
          setVoiceQuantity(result.quantity);
          setVoiceStatus(voiceLanguage === 'ta-IN'
            ? `${results.length} பொருட்கள் கண்டறியப்பட்டன. ஒன்றை தேர்ந்தெடுக்கவும்.`
            : `Found ${results.length} matches. Please select one.`
          );
          setVoiceStatusType('info');
        } else {
          if (result.parsedQuery !== result.rawText) {
            const rawResults = await searchProducts(result.rawText);
            if (rawResults.length === 1) {
              addProductToCart(rawResults[0], result.quantity);
              setVoiceStatus(voiceLanguage === 'ta-IN'
                ? `சேர்க்கப்பட்டது: ${rawResults[0].tamilName || rawResults[0].name} x${result.quantity}`
                : `Added: ${rawResults[0].name} x${result.quantity}`
              );
              setVoiceStatusType('success');
              setProductQuery('');
              setSearchResults([]);
              setShowProductDropdown(false);
              setFocusedProductIndex(-1);
              return;
            } else if (rawResults.length > 1) {
              setSearchResults(rawResults);
              setShowProductDropdown(true);
              setFocusedProductIndex(0);
              setVoiceQuantity(result.quantity);
              setVoiceStatus(voiceLanguage === 'ta-IN'
                ? `${rawResults.length} பொருட்கள் கண்டறியப்பட்டன. ஒன்றை தேர்ந்தெடுக்கவும்.`
                : `Found ${rawResults.length} matches. Please select one.`
              );
              setVoiceStatusType('info');
              return;
            }
          }

          setVoiceStatus(voiceLanguage === 'ta-IN'
            ? `"${result.parsedQuery}" என்ற பெயரில் பொருள் இல்லை`
            : `No product found matching "${result.parsedQuery}"`
          );
          setVoiceStatusType('error');
        }
      } catch (err) {
        console.error('Voice search failed:', err);
        setVoiceStatus(voiceLanguage === 'ta-IN' ? 'தேடலில் பிழை ஏற்பட்டது.' : 'Voice search error.');
        setVoiceStatusType('error');
      }
    } finally {
      isVoiceSearchingRef.current = false;
      productInputRef.current?.focus();
    }
  };

  const { isListening, error: voiceError, toggleListening } = useVoiceBilling({
    onVoiceCommand: handleVoiceCommand,
    language: voiceLanguage
  });

  useEffect(() => {
    if (voiceError) {
      setVoiceStatus(voiceError);
      setVoiceStatusType('error');
    }
  }, [voiceError]);

  useEffect(() => {
    if (isListening) {
      setVoiceStatus(voiceLanguage === 'ta-IN' ? 'கேட்டுக்கொண்டிருக்கிறது... பேசவும்.' : 'Listening... Speak now.');
      setVoiceStatusType('listening');
    } else {
      setVoiceStatus(prev => {
        if (prev === 'Listening... Speak now.' || prev === 'கேட்டுக்கொண்டிருக்கிறது... பேசவும்.') {
          return null;
        }
        return prev;
      });
    }
  }, [isListening, voiceLanguage]);

  useEffect(() => {
    if (voiceStatus && voiceStatusType !== 'listening') {
      const timer = setTimeout(() => {
        if (isListening) {
          setVoiceStatus(voiceLanguage === 'ta-IN' ? 'கேட்டுக்கொண்டிருக்கிறது... பேசவும்.' : 'Listening... Speak now.');
          setVoiceStatusType('listening');
        } else {
          setVoiceStatus(null);
        }
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [voiceStatus, voiceStatusType, isListening, voiceLanguage]);

  useEffect(() => {
    const handleVoiceShortcut = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key.toLowerCase() === 'm') {
        e.preventDefault();
        toggleListening();
      }
    };
    window.addEventListener('keydown', handleVoiceShortcut);
    return () => window.removeEventListener('keydown', handleVoiceShortcut);
  }, [toggleListening]);

  // Debounced instant search trigger on text change
  useEffect(() => {
    if (isVoiceSearchingRef.current) return;

    const val = productQuery.trim();
    if (!val) {
      setSearchResults([]);
      setShowProductDropdown(false);
      setFocusedProductIndex(-1);
      return;
    }

    // Skip triggering instant search if the query is a potential barcode (long digits/letters)
    // to let the Enter key keypress/scanner handle it uniquely without popping the dropdown.
    // If it's a normal short text query (2 or more characters), search instantly.
    if (val.length < 2) return;

    let active = true;

    const delayDebounceFn = setTimeout(async () => {
      try {
        const results = await searchProducts(val);
        if (!active) return;

        if (results.length > 0) {
          setSearchResults(results);
          setShowProductDropdown(true);
          // If the dropdown was not open, set focus to first item
          setFocusedProductIndex(prev => prev >= 0 && prev < results.length ? prev : 0);
        } else {
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
        }
      } catch (err) {
        console.error('Error searching products instantly:', err);
      }
    }, 200); // 200ms debounce delay for instant responsiveness without performance lag

    return () => {
      active = false;
      clearTimeout(delayDebounceFn);
    };
  }, [productQuery]);

  // Helper: scroll a specific dropdown item into view.
  // Uses data-idx attribute + native scrollIntoView which is guaranteed by
  // the browser to scroll the nearest scrollable ancestor (our overflow-y-auto
  // container) just enough to reveal the element — no manual math needed.
  const scrollDropdownToIndex = (index: number) => {
    if (!dropdownRef.current) return;
    if (index <= 0) {
      dropdownRef.current.scrollTop = 0;
      return;
    }
    const el = dropdownRef.current.querySelector(`[data-idx="${index}"]`) as HTMLElement | null;
    if (el) el.scrollIntoView({ block: 'nearest', behavior: 'auto' });
  };

  // Debounced instant customer search trigger on text change
  useEffect(() => {
    const val = customerQuery.trim();
    if (!val || val.length < 2) {
      setCustomerSearchResults([]);
      setShowCustomerDropdown(false);
      setFocusedCustomerIndex(-1);
      return;
    }

    let active = true;
    const delayDebounceFn = setTimeout(async () => {
      try {
        const results = await searchCustomers(val);
        if (!active) return;

        if (results && results.length > 0) {
          setCustomerSearchResults(results);
          setShowCustomerDropdown(true);
          setFocusedCustomerIndex(0); // auto-focus first matching customer
        } else {
          setCustomerSearchResults([]);
          setShowCustomerDropdown(false);
          setFocusedCustomerIndex(-1);
        }
      } catch (err) {
        console.error('Error searching customers instantly:', err);
      }
    }, 200);

    return () => {
      active = false;
      clearTimeout(delayDebounceFn);
    };
  }, [customerQuery]);

  const scrollCustomerDropdownToIndex = (index: number) => {
    if (!customerDropdownRef.current) return;
    if (index <= 0) {
      customerDropdownRef.current.scrollTop = 0;
      return;
    }
    const el = customerDropdownRef.current.querySelector(`[data-cust-idx="${index}"]`) as HTMLElement | null;
    if (el) el.scrollIntoView({ block: 'nearest', behavior: 'auto' });
  };

  // Dynamic Cart State initializing empty
  const [cart, setCart] = useState<any>({
    items: [],
    subtotal: 0,
    totalDiscount: 0,
    taxTotal: 0,
    finalTotal: 0,
    appliedOfferNames: []
  });

  const focusItemQtyInput = useCallback((targetProductId?: string) => {
    setTimeout(() => {
      setCart((currentCart: any) => {
        const targetId = targetProductId || (
          selectedCartIndex >= 0 && selectedCartIndex < currentCart.items.length
            ? currentCart.items[selectedCartIndex]?.productId
            : currentCart.items[currentCart.items.length - 1]?.productId
        );

        if (targetId) {
          const inputEl = itemQtyRefs.current[targetId];
          if (inputEl) {
            inputEl.focus();
            inputEl.select();
          }
        }
        return currentCart;
      });
    }, 60);
  }, [selectedCartIndex]);
  const [suppressOffers, setSuppressOffers] = useState(false);
  const [pointsRedeemed, setPointsRedeemed] = useState<number>(0);
  const pointsDiscount = pointsRedeemed > 0 ? (pointsRedeemed / 10) : 0;
  const finalBillTotal = Math.max(0, cart.finalTotal - pointsDiscount);

  const recalculateCart = useCallback(async (items: any[], overrideCustomerId?: string | null) => {
    if (items.length === 0) {
      setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
      return;
    }

    // --- INSTANT OPTIMISTIC LOCAL CALCULATION ---
    let localFinalTotal = 0;
    let localTaxTotal = 0;
    const mappedItems = items.map((item: any) => {
      const qty = item.qty;
      const unitPrice = item.unitPrice;
      const lineTotal = qty * unitPrice; // MRP is tax-inclusive, so line total is unitPrice * qty
      const itemTaxRate = (item.cgstRate || 0) + (item.sgstRate || 0) + (item.cessRate || 0);
      const taxable = lineTotal / (1 + itemTaxRate / 100);
      const cgstAmount = taxable * ((item.cgstRate || 0) / 100);
      const sgstAmount = taxable * ((item.sgstRate || 0) / 100);
      const cessAmount = taxable * ((item.cessRate || 0) / 100);

      localFinalTotal += lineTotal;
      localTaxTotal += (cgstAmount + sgstAmount + cessAmount);

      return {
        ...item,
        finalLineTotal: lineTotal,
        discountAmount: item.discountAmount || 0,
        appliedOfferName: item.appliedOfferName || null,
        cgstAmount: +cgstAmount.toFixed(2),
        sgstAmount: +sgstAmount.toFixed(2),
        cessAmount: +cessAmount.toFixed(2)
      };
    });

    let localSubtotal = localFinalTotal - localTaxTotal;

    setCart({
      items: mappedItems,
      subtotal: localSubtotal,
      totalDiscount: 0, 
      taxTotal: localTaxTotal,
      finalTotal: localFinalTotal,
      appliedOfferNames: [] 
    });

    // --- BACKGROUND SERVER CALCULATION FOR PROMOS ---
    try {
      const payload = {
        items: items.map(i => ({ productId: i.productId, quantity: i.qty === '' ? 0 : Number(i.qty) })),
        promoCode: promoCode,
        customerId: overrideCustomerId !== undefined ? (overrideCustomerId || undefined) : customer?.id,
        suppressOffers: suppressOffers
      };

      const data = await calculateCart(payload);
      
      setCart((prevCart: any) => {
        const evaluatedItems = prevCart.items.map((origItem: any) => {
          const calcItem = data.items.find((i: any) => i.productId === origItem.productId);
          if (!calcItem) return origItem;
          return {
            ...origItem,
            discountAmount: calcItem.discountAmount,
            finalLineTotal: calcItem.finalLineTotal,
            appliedOfferName: calcItem.appliedOfferName,
            cgstRate: calcItem.cgstRate,
            sgstRate: calcItem.sgstRate,
            cessRate: calcItem.cessRate,
            cgstAmount: calcItem.cgstAmount,
            sgstAmount: calcItem.sgstAmount,
            cessAmount: calcItem.cessAmount
          };
        });

        return {
          items: evaluatedItems,
          subtotal: data.subTotal,
          totalDiscount: data.totalDiscount,
          taxTotal: data.taxTotal,
          finalTotal: data.finalTotal,
          appliedOfferNames: data.appliedOfferNames
        };
      });

    } catch (err) {
      console.warn('Backend calculation failed, keeping basic offline calculation', err);
    }
  }, [promoCode, customer]);

  // Evaluate whenever promo code or customer changes, only if cart is not empty
  useEffect(() => {
    if (cart.items.length > 0) {
      recalculateCart(cart.items);
    }
  }, [recalculateCart, suppressOffers]);

  // If suppressOffers changes, recalculate cart
  useEffect(() => {
    recalculateCart(cart.items);
  }, [suppressOffers]);

  // Auto-scroll cart container to active/last item row
  useEffect(() => {
    if (cart && cart.items && cart.items.length > 0) {
      activeItemRowRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, [cart?.items?.length, selectedCartIndex]);

  const updateItemBatch = (productId: string, batchId: string) => {
    const updatedItems = cart.items.map((item: any) => {
      if (item.productId === productId) {
        return { ...item, batchId: batchId };
      }
      return item;
    });
    setCart((prev: any) => ({
      ...prev,
      items: updatedItems
    }));
  };

  const [batchModalData, setBatchModalData] = useState<{ product: any; batches: any[]; overrideQty?: number } | null>(null);
  const [selectedBatchIndex, setSelectedBatchIndex] = useState(0);

  useEffect(() => {
    if (!batchModalData || !batchModalData.batches || batchModalData.batches.length === 0) return;

    const handleBatchKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        e.stopPropagation();
        setSelectedBatchIndex((prev) => (prev + 1) % batchModalData.batches.length);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        e.stopPropagation();
        setSelectedBatchIndex((prev) => (prev - 1 + batchModalData.batches.length) % batchModalData.batches.length);
      } else if (e.key === 'Enter' || e.key === ' ' || e.code === 'Space') {
        e.preventDefault();
        e.stopPropagation();
        const chosenBatch = batchModalData.batches[selectedBatchIndex];
        if (chosenBatch) {
          addProductToCart(batchModalData.product, batchModalData.overrideQty, chosenBatch);
          setBatchModalData(null);
        }
      } else if (e.key === 'Escape') {
        e.preventDefault();
        e.stopPropagation();
        setBatchModalData(null);
      }
    };

    window.addEventListener('keydown', handleBatchKeyDown, true);
    return () => window.removeEventListener('keydown', handleBatchKeyDown, true);
  }, [batchModalData, selectedBatchIndex]);

  // Global Keyboard Listener for '+' and '-' Quantity adjustments on cart items
  useEffect(() => {
    const handleCartQtyShortcut = (e: KeyboardEvent) => {
      // Ignore if user is currently typing in search input with non-empty text
      const activeEl = document.activeElement;
      const isEditingSearchInput = activeEl && (
        (activeEl.tagName === 'INPUT' && (activeEl as HTMLInputElement).type === 'text' && (activeEl as HTMLInputElement).value.trim().length > 0) ||
        activeEl.tagName === 'TEXTAREA'
      );

      if (isEditingSearchInput) return;
      if (cart.items.length === 0) return;

      const isPlus = e.key === '+' || e.key === '=' || e.code === 'NumpadAdd';
      const isMinus = e.key === '-' || e.key === '_' || e.code === 'NumpadSubtract';

      if (isPlus || isMinus) {
        // Prevent default input behavior when adjusting cart qty
        if (activeEl && activeEl.tagName === 'INPUT' && (activeEl as HTMLInputElement).type === 'text') {
          (activeEl as HTMLInputElement).value = '';
          setProductQuery('');
        }
        e.preventDefault();
        
        const targetIdx = (selectedCartIndex >= 0 && selectedCartIndex < cart.items.length) 
          ? selectedCartIndex 
          : cart.items.length - 1;
        const targetItem = cart.items[targetIdx];
        if (targetItem) {
          updateItemQty(targetItem.productId, isPlus ? 1 : -1);
        }
      }
    };

    window.addEventListener('keydown', handleCartQtyShortcut);
    return () => window.removeEventListener('keydown', handleCartQtyShortcut);
  }, [cart.items, selectedCartIndex]);

  const addProductToCart = async (product: any, overrideQty?: number, selectedBatch?: any) => {
    if (selectedBatch) {
      addSingleProductToCartDirect(product, selectedBatch, overrideQty);
      return;
    }

    let fetchedBatches: any[] = [];
    try {
      fetchedBatches = await getProductBatches(product.id);
    } catch (err) {
      console.warn('Failed to fetch batches', err);
    }

    // Feature 1 (Price Change Module): Filter out price-change batches (AvailableQuantity = 0)
    // from the POS batch selection popup. These batches exist purely to record a price-change
    // event and have no physical stock. The product's current MRP/SellingPrice (already updated
    // on the Product master record) is used for pricing — not the batch.
    const stockBatches = fetchedBatches.filter((b: any) =>
      b.availableQuantity !== undefined
        ? b.availableQuantity > 0
        : b.currentStock !== undefined
          ? b.currentStock > 0
          : true // if qty field name unknown, include (safe fallback)
    );

    if (stockBatches.length >= 2) {
      setSelectedBatchIndex(0);
      setBatchModalData({ product, batches: stockBatches, overrideQty });
      return;
    }

    const chosenBatch = stockBatches.length === 1 ? stockBatches[0] : null;
    addSingleProductToCartDirect(product, chosenBatch, overrideQty, stockBatches);
  };

  const addSingleProductToCartDirect = (product: any, batch?: any, overrideQty?: number, allBatches?: any[]) => {
    const sellingPrice = batch && batch.sellingPrice ? batch.sellingPrice : product.sellingPrice;
    const mrp = batch && batch.mrp ? batch.mrp : (product.mrp || sellingPrice);

    const existingIndex = cart.items.findIndex((item: any) => item.productId === product.id && (batch ? item.batchId === batch.id : true));
    const qtyToAdd = overrideQty !== undefined ? overrideQty : 1;

    if (existingIndex >= 0) {
      setSelectedCartIndex(existingIndex);
      const updatedItems = cart.items.map((item: any) =>
        item.productId === product.id && (batch ? item.batchId === batch.id : true)
          ? { ...item, qty: item.qty + qtyToAdd, lineTotal: (item.qty + qtyToAdd) * item.unitPrice }
          : item
      );
      recalculateCart(updatedItems);
    } else {
      setSelectedCartIndex(cart.items.length);
      const newItem = {
        id: safeRandomUUID(),
        productId: product.id,
        name: product.name,
        nameTamil: product.tamilName || product.nameTamil || product.secondaryName || null,
        mrp: mrp,
        qty: qtyToAdd,
        unitPrice: sellingPrice,
        lineTotal: sellingPrice * qtyToAdd,
        discountAmount: 0,
        finalLineTotal: sellingPrice * qtyToAdd,
        appliedOfferName: null,
        cgstRate: product.cgstRate || 0,
        sgstRate: product.sgstRate || 0,
        cessRate: product.cessRate || 0,
        isWeighable: !!(
          product.isWeighable ||
          (product.unitOfMeasure && /kg|gram|gm|ltr|liter/i.test(product.unitOfMeasure)) ||
          (product.uomName && /kg|gram|gm|ltr|liter/i.test(product.uomName)) ||
          (product.uom && /kg|gram|gm|ltr|liter/i.test(product.uom))
        ),
        batches: allBatches || (batch ? [batch] : []),
        batchId: batch ? batch.id : undefined
      };

      const isWeighableItem = !!(
        product.isWeighable ||
        (product.unitOfMeasure && /kg|gram|gm|ltr|liter/i.test(product.unitOfMeasure)) ||
        (product.uomName && /kg|gram|gm|ltr|liter/i.test(product.uomName)) ||
        (product.uom && /kg|gram|gm|ltr|liter/i.test(product.uom))
      );

      const updatedItems = [...cart.items, newItem];
      recalculateCart(updatedItems);

      // Per user request: Focus remains on the "Scan Barcode or Type Product Name" input by default.
      // Cashier presses TAB key when they want to edit the Qty of the last item.
      setTimeout(() => {
        productInputRef.current?.focus();
      }, 50);
    }
  };

  const updateItemQtyExact = (productId: string, newQty: number | string) => {
    const updatedItems = cart.items.map((item: any) => {
      if (item.productId === productId) {
        const qtyNum = newQty === '' ? 0 : Number(newQty);
        return { ...item, qty: newQty, lineTotal: qtyNum * item.unitPrice };
      }
      return item;
    });
    recalculateCart(updatedItems);
  };

  const updateItemQty = (productId: string, delta: number) => {
    const targetItem = cart.items.find((item: any) => item.productId === productId);
    if (!targetItem) return;

    if (targetItem.qty + delta <= 0) {
      handleDeleteCartItem(targetItem);
      return;
    }

    const updatedItems = cart.items.map((item: any) => {
      if (item.productId === productId) {
        const newQty = item.qty + delta;
        return { ...item, qty: newQty, lineTotal: newQty * item.unitPrice };
      }
      return item;
    });
    recalculateCart(updatedItems);
  };

  const removeItem = (productId: string) => {
    const updatedItems = cart.items.filter((item: any) => item.productId !== productId);
    recalculateCart(updatedItems);
  };

  /**
   * Feature 2: Audit-logs a cashier-initiated line-item deletion.
   * Calls POST /api/pos/audit/line-item-delete (server-side IAuditLoggingService).
   * Called whenever a cart item is deleted — with OR without a manager PIN.
   *
   * SECURITY: wasManagerOverride is NOT sent to the server. The server derives the audit
   * action name (CASHIER_DIRECT_DELETE_LINE_ITEM vs MANAGER_OVERRIDE_VOID_ITEM) from the
   * caller's JWT role claim, which is cryptographically signed and cannot be forged.
   *
   * CART PRICE BEHAVIOR (Feature 1 design decision — documented here):
   * Item prices in the cart are FROZEN at the time the item was added to the cart
   * (unitPrice is captured from the batch/product at add-time). If a price change
   * is applied via the Price Change Module while this cart is open, the already-added
   * items are NOT live-updated. The cashier must remove and re-scan to get the
   * new price. This is intentional: it prevents unexpected price changes mid-transaction
   * and matches standard retail POS behaviour.
   */
  const auditLogLineItemDelete = async (item: any) => {
    try {
      const termCode = localStorage.getItem('pos_terminal_code') || 'LOCAL POS 01';
      const cleanTerm = termCode.replace(/^POS-/i, 'LOCAL POS ');
      const activeInvoiceRef = `${cleanTerm}-BILLING-DRAFT`;

      await api.post('/api/pos/audit/line-item-delete', {
        productId: item.productId,
        productName: item.name,
        quantity: item.qty,
        unitPrice: item.unitPrice,
        terminalId,
        cartRef: safeRandomUUID(),
        invoiceNumber: activeInvoiceRef
      });
    } catch (err) {
      // Non-critical: log locally but don't block the delete action
      console.warn('Failed to audit log line item delete', err);
    }
  };

  /**
   * Feature 2 (PERMISSIVE MODE): Handles cart item delete based on the cashierCanDeleteLineItem flag.
   * When toggle is ON → ALL roles (Owner, Manager, Cashier, Supervisor) delete directly, NO PIN prompt.
   * When toggle is OFF → Manager Override PIN required for ALL roles (existing security flow).
   *
   * Design rationale: The Owner controls the toggle — when they enable it, it signals that the
   * entire terminal/store environment has sufficient accountability (CCTV, supervisor presence, etc.)
   * to allow direct deletion by anyone operating the POS at that counter.
   */
  const handleDeleteCartItem = (item: any) => {
    if (cashierCanDeleteLineItem) {
      // Toggle is ON: direct delete — no PIN required — regardless of role
      // Server logs action name from JWT role (CASHIER_DIRECT_DELETE_LINE_ITEM or MANAGER_OVERRIDE_VOID_ITEM)
      removeItem(item.productId);
      auditLogLineItemDelete(item);
    } else {
      // Toggle is OFF: Manager Override PIN required for ALL roles
      requestManagerOverride('Void Item', () => {
        removeItem(item.productId);
        auditLogLineItemDelete(item);
      });
    }
  };

  // Barcode Scanner Integration
  useBarcodeScanner(async (barcode: string, weight?: number) => {
    try {
        const results = await searchProducts(barcode);
        const product = results.find(p => p.primaryBarcode === barcode || p.productCode === barcode);
        if (product) {
            addProductToCart(product, weight);
            setTimeout(() => productInputRef.current?.focus(), 50);
        } else {
            showToastNotification(`Barcode not found: "${barcode}"`, 'error');
            setProductQuery('');
            setSearchResults([]);
            setShowProductDropdown(false);
            setFocusedProductIndex(-1);
            productInputRef.current?.focus();
        }
    } catch (err) {
        console.error('Barcode lookup failed', err);
    }
  });

  // Keyboard Shortcuts
  usePosKeyboardShortcuts({
    onF1Search: () => customerInputRef.current?.focus(),
    onF2Product: () => productInputRef.current?.focus(),
    onF11Payment: () => {
      if (cart.items.length > 0 && cart.finalTotal > 0) setPaymentModalOpen(true);
    },
    onF9Park: () => {
      handleHoldCart();
    },
    onF10Reprint: () => setReprintModalOpen(true),
    onF8Return: () => setReturnModalOpen(true),
    onF7Cancel: () => {
      if (isAuthorizedToCancel) setCancelModalOpen(true);
    }
  });

  const handleHoldCart = async () => {
    if (cart.items.length === 0) {
      setHoldModalOpen(true);
      return;
    }
    
    const uuid = safeRandomUUID();
    const invoiceToHold = {
      id: uuid,
      invoiceNumber: `HOLD-${Date.now()}`,
      businessDate: activeBusinessDate || new Date().toISOString(),
      terminalId: terminalId,
      cashierId: cashierId,
      customerId: customer?.id || null,
      subTotal: cart.subtotal,
      discountAmount: cart.totalDiscount,
      taxAmount: cart.taxTotal,
      totalAmount: cart.finalTotal,
      roundOff: 0,
      netPayable: cart.finalTotal,
      items: cart.items.map((item: any) => ({
        productId: item.productId,
        barcode: item.barcode || '',
        productName: item.name,
        quantity: item.qty,
        unitPrice: item.unitPrice,
        discountAmount: item.discountAmount || 0,
        totalAmount: item.finalLineTotal,
        cgstRate: item.cgstRate || 0,
        cgstAmount: item.cgstAmount || 0,
        sgstRate: item.sgstRate || 0,
        sgstAmount: item.sgstAmount || 0,
        cessRate: item.cessRate || 0,
        cessAmount: item.cessAmount || 0
      }))
    };
    
    try {
      await holdInvoice(invoiceToHold);
      setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
      setCustomer(null);
      setCustomerQuery('');
      alert('Cart put on hold successfully.');
    } catch (err: any) {
      console.error(err);
      alert('Failed to hold cart globally: ' + (err.response?.data?.message || err.message));
    }
  };

  const handleResumeCart = (invoice: any) => {
      setCart({
          items: (invoice.items || []).map((i: any) => ({
             ...i,
             id: i.id || safeRandomUUID(),
             qty: i.qty ?? 0,
             unitPrice: i.unitPrice ?? 0,
             lineTotal: i.lineTotal ?? 0,
             finalLineTotal: i.finalLineTotal ?? 0,
             discountAmount: i.discountAmount ?? 0
          })),
          subtotal: 0, 
          totalDiscount: 0,
          taxTotal: 0,
          finalTotal: 0,
          appliedOfferNames: []
      });
      if (invoice.customer) {
          setCustomer(invoice.customer);
          setCustomerQuery(invoice.customer.phone || invoice.customer.name || '');
          recalculateCart(invoice.items, invoice.customer.id);
      } else {
          setCustomer(null);
          setCustomerQuery('');
          recalculateCart(invoice.items, null);
      }
      setHoldModalOpen(false);
  };

  const requestManagerOverride = (action: string, onSuccess: (pin?: string) => void) => {
      setManagerAction({ name: action, callback: onSuccess });
      setManagerModalOpen(true);
  };

  const handleProductSearch = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Tab') {
      if (cart.items.length > 0) {
        e.preventDefault();
        setShowProductDropdown(false);
        focusItemQtyInput();
        return;
      }
    }

    if (showProductDropdown && searchResults.length > 0) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        const newIndex = focusedProductIndex < searchResults.length - 1 ? focusedProductIndex + 1 : focusedProductIndex;
        scrollDropdownToIndex(newIndex);
        setFocusedProductIndex(newIndex);
        return;
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        const newIndex = focusedProductIndex > 0 ? focusedProductIndex - 1 : 0;
        scrollDropdownToIndex(newIndex);
        setFocusedProductIndex(newIndex);
        return;
      } else if (e.key === 'Escape') {
        setShowProductDropdown(false);
        setFocusedProductIndex(-1);
        return;
      }
    }

    if (e.key === 'Enter') {
      e.preventDefault();

      const val = productQuery.trim();
      if (!val) return;

      try {
        const results = await searchProducts(val);

        // GLOBAL POS RETAIL STANDARD: 1-to-1 Exact Barcode / ProductCode Match Check
        const exactMatch = results.find((p: any) =>
          p.primaryBarcode?.trim().toLowerCase() === val.toLowerCase() ||
          p.productCode?.trim().toLowerCase() === val.toLowerCase() ||
          (Array.isArray(p.barcodes) && p.barcodes.some((b: any) => {
            const str = typeof b === 'string' ? b : b?.barcodeValue;
            return str?.trim().toLowerCase() === val.toLowerCase();
          }))
        );

        if (exactMatch) {
          // Exact 1-to-1 match found! Directly add to cart and reset search box
          addProductToCart(exactMatch, voiceQuantity || undefined);
          setVoiceQuantity(null);
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
          setTimeout(() => productInputRef.current?.focus(), 50);
          return;
        }

        if (results.length === 1) {
          // Single match found! Directly add to cart and reset search box
          addProductToCart(results[0], voiceQuantity || undefined);
          setVoiceQuantity(null);
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
          setTimeout(() => productInputRef.current?.focus(), 50);
          return;
        }

        // If dropdown is open and an item is focused, select it
        if (showProductDropdown && focusedProductIndex >= 0 && focusedProductIndex < searchResults.length) {
          addProductToCart(searchResults[focusedProductIndex], voiceQuantity || undefined);
          setVoiceQuantity(null);
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
          setTimeout(() => productInputRef.current?.focus(), 50);
          return;
        }

        if (results.length > 1) {
          setSearchResults(results);
          setShowProductDropdown(true);
          setFocusedProductIndex(0); // auto-focus first item
        } else {
          showToastNotification(`Product not found for: "${val}"`, 'error');
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
          productInputRef.current?.focus();
        }
      } catch (err) {
        console.error('Error searching products:', err);
      }
    }
  };

  const selectCustomer = (cust: any) => {
    setCustomer({
      id: cust.id,
      name: cust.name,
      phone: cust.phone,
      walletBalance: cust.walletBalance,
      points: cust.loyaltyPoints,
      tier: cust.tierName
    });
    setCustomerQuery('');
    setCustomerSearchResults([]);
    setShowCustomerDropdown(false);
    setFocusedCustomerIndex(-1);
    productInputRef.current?.focus();
  };

  const handleCustomerSearch = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (showCustomerDropdown && customerSearchResults.length > 0) {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        const newIdx = focusedCustomerIndex < customerSearchResults.length - 1 ? focusedCustomerIndex + 1 : focusedCustomerIndex;
        setFocusedCustomerIndex(newIdx);
        scrollCustomerDropdownToIndex(newIdx);
        return;
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        const newIdx = focusedCustomerIndex > 0 ? focusedCustomerIndex - 1 : 0;
        setFocusedCustomerIndex(newIdx);
        scrollCustomerDropdownToIndex(newIdx);
        return;
      } else if (e.key === 'Escape') {
        setShowCustomerDropdown(false);
        setFocusedCustomerIndex(-1);
        return;
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (focusedCustomerIndex >= 0 && focusedCustomerIndex < customerSearchResults.length) {
          selectCustomer(customerSearchResults[focusedCustomerIndex]);
          return;
        }
      }
    }

    if (e.key === 'Enter') {
      e.preventDefault();
      const val = customerQuery.trim();
      if (!val) return;
      try {
        const results = await searchCustomers(val);
        if (results.length === 1) {
          selectCustomer(results[0]);
        } else if (results.length > 1) {
          setCustomerSearchResults(results);
          setShowCustomerDropdown(true);
          setFocusedCustomerIndex(0);
        } else {
          showToastNotification(`Customer not found for: "${val}". Click "+" to register a new customer!`, 'info');
        }
      } catch (err) {
        console.error('Error searching customer:', err);
      }
    }
  };

  if (dateLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-100 dark:bg-slate-900">
        <div className="flex flex-col items-center space-y-4">
          <Clock className="w-10 h-10 text-indigo-600 animate-spin" />
          <p className="text-slate-500 dark:text-slate-400 font-semibold">Validating Business Date...</p>
        </div>
      </div>
    );
  }

  if (!isBusinessDateOpen) {
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center p-6">
        <div className="bg-slate-800 rounded-2xl border border-slate-700 p-8 max-w-md w-full shadow-2xl space-y-6 text-center">
          <div className="w-16 h-16 rounded-full bg-red-950/40 text-red-400 flex items-center justify-center mx-auto border border-red-900/60 shadow-lg">
            <ShieldAlert className="w-8 h-8 animate-pulse" />
          </div>
          <div>
            <h1 className="text-2xl font-black text-white">Business Day Closed</h1>
            <p className="text-slate-400 text-sm mt-2">
              There is no active open business date. Supermarket registers are locked from billing until a new operational date is opened.
            </p>
          </div>

          <div className="bg-slate-900/40 p-4 rounded-xl border border-slate-700 text-left space-y-3">
            <label className="block text-[10px] font-bold text-slate-500 uppercase tracking-wider">
              Open Day Operational Date
            </label>
            <input 
              type="date"
              value={selectedOpenDate}
              onChange={(e) => setSelectedOpenDate(e.target.value)}
              className="w-full bg-slate-950 border border-slate-700 rounded-lg py-2.5 px-3.5 text-white font-semibold outline-none focus:ring-2 focus:ring-indigo-500 text-sm"
            />
          </div>

          <button
            onClick={async () => {
              if (!selectedOpenDate) return;

              const performOpen = async (overridePin?: string) => {
                try {
                  setOpeningDateSubmitting(true);
                  setDateLoading(true);
                  const success = await openBusinessDate({ 
                    businessDate: selectedOpenDate, 
                    openedBy: cashierId,
                    managerOverridePin: overridePin
                  });
                  if (success) {
                    alert(`Business Date ${selectedOpenDate} opened successfully!`);
                    // Reload business date state
                    const activeState = await getActiveBusinessDate();
                    setBusinessDateOpen(activeState.isOpen);
                    setActiveBusinessDate(activeState.businessDate);
                    
                    if (activeState.isOpen) {
                      const sessionData = await getCurrentSession(terminalId, cashierId);
                      if (sessionData && sessionData.status === 'OPEN') {
                        setActiveSession(sessionData);
                      } else {
                        setOpenShiftModalOpen(true);
                      }
                    }
                  }
                } catch (err: any) {
                  console.error(err);
                  const msg = err.response?.data ? (typeof err.response.data === 'string' ? err.response.data : JSON.stringify(err.response.data)) : err.message;
                  alert(`Failed to open business date: ${msg}`);
                } finally {
                  setDateLoading(false);
                  setOpeningDateSubmitting(false);
                }
              };

              if (user?.role === 'Cashier') {
                requestManagerOverride('Open Business Date', (pin?: string) => {
                  performOpen(pin);
                });
              } else {
                performOpen();
              }
            }}
            disabled={openingDateSubmitting || !selectedOpenDate}
            className="w-full py-3 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-bold rounded-xl shadow-lg shadow-emerald-500/10 hover:scale-[1.01] transition-all duration-200 flex items-center justify-center gap-2"
          >
            <Unlock className="w-4 h-4" />
            {openingDateSubmitting ? 'Opening Day...' : 'Open Business Day'}
          </button>
        </div>

        <ManagerPinModal
          isOpen={isManagerModalOpen}
          onClose={() => setManagerModalOpen(false)}
          actionName={managerAction?.name}
          onSuccess={(pin?: string) => {
              setManagerModalOpen(false);
              managerAction?.callback(pin);
              setManagerAction(null);
          }}
        />
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-slate-100">
      {/* Left: Product/Cart Panel */}
      <div className="w-2/3 flex flex-col border-r border-slate-300 bg-white">
        
        {/* CRM Top Bar */}
        <div className="p-4 bg-indigo-50 border-b border-indigo-100 flex items-center justify-between">
          <div className="flex items-center w-1/2 relative">
            <User className="absolute left-3 text-indigo-400" />
            <input 
              ref={customerInputRef}
              type="text" 
              placeholder="F1: Search Customer (Phone/Name)..." 
              className="w-full pl-10 p-2 rounded-l border border-indigo-200 outline-none focus:ring-2 ring-indigo-500 font-bold text-slate-800"
              value={customerQuery}
              onChange={(e) => setCustomerQuery(e.target.value)}
              onKeyDown={handleCustomerSearch}
            />
            <button 
              onClick={() => setCustomerModalOpen(true)}
              className="bg-indigo-600 text-white p-2 rounded-r hover:bg-indigo-700 flex items-center"
            >
              <Plus className="w-5 h-5" />
            </button>

            {/* Customer Search Dropdown Overlay */}
            {showCustomerDropdown && customerSearchResults.length > 0 && (
              <div
                ref={customerDropdownRef}
                className="absolute left-0 top-full mt-1 w-[400px] bg-white border-2 border-indigo-300 rounded-lg shadow-2xl z-50 max-h-72 overflow-y-auto"
                style={{ overscrollBehavior: 'contain' }}
              >
                <div className="p-2 border-b border-indigo-100 flex justify-between items-center bg-indigo-50">
                  <span className="text-xs font-bold text-indigo-800">Select Customer (Use ↑ ↓ and Enter):</span>
                  <button onClick={() => setShowCustomerDropdown(false)} className="text-slate-400 hover:text-slate-600 text-xs font-bold">Close</button>
                </div>
                {customerSearchResults.map((cust: any, idx: number) => (
                  <div 
                    key={cust.id}
                    data-cust-idx={idx}
                    onClick={() => selectCustomer(cust)}
                    className={`px-4 py-2.5 cursor-pointer flex justify-between items-center transition border-b border-slate-100 ${
                      focusedCustomerIndex === idx ? 'bg-indigo-100 border-l-4 border-indigo-600 font-bold text-indigo-950' : 'hover:bg-indigo-50/70'
                    }`}
                  >
                    <div>
                      <p className="font-extrabold text-slate-800 text-sm flex items-center gap-1.5">
                        {cust.name}
                        {cust.tierName && (
                          <span className="bg-amber-100 text-amber-900 text-[10px] px-1.5 py-0.5 rounded font-black border border-amber-200">
                            {cust.tierName}
                          </span>
                        )}
                      </p>
                      <p className="text-xs text-slate-500 font-semibold mt-0.5">{cust.phone || 'No Phone'}</p>
                    </div>
                    <div className="text-right">
                      <span className="text-xs font-extrabold text-orange-600 bg-orange-50 px-2 py-0.5 rounded border border-orange-200">
                        ⭐ {cust.loyaltyPoints || 0} Pts
                      </span>
                      {cust.walletBalance !== undefined && cust.walletBalance !== 0 && (
                        <p className="text-xs font-bold text-blue-600 mt-1">₹{cust.walletBalance}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {customer && (
            <div className="flex items-center gap-4 bg-white p-2 rounded shadow-sm border border-indigo-200 w-full ml-4">
              <div className="flex-1">
                <p className="font-bold text-slate-800 text-sm flex items-center">
                  {customer.name} 
                  {customer.tier && <span className="bg-yellow-100 text-yellow-800 text-xs px-2 py-0.5 rounded ml-2 font-bold flex items-center shadow-sm"><Award className="w-3 h-3 mr-1"/>{customer.tier}</span>}
                </p>
                <p className="text-xs text-gray-500 mt-0.5 font-medium">{customer.phone}</p>
              </div>
              <div className="flex items-center gap-4 border-l pl-4">
                <div className="text-right">
                  <p className="text-xs text-gray-500 mb-1">Wallet</p>
                  <p className="text-sm text-blue-600 font-bold flex items-center justify-end"><Wallet className="w-4 h-4 mr-1"/> ₹{customer.walletBalance}</p>
                </div>
                <div className="text-right border-l pl-4">
                  <p className="text-xs text-gray-500 mb-1">Loyalty</p>
                  <p className="text-sm text-orange-600 font-bold flex items-center justify-end"><Award className="w-4 h-4 mr-1"/> {customer.points} Pts</p>
                </div>
                
                {customer.points > 0 && (
                  <button 
                    onClick={() => {
                      const pts = prompt(`Available Points: ${customer.points}\nConversion: 10 Points = 1 Rs\n\nEnter points to redeem:`, "0");
                      if (pts && !isNaN(Number(pts))) {
                        const numPts = Number(pts);
                        if (numPts <= customer.points) {
                          setPointsRedeemed(numPts);
                        } else {
                          alert("Insufficient points");
                        }
                      }
                    }}
                    className="ml-2 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 text-white px-3 py-1.5 rounded text-xs font-bold shadow transition flex items-center"
                  >
                    <Tag className="w-3 h-3 mr-1"/> Redeem Pts
                  </button>
                )}
                
                <button onClick={() => { setCustomer(null); setCustomerQuery(''); setPointsRedeemed(0); }} className="text-gray-400 hover:text-red-500 ml-2 p-1 bg-red-50 hover:bg-red-100 rounded transition"><X className="w-4 h-4"/></button>
              </div>
            </div>
          )}
        </div>

        {/* Product Search / Barcode Input Bar */}
        <div className="p-4 bg-slate-50 border-b border-slate-200 relative">
          <div className="flex gap-2 items-center">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-3 text-slate-400 w-5 h-5" />
              <input 
                ref={productInputRef}
                type="text"
                placeholder="F2: Scan Barcode or Type Product Name (Press Enter)..."
                className="w-full pl-10 pr-4 py-2.5 border-2 border-slate-300 rounded-lg outline-none focus:ring-4 focus:ring-emerald-500/40 focus:border-emerald-600 font-bold text-slate-850 text-base shadow-sm transition-all"
                value={productQuery}
                onChange={(e) => {
                  const val = e.target.value;
                  setProductQuery(val);
                  if (!val.trim()) {
                    setSearchResults([]);
                    setShowProductDropdown(false);
                    setFocusedProductIndex(-1);
                  }
                }}
                onKeyDown={handleProductSearch}
              />
            </div>
            
            {/* Language Selector Capsule */}
            <button
              onClick={handleVoiceLanguageToggle}
              className="px-3.5 py-2.5 border border-slate-300 rounded-lg bg-white text-xs font-black text-slate-700 hover:bg-slate-50 transition-colors shadow-sm whitespace-nowrap min-w-[70px]"
              title="Voice Language (மொழி)"
            >
              {voiceLanguage === 'ta-IN' ? 'தமிழ்' : 'English'}
            </button>

            {/* Mic Toggle Button */}
            <button
              onClick={toggleListening}
              className={`p-2.5 rounded-lg border transition-all flex items-center justify-center shadow-sm ${
                isListening 
                  ? 'bg-red-500 text-white border-red-500 animate-pulse' 
                  : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'
              }`}
              title="Voice Search (Ctrl + M)"
            >
              {isListening ? <Mic className="w-5 h-5 animate-pulse" /> : <MicOff className="w-5 h-5" />}
            </button>
          </div>

          {/* Voice status banner */}
          {voiceStatus && (
            <div className={`mt-2 px-3 py-1.5 rounded-md text-xs font-bold transition-all shadow-sm flex items-center gap-2 ${
              voiceStatusType === 'success' ? 'bg-emerald-50 text-emerald-700 border border-emerald-100' :
              voiceStatusType === 'error' ? 'bg-rose-50 text-rose-700 border border-rose-100' :
              voiceStatusType === 'listening' ? 'bg-blue-50 text-blue-700 border border-blue-100' :
              'bg-slate-100 text-slate-700 border border-slate-200'
            }`}>
              <div className={`w-2 h-2 rounded-full ${
                voiceStatusType === 'success' ? 'bg-emerald-500' :
                voiceStatusType === 'error' ? 'bg-rose-500' :
                voiceStatusType === 'listening' ? 'bg-blue-500 animate-ping' :
                'bg-slate-500'
              }`} />
              <span>{voiceStatus}</span>
            </div>
          )}

          {/* Toast Notification Banner overlay */}
          {toastNotification && (
            <div className="mt-2 px-4 py-2 rounded-lg font-black text-xs border-2 shadow-md flex items-center gap-2 animate-pulse bg-rose-600 text-white border-rose-700">
              <span className="w-2 h-2 rounded-full bg-white animate-ping" />
              <span>{toastNotification.message}</span>
            </div>
          )}

          {/* Search Dropdown Overlay */}
          {showProductDropdown && searchResults.length > 0 && (
            <div
              ref={dropdownRef}
              className="absolute left-4 right-4 mt-1 bg-white border border-slate-200 rounded-lg shadow-xl z-50 max-h-60 overflow-y-auto"
              style={{ overscrollBehavior: 'contain' }}
            >
              <div className="p-2 border-b border-slate-100 flex justify-between items-center bg-slate-50">
                <span className="text-xs font-semibold text-slate-500">Multiple items found. Select one:</span>
                <button onClick={() => setShowProductDropdown(false)} className="text-slate-400 hover:text-slate-600 text-xs font-bold">Close</button>
              </div>
              {searchResults.map((p, idx) => (
                <div 
                  key={p.id}
                  data-idx={idx}
                  onClick={() => {
                    addProductToCart(p, voiceQuantity || undefined);
                    setVoiceQuantity(null);
                    setProductQuery('');
                    setSearchResults([]);
                    setShowProductDropdown(false);
                    setFocusedProductIndex(-1);
                    setTimeout(() => productInputRef.current?.focus(), 50);
                  }}
                  className={`px-4 py-2.5 cursor-pointer flex justify-between items-center transition ${focusedProductIndex === idx ? 'bg-blue-100 border-l-4 border-blue-500' : 'hover:bg-blue-50 border-b border-slate-100'}`}
                >
                  <div>
                    <p className="font-bold text-slate-800">{p.name}</p>
                    <p className="text-xs text-slate-400">Code: {p.productCode} | Barcode: {p.primaryBarcode || 'N/A'}</p>
                  </div>
                  <span className="font-extrabold text-blue-600">₹{p.sellingPrice.toFixed(2)}</span>
                </div>
              ))}
            </div>
          )}
        </div>



        {/* Cart Table with Dynamic Controls */}
        <div ref={cartContainerRef} className="p-0 flex-1 overflow-y-auto">
          {cart.items.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center text-slate-400">
              <ShoppingCart className="w-16 h-16 mb-2 stroke-1" />
              <p className="font-semibold">Billing cart is empty</p>
              <p className="text-xs">Scan items or search products above to start checkout</p>
            </div>
          ) : (
            <table className="w-full text-left border-collapse border border-slate-300">
              <thead className="bg-slate-200 sticky top-0 border-b-2 border-slate-400 shadow-xs z-10">
                <tr className="text-slate-800 font-extrabold text-xs">
                  <th className="p-3 text-center w-14 border-r border-slate-300">S.No</th>
                  <th className="p-3 border-r border-slate-300">Item Name & Details</th>
                  <th className="p-3 text-center w-36 border-r border-slate-300">Qty</th>
                  <th className="p-3 text-right w-24 border-r border-slate-300">MRP</th>
                  <th className="p-3 text-right w-24 border-r border-slate-300">Price</th>
                  <th className="p-3 text-right w-28 border-r border-slate-300">Total</th>
                  <th className="p-3 text-center w-16">Action</th>
                </tr>
              </thead>
              <tbody>
                {cart.items.map((item: any, index: number) => {
                  const isSelected = selectedCartIndex === index || (selectedCartIndex === -1 && index === cart.items.length - 1);
                  const isEven = index % 2 === 0;

                  const rowStyle = isSelected
                    ? 'bg-gradient-to-r from-amber-100/90 via-amber-50 to-amber-100/90 border-l-4 border-l-amber-500 font-extrabold shadow-sm text-slate-900'
                    : isEven
                      ? 'bg-white hover:bg-slate-100/70'
                      : 'bg-emerald-50/40 hover:bg-emerald-100/50';

                  return (
                    <tr 
                      key={item.id} 
                      ref={isSelected ? activeItemRowRef : null}
                      onClick={() => setSelectedCartIndex(index)}
                      className={`border-b border-slate-300 cursor-pointer transition-all duration-150 ${rowStyle}`}
                    >
                      <td className={`p-3 text-center border-r border-slate-300 ${isSelected ? 'bg-amber-200/80 font-black text-slate-900' : 'bg-slate-100/60 font-bold text-slate-600'}`}>
                        {isSelected ? (
                          <span className="inline-flex items-center gap-0.5 text-xs font-black text-amber-950 bg-amber-400 px-1.5 py-0.5 rounded shadow-xs animate-pulse">
                            ▶ {index + 1}
                          </span>
                        ) : (
                          index + 1
                        )}
                      </td>
                      <td className="p-3 border-r border-slate-300">
                        <p className="font-extrabold text-slate-800 text-sm leading-snug">{item.name}</p>
                        {(item.nameTamil || item.secondaryName || item.tamilName) && (
                          <p className="text-xs font-bold text-emerald-800 font-tamil mt-0.5">
                            {item.nameTamil || item.secondaryName || item.tamilName}
                          </p>
                        )}
                        {item.appliedOfferName && (
                          <p className="text-xs text-emerald-700 flex items-center font-bold bg-emerald-100/80 w-max px-2 py-0.5 rounded mt-1 border border-emerald-300">
                            <Tag className="w-3 h-3 mr-1" /> {item.appliedOfferName}
                          </p>
                        )}
                        {item.batches && item.batches.length > 0 && (
                          <div className="mt-2 flex items-center gap-1.5" onClick={(e) => e.stopPropagation()}>
                            <span className="text-[10px] font-bold text-indigo-700 bg-indigo-100 px-1 py-0.5 rounded border border-indigo-200">Batch</span>
                            <select
                              className="text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded p-1 outline-none focus:ring-1 focus:ring-indigo-500 max-w-[200px]"
                              value={item.batchId || ''}
                              onChange={(e) => updateItemBatch(item.productId, e.target.value)}
                            >
                              {item.batches.map((b: any) => (
                                <option key={b.id} value={b.id}>
                                  {b.batchNumber} {b.expiryDate ? `(Exp: ${b.expiryDate.substring(0, 10)})` : '(No Exp)'} [Qty: {b.currentStock}]
                                </option>
                              ))}
                            </select>
                          </div>
                        )}
                      </td>
                      <td className="p-3 border-r border-slate-300" onClick={(e) => e.stopPropagation()}>
                        <div className="flex items-center justify-center gap-2">
                          <button onClick={() => updateItemQty(item.productId, -1)} className="text-slate-500 hover:text-indigo-600 transition">
                            <MinusCircle className="w-6 h-6" />
                          </button>
                          <input 
                            ref={(el) => { itemQtyRefs.current[item.productId] = el; }}
                            type="number"
                            min="1"
                            // GAP-05 FIX: enforce integer quantities for non-weighable products
                            step={item.isWeighable ? 'any' : '1'}
                            className="font-black text-lg w-16 text-center border border-slate-300 rounded focus:outline-none focus:border-indigo-500 focus:ring-2 focus:ring-amber-500 bg-white shadow-xs"
                            value={item.qty === '' ? '' : item.qty}
                            onFocus={(e) => e.target.select()}
                            onClick={(e) => {
                              e.stopPropagation();
                              e.currentTarget.select();
                            }}
                            onKeyDown={(e) => {
                              if (e.key === 'Enter' || e.key === 'Tab') {
                                e.preventDefault();
                                productInputRef.current?.focus();
                                productInputRef.current?.select();
                              }
                            }}
                            onChange={(e) => {
                              if (e.target.value === '') {
                                updateItemQtyExact(item.productId, '');
                                return;
                              }
                              const raw = item.isWeighable
                                ? parseFloat(e.target.value)
                                : parseInt(e.target.value, 10);
                              const val = isNaN(raw) ? 1 : raw;
                              if (val >= 0) {
                                updateItemQtyExact(item.productId, val);
                              }
                            }}
                          />
                          <button onClick={() => updateItemQty(item.productId, 1)} className="text-slate-500 hover:text-indigo-600 transition">
                            <PlusCircle className="w-6 h-6" />
                          </button>
                        </div>
                      </td>
                      <td className="p-3 text-right font-bold text-slate-600 border-r border-slate-300">₹{(item.mrp || item.unitPrice).toFixed(2)}</td>
                      <td className="p-3 text-right font-bold text-slate-800 border-r border-slate-300">₹{item.unitPrice.toFixed(2)}</td>
                      <td className="p-3 text-right border-r border-slate-300">
                        {item.discountAmount > 0 && <p className="text-xs text-slate-400 line-through">₹{item.lineTotal.toFixed(2)}</p>}
                        <p className="font-black text-xl text-slate-900">₹{item.finalLineTotal.toFixed(2)}</p>
                      </td>
                      <td className="p-3 text-center" onClick={(e) => e.stopPropagation()}>
                        <button
                          onClick={() => handleDeleteCartItem(item)}
                          className={`transition p-1 ${
                            cashierCanDeleteLineItem
                              ? 'text-red-500 hover:text-red-700 hover:bg-red-50 rounded'
                              : 'text-slate-400 hover:text-red-600'
                          }`}
                          title={
                            cashierCanDeleteLineItem
                              ? 'Delete Item'
                              : 'Void Item (Manager Override Required)'
                          }
                        >
                          <Trash2 className="w-5 h-5" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Right: Payment Panel */}
      <div className="w-1/3 flex flex-col bg-slate-50 p-6">
        <div className="flex justify-between items-center mb-6 border-b pb-2">
          <div className="flex items-center gap-2">
            <h2 className="text-2xl font-black text-slate-800">Payment</h2>
            <button 
              onClick={toggleFullscreen} 
              className="text-slate-400 hover:text-slate-600 hover:bg-slate-200/50 p-1.5 rounded-lg transition-colors ml-1"
              title={isFullscreen ? "Exit Fullscreen" : "Enter Fullscreen"}
            >
              {isFullscreen ? <Minimize className="w-5 h-5" /> : <Maximize className="w-5 h-5" />}
            </button>
          </div>
          {activeSession && (
            <button 
              onClick={() => setCloseShiftModalOpen(true)}
              className="bg-red-100 text-red-600 px-4 py-2 rounded-lg font-bold hover:bg-red-200 transition-colors shadow-sm"
            >
              Close Shift
            </button>
          )}
        </div>
        
        <div className="flex-1">
          {/* Promo Code Input */}
          <div className="flex mb-6">
            <input 
              type="text" 
              placeholder="Promo Code" 
              className="w-full p-2 border border-r-0 rounded-l outline-none focus:border-indigo-500 font-bold uppercase"
              value={promoCode}
              onChange={(e) => setPromoCode(e.target.value.toUpperCase())}
            />
            <button className="bg-slate-800 text-white px-4 rounded-r font-bold hover:bg-slate-700" onClick={() => recalculateCart(cart.items)}>Apply</button>
          </div>

          <div className="flex justify-between text-lg mb-2"><span>Subtotal</span><span className="font-bold text-slate-700">₹{cart.subtotal.toFixed(2)}</span></div>
          <div className="flex justify-between text-lg mb-2 text-emerald-600 group relative">
            <span>Discounts</span>
            <div className="flex items-center gap-2">
              <span className="font-bold cursor-help">-₹{cart.totalDiscount.toFixed(2)}</span>
            </div>
          </div>
          
          {pointsRedeemed > 0 && (
            <div className="flex justify-between text-lg mb-2 text-indigo-600">
              <span>Points Redeemed ({pointsRedeemed})</span>
              <span className="font-bold">-₹{(pointsRedeemed / 10).toFixed(2)}</span> {/* Assumption: 10 points = 1 Rs. Actual ratio via backend */}
            </div>
          )}

          {cart.appliedOfferNames.length > 0 && !suppressOffers && (
            <div className="bg-emerald-50 text-emerald-700 p-2 rounded-lg text-sm mb-3 shadow-sm border border-emerald-100 flex justify-between items-start">
              <div>
                <span className="font-bold block mb-1">Offer(s) Applied:</span>
                <ul className="space-y-1">
                  {cart.appliedOfferNames.map((o: string) => (
                    <li key={o} className="flex items-center gap-1">
                      <Tag className="w-3 h-3" /> {o}
                    </li>
                  ))}
                </ul>
              </div>
              <button 
                onClick={() => requestManagerOverride('Remove Offers', () => setSuppressOffers(true))} 
                className="text-emerald-700 hover:text-emerald-900 bg-emerald-100 hover:bg-emerald-200 p-1.5 rounded text-xs font-bold flex items-center transition-colors"
                title="Remove Offers (Manager Override)"
              >
                <X className="w-3 h-3" />
              </button>
            </div>
          )}

          {suppressOffers && (
            <div className="bg-slate-100 text-slate-600 p-2 rounded-lg text-sm mb-3 flex justify-between items-center border border-slate-200">
              <span className="italic text-xs">Offers Suppressed</span>
              <button onClick={() => setSuppressOffers(false)} className="text-xs text-indigo-600 hover:underline font-bold">
                 Reapply Offers
              </button>
            </div>
          )}
          
          <div className="flex justify-between text-lg mb-6"><span>Tax (GST)</span><span>₹{cart.taxTotal.toFixed(2)}</span></div>
          
          <div className="flex justify-between text-4xl font-black text-indigo-700 mb-8 border-t pt-4">
            <span>Total</span><span>₹{finalBillTotal.toFixed(2)}</span>
          </div>

          {/* Payment Methods */}
          <div className="mt-4">
            <button 
              disabled={cart.items.length === 0 || finalBillTotal < 0}
              className="w-full bg-emerald-600 text-white p-4 rounded-lg font-black text-2xl shadow-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center transition-colors mb-4" 
              onClick={() => {
                if (cart.items.length === 0) {
                  alert('Cart is empty. Please add items before proceeding to payment.');
                  return;
                }
                if (cart.finalTotal <= 0) {
                  alert('Invoice total is ₹0.00. Cannot process a zero-value transaction. Please check item prices or remove offers that reduce the total to zero.');
                  return;
                }
                setPaymentModalOpen(true);
              }}
            >
              <CreditCard className="w-8 h-8 mr-3" /> PAYMENT (F11)
            </button>
            <div className="flex gap-3">
               {isAuthorizedToCancel && (
                 <button 
                   onClick={() => setCancelModalOpen(true)} 
                   className="flex-1 bg-red-700 hover:bg-red-800 text-white p-3.5 rounded-xl shadow-md flex flex-col items-center justify-center gap-1.5 transition-all active:scale-95 text-xs font-bold"
                 >
                   <ShieldAlert className="w-5 h-5 text-red-200" />
                   <span>F7: Cancel</span>
                 </button>
               )}
               <button 
                 onClick={() => setReturnModalOpen(true)} 
                 className="flex-1 bg-rose-700 hover:bg-rose-800 text-white p-3.5 rounded-xl shadow-md flex flex-col items-center justify-center gap-1.5 transition-all active:scale-95 text-xs font-bold"
               >
                 <RotateCcw className="w-5 h-5" />
                 <span>F8: Return</span>
               </button>
               <button 
                 onClick={handleHoldCart} 
                 className="flex-1 bg-amber-600 hover:bg-amber-700 text-white p-3.5 rounded-xl shadow-md flex flex-col items-center justify-center gap-1.5 transition-all active:scale-95 text-xs font-bold"
               >
                 <Clock className="w-5 h-5" />
                 <span>F9: Hold/Resume</span>
               </button>
               <button 
                 onClick={() => setReprintModalOpen(true)} 
                 className="flex-1 bg-slate-700 hover:bg-slate-800 text-white p-3.5 rounded-xl shadow-md flex flex-col items-center justify-center gap-1.5 transition-all active:scale-95 text-xs font-bold"
               >
                 <Printer className="w-5 h-5" />
                 <span>F10: Reprint</span>
               </button>
            </div>
          </div>
        </div>
      <PaymentModal 
        isOpen={isPaymentModalOpen} 
        onClose={() => !isProcessing && setPaymentModalOpen(false)} 
        cartTotal={finalBillTotal}
        isProcessing={isProcessing}
        customer={customer}
        onCompletePayment={async (tenders: any) => {
          try {
            setIsProcessing(true);

            // LOGIC-01 Guard: Block checkout if terminalId or cashierId is missing/invalid.
            // Runs before any payload construction, network call, or IndexedDB write.
            const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
            if (!terminalId || !UUID_RE.test(terminalId)) {
              console.error('[POS] Checkout blocked: terminalId is missing or invalid.', { terminalId });
              alert('This device is not registered as a POS terminal. Please go to Settings \u2192 Terminal Configuration \u2192 "Register This Device" before billing.');
              setIsProcessing(false);
              return;
            }
            if (!cashierId || !UUID_RE.test(cashierId)) {
              console.error('[POS] Checkout blocked: cashierId is missing or invalid.', { cashierId, user });
              alert('Your session has expired. Please log in again to complete this sale.');
              setIsProcessing(false);
              return;
            }
            // Generate dynamic invoice payload matching CreateInvoiceCommand
            const roundOffVal = +(Math.round(finalBillTotal) - finalBillTotal).toFixed(2);
            const netPayableVal = Math.round(finalBillTotal);
            const paymentModeVal = tenders.cash > 0 && (tenders.upi > 0 || tenders.card > 0) ? 'SPLIT'
                                 : tenders.cash > 0 ? 'CASH'
                                 : tenders.upi > 0  ? 'UPI'
                                 : tenders.card > 0 ? 'CARD'
                                 : 'WALLET';
            
            const hasZeroRateItem = cart.items.some((item: any) => item.unitPrice <= 0);

            const executeCheckout = async (supervisorPin?: string) => {
              const payload = {
                invoiceNumber: `INV-${localStorage.getItem('pos_terminal_code') || 'POS-01'}-${Date.now().toString().slice(-6)}`,
                terminalId: terminalId,
                cashierId: cashierId,
                customerId: customer?.id || undefined,
                promoCode: promoCode || undefined,
                walletAmountUsed: tenders.wallet || 0,
                cashAmount: tenders.cash || 0,
                upiAmount: tenders.upi || 0,
                cardAmount: tenders.card || 0,
                roundOff: roundOffVal,
                netPayable: netPayableVal,
                paymentMode: paymentModeVal,
                pointsRedeemed: pointsRedeemed || 0,
                supervisorOverridePin: supervisorPin || undefined,
                items: cart.items.map((item: any) => ({
                  productId: item.productId,
                  quantity: item.qty,
                  unitPrice: item.unitPrice,
                  batchId: item.batchId || undefined
                }))
              };

              // 1. Calculate offline loyalty estimation
              const oldLoyaltyPoints = customer ? (customer.points || 0) : 0;
              let offlineLoyaltyEarned = customer ? Math.floor(finalBillTotal / 100) : 0;
              let offlineLoyaltyBalance = oldLoyaltyPoints + offlineLoyaltyEarned - pointsRedeemed;

              // 2. Construct the FULL invoice object for local storage and printing
              const invoiceId = safeRandomUUID();
              const fullInvoice = {
                id: invoiceId,
                invoiceNumber: payload.invoiceNumber,
                businessDate: new Date().toISOString(),
                terminalId: payload.terminalId,
                terminalSequence: 1,
                cashierId: cashierId,
                cashierName: user?.fullName || user?.username || 'Cashier',
                customerName: customer?.name || undefined,
                customerPhone: customer?.phone || undefined,
                customerId: customer?.id || undefined,
                loyaltyPointsEarned: offlineLoyaltyEarned,
                loyaltyPointsBalance: offlineLoyaltyBalance,
                subTotal: cart.subtotal,
                discountAmount: cart.totalDiscount,
                taxAmount: cart.taxTotal,
                totalAmount: cart.finalTotal,
                cashAmount: tenders.cash || 0,
                upiAmount: tenders.upi || 0,
                cardAmount: tenders.card || 0,
                walletAmountUsed: tenders.wallet || 0,
                roundOff: roundOffVal,
                netPayable: netPayableVal,
                paymentMode: paymentModeVal,
                pointsRedeemed: payload.pointsRedeemed,
                status: 'COMPLETED',
                items: cart.items.map((item: any) => ({
                  id: item.id || safeRandomUUID(),
                  productId: item.productId,
                  name: item.name,
                  quantity: item.qty,
                  unitPrice: item.unitPrice,
                  cgstRate: item.cgstRate || 0,
                  sgstRate: item.sgstRate || 0,
                  cessRate: item.cessRate || 0,
                  discountAmount: item.discountAmount || 0,
                  totalAmount: item.finalLineTotal || item.lineTotal,
                  // Add mapping for Sync API expected fields
                  barcode: item.barcode || item.primaryBarcode || undefined,
                  productName: item.name,
                  cgstAmount: (() => {
                    const itemTaxRate = (item.cgstRate || 0) + (item.sgstRate || 0) + (item.cessRate || 0);
                    const lineTotal = item.finalLineTotal || item.lineTotal;
                    const taxable = lineTotal / (1 + itemTaxRate / 100);
                    return +(taxable * ((item.cgstRate || 0) / 100)).toFixed(2);
                  })(),
                  sgstAmount: (() => {
                    const itemTaxRate = (item.cgstRate || 0) + (item.sgstRate || 0) + (item.cessRate || 0);
                    const lineTotal = item.finalLineTotal || item.lineTotal;
                    const taxable = lineTotal / (1 + itemTaxRate / 100);
                    return +(taxable * ((item.sgstRate || 0) / 100)).toFixed(2);
                  })(),
                  cessAmount: (() => {
                    const itemTaxRate = (item.cgstRate || 0) + (item.sgstRate || 0) + (item.cessRate || 0);
                    const lineTotal = item.finalLineTotal || item.lineTotal;
                    const taxable = lineTotal / (1 + itemTaxRate / 100);
                    return +(taxable * ((item.cessRate || 0) / 100)).toFixed(2);
                  })()
                }))
              };

              try {
                const response = await createInvoice(payload);
                if (response) {
                  fullInvoice.id = response.invoiceId || (response as any).InvoiceId || (response as any).id || fullInvoice.id;
                  fullInvoice.invoiceNumber = response.invoiceNumber || (response as any).InvoiceNumber || fullInvoice.invoiceNumber;
                }
                // Re-fetch customer to get updated loyalty balance from backend.
                // Only update if the backend returned a HIGHER balance than our offline
                // estimate (guards against a race condition where the DB hasn't flushed yet).
                if (customer?.phone) {
                  try {
                    const freshCustomers = await searchCustomers(customer.phone);
                    if (freshCustomers.length > 0) {
                      const backendBalance = freshCustomers[0].loyaltyPoints || 0;
                      if (backendBalance > oldLoyaltyPoints) {
                        // Backend has committed the new points — use the real values
                        fullInvoice.loyaltyPointsBalance = backendBalance;
                        fullInvoice.loyaltyPointsEarned  = Math.max(0, backendBalance - oldLoyaltyPoints);
                      }
                      // else: keep the offline-calculated estimate (backend hadn't flushed yet)
                    }
                  } catch { /* non-critical — keep offline estimate */ }
                }
              } catch (err: any) {
                const errorText = err?.response?.data?.message || err?.response?.data?.detailed || err?.response?.data?.Detailed || err?.response?.data?.Message || err?.message || "";
                
                if (errorText.includes("INSUFFICIENT_STOCK")) {
                  const pin = prompt(`${errorText}\n\nPlease enter Supervisor PIN to override negative stock sale:`);
                  if (pin !== null && pin.trim() !== "") {
                    try {
                      const retryPayload = { ...payload, supervisorOverridePin: pin };
                      const retryResponse = await createInvoice(retryPayload);
                      if (retryResponse) {
                        fullInvoice.id = retryResponse.invoiceId || (retryResponse as any).InvoiceId || (retryResponse as any).id || fullInvoice.id;
                        fullInvoice.invoiceNumber = retryResponse.invoiceNumber || (retryResponse as any).InvoiceNumber || fullInvoice.invoiceNumber;
                      }
                    } catch (retryErr: any) {
                      const retryErrorText = retryErr?.response?.data?.message || retryErr?.response?.data?.detailed || retryErr?.response?.data?.Detailed || retryErr?.response?.data?.Message || retryErr?.message || "Invalid PIN.";
                      alert("Override Failed: " + retryErrorText);
                      throw new Error(retryErrorText);
                    }
                  } else {
                    throw new Error("Checkout blocked: Insufficient stock and no override PIN provided.");
                  }
                } else if (errorText.includes("ZERO_RATE_LIMIT")) {
                  alert(errorText);
                  throw new Error(errorText);
                } else {
                  console.warn('Network issue during checkout, saving offline...', err);
                  await posDb.invoices.put(fullInvoice as any);
                  await posDb.sync_queue.put(fullInvoice as any);
                  const errorDetail = err?.response?.data?.detailed || err?.response?.data?.message || err?.response?.data?.Detailed || err?.response?.data?.Message || err?.message || JSON.stringify(err);
                  alert(`Saved Offline: Invoice ${payload.invoiceNumber} queued for sync.\n\nERROR DETAIL:\n${errorDetail}`);
                }
              }

              await posDb.invoices.put(fullInvoice as any);

              setCompletedInvoice(fullInvoice);
              setPaymentModalOpen(false);
              printReceipt(fullInvoice);

              setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
              setCustomer(null);
              setCustomerQuery('');
              setPromoCode('');
              setPointsRedeemed(0);
            };

            if (hasZeroRateItem) {
              requestManagerOverride('Zero Rate Item Checkout', (pin?: string) => {
                if (pin) {
                  executeCheckout(pin);
                } else {
                  alert("Manager Override PIN required to sell items at zero rate.");
                  setIsProcessing(false);
                }
              });
            } else {
              await executeCheckout();
            }
          } catch (err: any) {
            console.error('Checkout error:', err);
            alert('Failed to process checkout: ' + (err.message));
          } finally {
            setIsProcessing(false);
          }
        }} 
      />

      </div>

      <CustomerRegistrationModal 
        isOpen={isCustomerModalOpen} 
        initialPhone={customerQuery}
        onClose={() => setCustomerModalOpen(false)} 
        onRegister={async (newCust: any) => {
          try {
            const customerId = await registerCustomer({
              phone: newCust.phone,
              name: newCust.name,
              tamilName: newCust.tamilName || undefined,
              email: newCust.email || undefined,
              dob: newCust.dob || undefined,
              marketingConsent: newCust.marketingConsent
            });
            // Automatically select registered customer
            setCustomer({
              id: customerId,
              name: newCust.name,
              phone: newCust.phone,
              walletBalance: 0,
              points: 0,
              tier: 'Base'
            });
            setCustomerQuery('');
            alert('Customer registered successfully!');
          } catch (err) {
            console.error('Error registering customer:', err);
            throw err;
          }
        }} 
      />

      {/* Modals */}

      <HoldResumeModal 
        isOpen={isHoldModalOpen} 
        onClose={() => setHoldModalOpen(false)} 
        onResume={handleResumeCart} 
      />

      <ReprintModal
        isOpen={isReprintModalOpen}
        onClose={() => setReprintModalOpen(false)}
        onReprint={(inv: any) => {
          printReceipt(inv);
        }}
      />

      <ManagerPinModal
        isOpen={isManagerModalOpen}
        onClose={() => setManagerModalOpen(false)}
        actionName={managerAction?.name}
        onSuccess={async (pin?: string) => {
            setManagerModalOpen(false);
            try {
              // Log to backend
              await fetch('/api/pos/audit/override', {
                method: 'POST',
                headers: {
                  'Content-Type': 'application/json',
                  'Authorization': `Bearer ${localStorage.getItem('token')}`
                },
                body: JSON.stringify({
                  action: managerAction?.name || 'Unknown Action',
                  reason: 'Manager PIN authenticated',
                  details: `Terminal: ${terminalId}, Cashier: ${cashierId}`
                })
              });
            } catch (err) {
              console.warn('Failed to audit log manager override', err);
            }
            managerAction?.callback(pin);
            setManagerAction(null);
        }}
      />

      <OpenShiftModal
        isOpen={isOpenShiftModalOpen}
        onOpenShift={handleOpenShift}
      />

      <CloseShiftModal
        isOpen={isCloseShiftModalOpen}
        onClose={() => setCloseShiftModalOpen(false)}
        onCloseShift={handleCloseShift}
      />

      <SalesReturnModal
        isOpen={isReturnModalOpen}
        onClose={() => setReturnModalOpen(false)}
        user={user || undefined}
        requestManagerOverride={requestManagerOverride}
      />

      {/* Batch Selection Modal (matching Sigma POS Image 1) */}
      {batchModalData && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-xl shadow-2xl max-w-xl w-full p-6 border border-slate-200">
            <div className="flex justify-between items-center pb-3 border-b border-slate-200">
              <div>
                <h3 className="text-xl font-black text-slate-800 underline decoration-slate-400">Batch Selection:</h3>
                <p className="text-sm font-bold text-slate-600 mt-1">Product Name: <span className="text-slate-900 font-extrabold">{batchModalData.product.name}</span></p>
              </div>
              <button 
                onClick={() => setBatchModalData(null)} 
                className="text-slate-400 hover:text-slate-600 font-bold text-xl px-2"
              >
                ✕
              </button>
            </div>

            <div className="mt-4 overflow-x-auto border border-slate-400 rounded-lg">
              <table className="w-full text-left border-collapse">
                <thead className="bg-slate-200">
                  <tr className="border-b border-slate-400 text-xs font-black text-slate-800">
                    <th className="p-2.5 border-r border-slate-400 text-center">Batch No</th>
                    <th className="p-2.5 border-r border-slate-400 text-center">Unit</th>
                    <th className="p-2.5 border-r border-slate-400 text-right">MRP</th>
                    <th className="p-2.5 border-r border-slate-400 text-right">Sales Rate1</th>
                    <th className="p-2.5 text-right">Stock</th>
                  </tr>
                </thead>
                <tbody>
                  {batchModalData.batches.map((batch: any, index: number) => {
                    const mrpVal = batch.mrp || batchModalData.product.mrp || batchModalData.product.sellingPrice;
                    const priceVal = batch.sellingPrice || batchModalData.product.sellingPrice;
                    const stockVal = batch.currentStock ?? 0;
                    const isSelected = index === selectedBatchIndex;

                    return (
                      <tr 
                        key={batch.id || index}
                        onClick={() => {
                          addProductToCart(batchModalData.product, batchModalData.overrideQty, batch);
                          setBatchModalData(null);
                        }}
                        onMouseEnter={() => setSelectedBatchIndex(index)}
                        className={`border-b border-slate-300 cursor-pointer font-bold text-xs transition transform ${
                          isSelected
                            ? 'bg-red-700 text-white ring-4 ring-indigo-600 ring-offset-1 z-10 scale-[1.01] shadow-lg' 
                            : index === 0 
                              ? 'bg-red-600 text-white hover:bg-red-700' 
                              : 'bg-orange-500 text-white hover:bg-orange-600'
                        }`}
                      >
                        <td className="p-2.5 border-r border-white/30 text-center font-black">
                          <span className="inline-flex items-center justify-center gap-1">
                            {isSelected && <span className="text-yellow-300 font-black text-sm">▶</span>}
                            {batch.batchNumber || 'DEFAULT'}
                          </span>
                        </td>
                        <td className="p-2.5 border-r border-white/30 text-center">Nos</td>
                        <td className="p-2.5 border-r border-white/30 text-right font-black">₹{mrpVal.toFixed(2)}</td>
                        <td className="p-2.5 border-r border-white/30 text-right font-black">₹{priceVal.toFixed(2)}</td>
                        <td className="p-2.5 text-right font-black">
                          {stockVal.toFixed(2)}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="mt-5 pt-3 border-t border-slate-100 flex justify-between items-center">
              <span className="text-xs text-indigo-700 font-extrabold flex items-center gap-1 bg-indigo-50 px-2.5 py-1 rounded-md border border-indigo-200">
                <span>⌨ Use <kbd className="bg-white border px-1 rounded shadow-xs">↑</kbd> <kbd className="bg-white border px-1 rounded shadow-xs">↓</kbd> Arrow keys & press <kbd className="bg-white border px-1 rounded shadow-xs">Enter</kbd> or <kbd className="bg-white border px-1 rounded shadow-xs">Space</kbd> to select batch</span>
              </span>
              <button
                onClick={() => setBatchModalData(null)}
                className="px-4 py-2 bg-slate-200 hover:bg-slate-300 text-slate-800 font-bold text-xs rounded-lg transition"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};
