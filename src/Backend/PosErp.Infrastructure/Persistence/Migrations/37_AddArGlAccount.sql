-- Migration 37: Add GL account 10400 - Trade Receivables (AR)
-- Required by F02: credit-sale AR postings must debit an ASSET account,
-- not the Customer Wallet Liabilities (20200) account which CreateInvoiceCommand.cs
-- was incorrectly resolving via the same "LIABILITY / Wallet" lookup.
--
-- 10400 is a child of 10000 (Current Assets), consistent with the
-- existing chart of accounts hierarchy in migration 12 / migration 35.
-- Account code 10400 verified absent from all existing migrations before insertion.

INSERT INTO accounts (account_code, name, account_type, parent_account_id)
VALUES (
    '10400',
    'Trade Receivables (AR)',
    'ASSET',
    (SELECT id FROM accounts WHERE account_code = '10000')
)
ON CONFLICT (account_code) DO NOTHING;
