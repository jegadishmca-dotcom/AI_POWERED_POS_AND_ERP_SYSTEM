# Knowledge Base Traceability Matrix

This matrix maps documented business rules to their origin in the ERP system to ensure the AI QA Platform is testing verified, real-world constraints.

| Rule ID | Module | Source / Origin | Knowledge Document | Rule Dependencies | Future Scenario(s) |
|---------|--------|-----------------|--------------------|-------------------|--------------------|
| **POS-01** | POS | `StoreBusinessDate.cs` | `POS.md` | GLB-01 | `SCEN-POS-001` |
| **POS-02** | POS | `PosSession.cs` | `POS.md` | GLB-10 | `SCEN-POS-002` |
| **POS-03** | POS | `CreateProductCommand...`| `POS.md` | None | `SCEN-POS-003` |
| **POS-04** | POS | `layer4_accounting.py` | `POS.md` | GST-02, LOY-03 | `SCEN-POS-004` |
| **POS-05** | POS | `Invoice` entity | `POS.md` | POS-04 | `SCEN-POS-005` |
| **POS-06** | POS | ERP Accounting Std | `POS.md` | None | `SCEN-POS-006` |
| **INV-01** | Inventory | `layer3_workflows.py` | `Inventory.md` | PUR-02, POS-06 | `SCEN-INV-001` |
| **INV-02** | Inventory | `StockLedgerEntry.cs` | `Inventory.md` | None | `SCEN-INV-002` |
| **INV-03** | Inventory | `CreateProductCommand...`| `Inventory.md` | None | `SCEN-INV-003` |
| **INV-04** | Inventory | `layer3_workflows.py` | `Inventory.md` | INV-01 | `SCEN-INV-004` |
| **INV-05** | Inventory | `ProductBatch.cs` | `Inventory.md` | None | `SCEN-INV-005` |
| **CRM-01** | CRM | `workflow_2_customer_crm`| `CRM.md` | None | `SCEN-CRM-001` |
| **CRM-02** | CRM | `Customer.cs` | `CRM.md` | None | `SCEN-CRM-002` |
| **CRM-03** | CRM | `Customer.cs` | `CRM.md` | None | `SCEN-CRM-003` |
| **CRM-04** | CRM | `WalletLedgerEntry.cs` | `CRM.md` | GLB-01 | `SCEN-CRM-004` |
| **CRM-05** | CRM | `CustomerTier.cs` | `CRM.md` | LOY-02 | `SCEN-CRM-005` |
| **FIN-01** | Finance | `layer4_accounting.py` | `Finance.md` | POS-04, PUR-03 | `SCEN-FIN-001` |
| **FIN-02** | Finance | `layer4_accounting.py` | `Finance.md` | None | `SCEN-FIN-002` |
| **FIN-03** | Finance | `JournalEntry.cs` | `Finance.md` | None | `SCEN-FIN-003` |
| **FIN-04** | Finance | Accounting Standard | `Finance.md` | None | `SCEN-FIN-004` |
| **GST-01** | GST | `TaxSlab.cs` | `GST.md` | None | `SCEN-GST-001` |
| **GST-02** | GST | `layer3_workflows.py` | `GST.md` | GST-01 | `SCEN-GST-002` |
| **GST-03** | GST | GST Act Rules | `GST.md` | None | `SCEN-GST-003` |
| **GST-04** | GST | `TaxTransaction.cs` | `GST.md` | None | `SCEN-GST-004` |
| **PUR-01** | Purchasing | `GRNHeader.cs` | `Purchasing.md` | None | `SCEN-PUR-001` |
| **PUR-02** | Purchasing | Inventory Integration | `Purchasing.md` | INV-02 | `SCEN-PUR-002` |
| **PUR-03** | Purchasing | Finance Integration | `Purchasing.md` | INV-02 | `SCEN-PUR-003` |
| **PUR-04** | Purchasing | `GRNItem.cs` | `Purchasing.md` | None | `SCEN-PUR-004` |
| **PUR-05** | Purchasing | `ProductBatch.cs` | `Purchasing.md` | None | `SCEN-PUR-005` |
| **LOY-01** | Loyalty | `LoyaltyLedgerEntry.cs` | `Loyalty.md` | CRM-04 | `SCEN-LOY-001` |
| **LOY-02** | Loyalty | `workflow_4_loyalty...` | `Loyalty.md` | None | `SCEN-LOY-002` |
| **LOY-03** | Loyalty | `LoyaltyProgramConfig.cs`| `Loyalty.md` | None | `SCEN-LOY-003` |
| **LOY-04** | Loyalty | Return processing | `Loyalty.md` | None | `SCEN-LOY-004` |
| **OFF-01** | Offers | `Offer.cs` | `Offers.md` | None | `SCEN-OFF-001` |
| **OFF-02** | Offers | `Offer.cs` | `Offers.md` | None | `SCEN-OFF-002` |
| **OFF-03** | Offers | `Offer.cs` | `Offers.md` | None | `SCEN-OFF-003` |
| **OFF-04** | Offers | `Offer.cs` | `Offers.md` | None | `SCEN-OFF-004` |
| **OFF-05** | Offers | `Offer.cs` | `Offers.md` | None | `SCEN-OFF-005` |
| **REP-01** | Reports | `layer2_api_smoke.py` | `Reports.md` | GLB-10 | `SCEN-REP-001` |
| **REP-02** | Reports | Reporting Standards | `Reports.md` | FIN-03 | `SCEN-REP-002` |
| **REP-03** | Reports | Finance Valuation | `Reports.md` | None | `SCEN-REP-003` |
| **SEC-01** | Security | `AuthController.cs` | `Security.md` | None | `SCEN-SEC-001` |
| **SEC-02** | Security | `AuthController.cs` | `Security.md` | None | `SCEN-SEC-002` |
| **SEC-03** | Security | `AuthController.cs` | `Security.md` | None | `SCEN-SEC-003` |
| **SEC-04** | Security | Configuration | `Security.md` | None | `SCEN-SEC-004` |
| **SEC-05** | Security | `AuthController.cs` | `Security.md` | None | `SCEN-SEC-005` |
| **GLB-01** | Global | `StoreBusinessDate.cs` | `GlobalBusinessRules.md`| None | `SCEN-GLB-001` |
| **GLB-02** | Global | DB Schema | `GlobalBusinessRules.md`| None | `SCEN-GLB-002` |
| **GLB-03** | Global | `CreateInvoiceCommand.cs`| `GlobalBusinessRules.md`| None | `SCEN-GLB-003` |
| **GLB-04** | Global | DB Setup | `GlobalBusinessRules.md`| None | `SCEN-GLB-004` |
| **GLB-05** | Global | `DocumentSequence.cs` | `GlobalBusinessRules.md`| None | `SCEN-GLB-005` |
| **GLB-06** | Global | Entities `IsDeleted` | `GlobalBusinessRules.md`| None | `SCEN-GLB-006` |
| **GLB-07** | Global | Base Entities | `GlobalBusinessRules.md`| None | `SCEN-GLB-007` |
| **GLB-08** | Global | `User.cs`, `Role.cs` | `GlobalBusinessRules.md`| None | `SCEN-GLB-008` |
| **GLB-09** | Global | `Terminal.cs` | `GlobalBusinessRules.md`| None | `SCEN-GLB-009` |
| **GLB-10** | Global | `PosSession.cs` | `GlobalBusinessRules.md`| None | `SCEN-GLB-010` |
| **GLB-11** | Global | `WorkflowApproval` | `GlobalBusinessRules.md`| None | `SCEN-GLB-011` |
