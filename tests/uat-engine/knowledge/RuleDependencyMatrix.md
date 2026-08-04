# Rule Dependency Matrix

This matrix maps inter-module dependencies to ensure that testing one business rule correctly considers prerequisites from other modules.

| Dependent Rule | Description | Depends On | Dependency Description |
|----------------|-------------|------------|------------------------|
| **POS-01** (Business Date) | Invoice requires OPEN business date | **GLB-01** | Business Date Policy |
| **POS-04** (Cart Total) | NetPayable calculation | **GST-02** | Invoice Level Tax must be added |
| **POS-04** (Cart Total) | NetPayable calculation | **LOY-03** | Loyalty Redemption Limit |
| **POS-04** (Cart Total) | NetPayable calculation | **OFF-03** | Max Discount Cap |
| **POS-05** (Payment Split)| Tenders must equal NetPayable | **POS-04** | Cart Total Equation |
| **INV-01** (Stock Equation)| Stock equals ledger sum | **PUR-02** | GRN Stock Update |
| **INV-01** (Stock Equation)| Stock equals ledger sum | **POS-06** | Data Immutability (Sales deduction) |
| **INV-04** (Negative Stock)| Allows SALE_OVERRIDE | **INV-01** | Stock Equation evaluation |
| **FIN-01** (Double-Entry) | Journal balance | **POS-04** | Cart totals for Revenue |
| **FIN-01** (Double-Entry) | Journal balance | **PUR-03** | Financial Recognition for AP |
| **CRM-05** (Tier Eval) | Tier changes based on points | **LOY-02** | Earn Math |
| **PUR-03** (AP Recognition)| Debit Inv, Credit AP | **INV-02** | Immutable Ledger |
| **REP-01** (Z-Report) | End of shift cash balancing | **GLB-10** | Shift Rules (Expected Closing Cash) |
| **REP-02** (Data Freshness)| Reports reflect POSTED JEs | **FIN-03** | Immutability of Journal Entries |
| **LOY-01** (Loyalty Truth)| Running balance matches ledger | **CRM-04** | Customer Profile structure |
