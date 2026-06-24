# RC1 User Acceptance Testing (UAT) Scripts

## 1. Cashier UAT Workflow

### Scenario: Basic Billing & Payment
- [ ] Login using Cashier credentials. Verify access is restricted to POS Terminal UI.
- [ ] Scan a Barcode (1D/2D). Ensure product is added to the cart instantly.
- [ ] Manually search for a product and add it to the cart.
- [ ] Adjust quantity using the touch numpad or scanner increment.
- [ ] Complete payment via Cash. Verify exact change calculation is displayed.
- [ ] Print Receipt (ESC/POS trigger). Ensure Cash Drawer opens.

### Scenario: Cart Management & Returns
- [ ] Add 3 items to the cart. Click "Hold Cart" and provide a customer reference.
- [ ] Serve the next customer, complete their billing.
- [ ] Click "Resume Cart", select the held cart. Verify all 3 items persist.
- [ ] Process a Return (Negative quantity or explicit return flow). Verify inventory increments properly.

### Scenario: Loyalty Redemption
- [ ] Search Customer by Phone Number.
- [ ] View available points balance.
- [ ] Apply points for a ₹100 discount. Verify the total amount updates correctly.

---

## 2. Manager UAT Workflow

### Scenario: Procurement & GRN
- [ ] Login using Manager credentials.
- [ ] View the Procurement Dashboard. Review the AI-generated Reorder Recommendations.
- [ ] Convert a Recommendation into a Draft Purchase Order (PO).
- [ ] Process a Goods Receipt Note (GRN) against the PO.
- [ ] Verify the Stock Ledger successfully recorded the inventory increase.

### Scenario: Promotions & Loyalty
- [ ] Create a "Buy 1 Get 1 Free" Offer for a specific Category.
- [ ] Log out, log in as Cashier, and verify the offer applies at the POS.
- [ ] Review the Fast-Moving / Slow-Moving Inventory dashboards.

---

## 3. Owner UAT Workflow

### Scenario: Executive Dashboards
- [ ] Login using Owner credentials.
- [ ] Navigate to the Executive Dashboard. Verify Revenue and Profit calculations for "Today".
- [ ] Navigate to AI Insights. "Acknowledge" an Overstock Risk alert and track its lifecycle.

### Scenario: System Health & Audit
- [ ] Open the System Health Dashboard. Verify Database and Redis show "Healthy".
- [ ] Navigate to the Audit Log. Validate that Cashier Logins and Price Overrides are recorded with IP and TenantId.
