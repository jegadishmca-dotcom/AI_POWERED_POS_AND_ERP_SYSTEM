-- ==============================================================================
-- PHASE 4: RETAIL CHART OF ACCOUNTS SEED
-- ==============================================================================
-- Assets
INSERT INTO accounts (account_code, name, account_type) VALUES ('10000', 'Current Assets', 'ASSET') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('10100', 'Main Cash Register', 'ASSET', (SELECT id FROM accounts WHERE account_code = '10000')),
('10200', 'HDFC Current A/C', 'ASSET', (SELECT id FROM accounts WHERE account_code = '10000')),
('10300', 'Inventory Asset', 'ASSET', (SELECT id FROM accounts WHERE account_code = '10000'))
ON CONFLICT (account_code) DO NOTHING;

-- Liabilities
INSERT INTO accounts (account_code, name, account_type) VALUES ('20000', 'Current Liabilities', 'LIABILITY') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('20100', 'Accounts Payable - Vendors', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '20000')),
('20200', 'Customer Wallet Liabilities', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '20000')),
('22000', 'GST Payable', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '20000'))
ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('22010', 'Output CGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000')),
('22020', 'Output SGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000')),
('22030', 'Input CGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000')),
('22040', 'Input SGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000'))
ON CONFLICT (account_code) DO NOTHING;

-- Equity
INSERT INTO accounts (account_code, name, account_type) VALUES ('30000', 'Owner Equity', 'EQUITY') ON CONFLICT (account_code) DO NOTHING;

-- Revenue
INSERT INTO accounts (account_code, name, account_type) VALUES ('40000', 'Operating Revenue', 'REVENUE') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('40100', 'Sales - FMCG', 'REVENUE', (SELECT id FROM accounts WHERE account_code = '40000')),
('40200', 'Sales - Grocery', 'REVENUE', (SELECT id FROM accounts WHERE account_code = '40000'))
ON CONFLICT (account_code) DO NOTHING;

-- Expenses
INSERT INTO accounts (account_code, name, account_type) VALUES ('50000', 'Operating Expenses', 'EXPENSE') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('50100', 'Cost of Goods Sold (COGS)', 'EXPENSE', (SELECT id FROM accounts WHERE account_code = '50000')),
('50200', 'Staff Salary', 'EXPENSE', (SELECT id FROM accounts WHERE account_code = '50000')),
('50300', 'Electricity & Utilities', 'EXPENSE', (SELECT id FROM accounts WHERE account_code = '50000'))
ON CONFLICT (account_code) DO NOTHING;
