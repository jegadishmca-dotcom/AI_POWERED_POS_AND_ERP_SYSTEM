import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award, Tag, Trash2, PlusCircle, MinusCircle, Hand, ShieldAlert, Printer, Clock, Maximize, Minimize, Mic, MicOff, Unlock } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';
import { PaymentModal } from './PaymentModal';
import { searchProducts } from '../../catalog/api/catalog.api';
import { searchCustomers, registerCustomer } from '../../crm/api/crm.api';
import { createInvoice, closeShift, getZReport, getProductBatches, getCurrentSession, openSession, calculateCart, getActiveBusinessDate, openBusinessDate, holdInvoice } from '../api/pos.api';
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
import { posDb } from '../db/pos.db';
import { useAuthStore } from '../../auth/store/auth.store';
import { syncInvoices } from '../api/pos.sync';

export const PosTerminal = () => {
  const [customer, setCustomer] = useState<any>(null);
  const [customerQuery, setCustomerQuery] = useState('');
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  const [promoCode, setPromoCode] = useState('');
  const [isPaymentModalOpen, setPaymentModalOpen] = useState(false);
  const [completedInvoice, setCompletedInvoice] = useState<any>(null);
  
  const [isProcessing, setIsProcessing] = useState(false);
  
  // Modals & Hooks State
  const [isHoldModalOpen, setHoldModalOpen] = useState(false);
  const [isReprintModalOpen, setReprintModalOpen] = useState(false);
  const [isManagerModalOpen, setManagerModalOpen] = useState(false);
  const [managerAction, setManagerAction] = useState<any>(null);
  const customerInputRef = useRef<HTMLInputElement>(null);
  const productInputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const isVoiceSearchingRef = useRef(false);

  // Shift Management State
  const [activeSession, setActiveSession] = useState<any>(null);
  const [isOpenShiftModalOpen, setOpenShiftModalOpen] = useState(false);
  const [isCloseShiftModalOpen, setCloseShiftModalOpen] = useState(false);
  const { user } = useAuthStore();
  const terminalId = localStorage.getItem('pos_terminal_id') || '00000000-0000-0000-0000-000000000001';
  const cashierId = user?.id || '00000000-0000-0000-0000-000000000001';

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

  // Fullscreen management & auto-fullscreen on first user interaction
  const [isFullscreen, setIsFullscreen] = useState(false);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);

    const autoFullscreen = () => {
      if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen().catch(() => {
          // ignore blocked request
        });
      }
      window.removeEventListener('click', autoFullscreen);
      window.removeEventListener('keydown', autoFullscreen);
    };

    window.addEventListener('click', autoFullscreen);
    window.addEventListener('keydown', autoFullscreen);

    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      window.removeEventListener('click', autoFullscreen);
      window.removeEventListener('keydown', autoFullscreen);
    };
  }, []);

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

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().catch((err) => {
        console.error(`Error attempting to enable fullscreen: ${err.message}`);
      });
    } else {
      document.exitFullscreen();
    }
  };

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

  // Dynamic Cart State initializing empty
  const [cart, setCart] = useState<any>({
    items: [],
    subtotal: 0,
    totalDiscount: 0,
    taxTotal: 0,
    finalTotal: 0,
    appliedOfferNames: []
  });
  const [suppressOffers, setSuppressOffers] = useState(false);
  const [pointsRedeemed, setPointsRedeemed] = useState<number>(0);

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

  const addProductToCart = async (product: any, overrideQty?: number) => {
    const existing = cart.items.find((item: any) => item.productId === product.id);
    let updatedItems = [];
    const qtyToAdd = overrideQty !== undefined ? overrideQty : 1;

    if (existing) {
      updatedItems = cart.items.map((item: any) =>
        item.productId === product.id 
          ? { ...item, qty: item.qty + qtyToAdd, lineTotal: (item.qty + qtyToAdd) * item.unitPrice } 
          : item
      );
      recalculateCart(updatedItems);
    } else {
      const newItem = {
        id: crypto.randomUUID(), // CQ-04 FIX: use crypto.randomUUID() instead of Math.random().toString()
        productId: product.id,
        name: product.name,
        qty: qtyToAdd,
        unitPrice: product.sellingPrice,
        lineTotal: product.sellingPrice * qtyToAdd,
        discountAmount: 0,
        finalLineTotal: product.sellingPrice * qtyToAdd,
        appliedOfferName: null,
        cgstRate: product.cgstRate || 0,
        sgstRate: product.sgstRate || 0,
        cessRate: product.cessRate || 0,
        isWeighable: product.isWeighable || false,
        batches: [],
        batchId: undefined
      };

      updatedItems = [...cart.items, newItem];
      recalculateCart(updatedItems);

      // Fetch batches asynchronously in the background
      try {
        const fetchedBatches = await getProductBatches(product.id);
        if (fetchedBatches && fetchedBatches.length > 0) {
          setCart((prev: any) => ({
            ...prev,
            items: prev.items.map((i: any) => 
              i.productId === product.id 
                ? { ...i, batches: fetchedBatches, batchId: fetchedBatches[0].id } 
                : i
            )
          }));
        }
      } catch (err) {
        console.warn('Failed to fetch batches for product', err);
      }
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
    const updatedItems = cart.items.map((item: any) => {
      if (item.productId === productId) {
        const newQty = Math.max(1, item.qty + delta);
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

  // Barcode Scanner Integration
  useBarcodeScanner(async (barcode: string, weight?: number) => {
    try {
        const results = await searchProducts(barcode);
        const product = results.find(p => p.primaryBarcode === barcode || p.productCode === barcode);
        if (product) {
            addProductToCart(product, weight);
        } else {
            alert('Barcode not found: ' + barcode);
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
      if (cart.items.length > 0) setPaymentModalOpen(true);
    },
    onF9Park: () => {
      handleHoldCart();
    },
    onF10Reprint: () => setReprintModalOpen(true)
  });

  const handleHoldCart = async () => {
    if (cart.items.length === 0) {
      setHoldModalOpen(true);
      return;
    }
    
    const uuid = crypto.randomUUID();
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
             id: i.id || crypto.randomUUID(),
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

  const requestManagerOverride = (action: string, onSuccess: () => void) => {
      setManagerAction({ name: action, callback: onSuccess });
      setManagerModalOpen(true);
  };

  const handleProductSearch = async (e: React.KeyboardEvent<HTMLInputElement>) => {
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

      // If dropdown is open and an item is focused, select it
      if (showProductDropdown && focusedProductIndex >= 0 && focusedProductIndex < searchResults.length) {
        addProductToCart(searchResults[focusedProductIndex], voiceQuantity || undefined);
        setVoiceQuantity(null);
        setProductQuery('');
        setSearchResults([]);
        setShowProductDropdown(false);
        setFocusedProductIndex(-1);
        return;
      }

      const val = productQuery.trim();
      if (!val) return;

      try {
        const results = await searchProducts(val);
        if (results.length === 1) {
          addProductToCart(results[0], voiceQuantity || undefined);
          setVoiceQuantity(null);
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
          setFocusedProductIndex(-1);
        } else if (results.length > 1) {
          setSearchResults(results);
          setShowProductDropdown(true);
          setFocusedProductIndex(0); // auto-focus first item
        } else {
          alert('Product not found.');
        }
      } catch (err) {
        console.error('Error searching products:', err);
      }
    }
  };

  const handleCustomerSearch = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      const val = customerQuery.trim();
      if (!val) return;
      try {
        const results = await searchCustomers(val);
        if (results.length > 0) {
          const cust = results[0];
          setCustomer({
            id: cust.id,
            name: cust.name,
            phone: cust.phone,
            walletBalance: cust.walletBalance,
            points: cust.loyaltyPoints,
            tier: cust.tierName
          });
        } else {
          alert('Customer not found. Click "+" to register a new customer!');
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
              className="w-full pl-10 p-2 rounded-l border border-indigo-200 outline-none focus:ring-2 ring-indigo-500 font-bold"
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
              <Search className="absolute left-3 top-2.5 text-slate-400 w-5 h-5" />
              <input 
                ref={productInputRef}
                type="text"
                placeholder="F2: Scan Barcode or Type Product Name (Press Enter)..."
                className="w-full pl-10 pr-4 py-2 border border-slate-300 rounded-lg outline-none focus:ring-2 focus:ring-blue-500 font-bold text-slate-850"
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
              className="px-3 py-2 border border-slate-300 rounded-lg bg-white text-xs font-black text-slate-700 hover:bg-slate-50 transition-colors shadow-sm whitespace-nowrap min-w-[70px]"
              title="Voice Language (மொழி)"
            >
              {voiceLanguage === 'ta-IN' ? 'தமிழ்' : 'English'}
            </button>

            {/* Mic Toggle Button */}
            <button
              onClick={toggleListening}
              className={`p-2 rounded-lg border transition-all flex items-center justify-center shadow-sm ${
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
        <div className="p-0 flex-1 overflow-y-auto">
          {cart.items.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center text-slate-400">
              <ShoppingCart className="w-16 h-16 mb-2 stroke-1" />
              <p className="font-semibold">Billing cart is empty</p>
              <p className="text-xs">Scan items or search products above to start checkout</p>
            </div>
          ) : (
            <table className="w-full text-left">
              <thead className="bg-slate-100 sticky top-0 border-b">
                <tr>
                  <th className="p-3">Item</th>
                  <th className="p-3 text-center">Qty</th>
                  <th className="p-3 text-right">Price</th>
                  <th className="p-3 text-right">Total</th>
                  <th className="p-3 text-center w-16"></th>
                </tr>
              </thead>
              <tbody>
                {cart.items.map((item: any) => (
                  <tr key={item.id} className="border-b hover:bg-slate-50/50">
                    <td className="p-3">
                      <p className="font-bold text-slate-800">{item.name}</p>
                      {item.appliedOfferName && (
                        <p className="text-xs text-emerald-600 flex items-center font-bold bg-emerald-50 w-max px-2 py-0.5 rounded mt-1">
                          <Tag className="w-3 h-3 mr-1" /> {item.appliedOfferName}
                        </p>
                      )}
                      {item.batches && item.batches.length > 0 && (
                        <div className="mt-2 flex items-center gap-1.5">
                          <span className="text-[10px] font-bold text-indigo-600 bg-indigo-50 px-1 py-0.5 rounded">Batch</span>
                          <select
                            className="text-xs font-semibold text-slate-700 bg-slate-50 border border-slate-200 rounded p-1 outline-none focus:ring-1 focus:ring-indigo-500 max-w-[200px]"
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
                    <td className="p-3">
                      <div className="flex items-center justify-center gap-2">
                        <button onClick={() => updateItemQty(item.productId, -1)} className="text-slate-400 hover:text-indigo-600 transition">
                          <MinusCircle className="w-6 h-6" />
                        </button>
                        <input 
                          type="number"
                          min="1"
                          // GAP-05 FIX: enforce integer quantities for non-weighable products
                          step={item.isWeighable ? 'any' : '1'}
                          className="font-black text-lg w-16 text-center border border-slate-200 rounded focus:outline-none focus:border-indigo-500 bg-white"
                          value={item.qty === '' ? '' : item.qty}
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
                        <button onClick={() => updateItemQty(item.productId, 1)} className="text-slate-400 hover:text-indigo-600 transition">
                          <PlusCircle className="w-6 h-6" />
                        </button>
                      </div>
                    </td>
                    <td className="p-3 text-right font-medium">₹{item.unitPrice.toFixed(2)}</td>
                    <td className="p-3 text-right">
                      {item.discountAmount > 0 && <p className="text-xs text-slate-400 line-through">₹{item.lineTotal.toFixed(2)}</p>}
                      <p className="font-black text-xl text-slate-800">₹{item.finalLineTotal.toFixed(2)}</p>
                    </td>
                    <td className="p-3 text-center">
                      <button onClick={() => requestManagerOverride('Void Item', () => removeItem(item.productId))} className="text-slate-300 hover:text-red-500 transition" title="Void Item (Manager)">
                        <Trash2 className="w-5 h-5" />
                      </button>
                    </td>
                  </tr>
                ))}
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
            <span>Total</span><span>₹{cart.finalTotal.toFixed(2)}</span>
          </div>

          {/* Payment Methods */}
          <div className="mt-4">
            <button 
              disabled={cart.items.length === 0}
              className="w-full bg-emerald-600 text-white p-4 rounded-lg font-black text-2xl shadow-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center transition-colors mb-4" 
              onClick={() => setPaymentModalOpen(true)}
            >
              <CreditCard className="w-8 h-8 mr-3" /> PAYMENT (F11)
            </button>
            <div className="flex gap-3">
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
        cartTotal={cart.finalTotal}
        isProcessing={isProcessing}
        customer={customer}
        onCompletePayment={async (tenders: any) => {
          try {
            setIsProcessing(true);
            // Generate dynamic invoice payload matching CreateInvoiceCommand
            const roundOffVal = +(Math.round(cart.finalTotal) - cart.finalTotal).toFixed(2);
            const netPayableVal = Math.round(cart.finalTotal);
            const paymentModeVal = tenders.cash > 0 && (tenders.upi > 0 || tenders.card > 0) ? 'SPLIT'
                                 : tenders.cash > 0 ? 'CASH'
                                 : tenders.upi > 0  ? 'UPI'
                                 : tenders.card > 0 ? 'CARD'
                                 : 'WALLET';
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
              items: cart.items.map((item: any) => ({
                productId: item.productId,
                quantity: item.qty,
                unitPrice: item.unitPrice,
                batchId: item.batchId || undefined
              }))
            };

            // 1. Calculate offline loyalty estimation
            const oldLoyaltyPoints = customer ? (customer.points || 0) : 0;
            let offlineLoyaltyEarned = customer ? Math.floor(cart.finalTotal / 100) : 0;
            let offlineLoyaltyBalance = oldLoyaltyPoints + offlineLoyaltyEarned;

            // 2. Construct the FULL invoice object for local storage and printing
            const invoiceId = crypto.randomUUID();
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
              status: 'COMPLETED',
              items: cart.items.map((item: any) => ({
                id: item.id || crypto.randomUUID(),
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
              await createInvoice(payload);
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
                    await createInvoice(retryPayload);
                  } catch (retryErr: any) {
                    const retryErrorText = retryErr?.response?.data?.message || retryErr?.response?.data?.detailed || retryErr?.response?.data?.Detailed || retryErr?.response?.data?.Message || retryErr?.message || "Invalid PIN.";
                    alert("Override Failed: " + retryErrorText);
                    throw new Error(retryErrorText);
                  }
                } else {
                  throw new Error("Checkout blocked: Insufficient stock and no override PIN provided.");
                }
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

    </div>
  );
};
