# SLS-001 Cash Sale Manual Flow

## Preconditions
- The cashier has valid login credentials.
- The POS terminal is powered on and connected to the ERP network.
- The product (e.g., Apple) is registered in the system with valid pricing and active inventory.
- The cash drawer has sufficient float for providing change if needed.

## Main Flow
1. **Login:** The cashier navigates to the POS login screen, enters their credentials, and clicks the Login button.
2. **Open Shift:** The cashier enters the starting float amount for the cash drawer and opens the shift.
3. **Navigate to Billing:** The cashier selects the POS Billing or Register screen from the dashboard.
4. **Scan Item:** The cashier scans the product barcode (or manually enters the SKU).
5. **Verify Line Item:** The cashier verifies that the scanned item appears in the cart with the correct unit price and applied GST.
6. **Tender Payment:** The cashier clicks the 'Pay' or 'Checkout' button.
7. **Select Payment Method:** The cashier selects 'Cash' as the payment method.
8. **Receive Cash:** The cashier enters the exact amount of cash handed over by the customer (or a greater amount to calculate change).
9. **Finalize Transaction:** The cashier confirms the payment to complete the sale.

## Expected Result
- The system successfully processes the payment.
- The cash drawer opens automatically.
- A physical or digital receipt is generated with correct item details, total amount, and GST breakdown.
- The inventory level for the sold product is reduced by the quantity sold.
- The financial ledger records the cash sale accurately.
