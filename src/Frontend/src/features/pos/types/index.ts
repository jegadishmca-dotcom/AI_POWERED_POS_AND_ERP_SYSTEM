export interface CartItem {
  id: string; // uuid
  productId: string;
  barcode?: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  totalAmount: number;
}

export interface Invoice {
  id: string;
  businessDate: string;
  invoiceNumber: string;
  terminalId: string;
  terminalSequence: number;
  cashierId: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  roundOff: number;
  netPayable: number;
  paymentMode: string;
  status: 'PENDING' | 'COMPLETED' | 'HOLD';
  items: CartItem[];
  customer?: {
    id: string;
    name: string;
    phone?: string;
    [key: string]: any;
  } | null;
}
