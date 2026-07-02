-- Migration 38: Add GL account 20300 - Loyalty Points Liability
-- Required by F03: loyalty points redemption value must be posted to a dedicated
-- liability account, separate from the Customer Wallet Liabilities (20200) account.
--
-- 20300 is a child of 20000 (Current Liabilities), consistent with the
-- existing chart of accounts hierarchy in migration 12 / migration 35.

INSERT INTO accounts (account_code, name, account_type, parent_account_id)
VALUES (
    '20300',
    'Loyalty Points Liability',
    'LIABILITY',
    (SELECT id FROM accounts WHERE account_code = '20000')
)
ON CONFLICT (account_code) DO NOTHING;
