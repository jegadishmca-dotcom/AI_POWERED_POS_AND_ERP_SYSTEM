-- Migration 34: Seed Default Loyalty Program Configuration
-- Fixes: Empty loyalty_program_configs table causing 0 points awarded on POS checkout

INSERT INTO loyalty_program_configs (
    id, 
    is_active_config, 
    earn_ratio_spend_amount, 
    earn_ratio_points, 
    redeem_ratio_points, 
    redeem_ratio_discount_amount, 
    max_redemption_percentage_per_invoice, 
    max_redemption_per_day, 
    max_manual_adjustment_per_day, 
    max_bonus_allocation_per_customer, 
    enable_auto_tier_evaluation, 
    enable_point_expiry, 
    expiry_months, 
    birthday_bonus_points, 
    anniversary_bonus_points, 
    updated_at
) 
VALUES (
    '99999999-9999-9999-9999-999999999999', 
    true, 
    100.00, 
    1.00, 
    100.00, 
    10.00, 
    20.00, 
    1000.00, 
    500.00, 
    2000.00, 
    true, 
    true, 
    12, 
    50.00, 
    100.00, 
    NOW()
) 
ON CONFLICT (id) DO NOTHING;
