-- ==============================================================================
-- PHASE 3: SEED OFFERS
-- ==============================================================================
INSERT INTO offers (name, description, offer_type, promo_code, priority, is_stackable, is_exclusive, start_date, end_date, rules_json)
VALUES 
(
    '10% Off Grocery Bill', 
    'Get 10% off on all grocery items for bills above 1000', 
    'PERCENTAGE', 
    NULL, 
    1, 
    TRUE, 
    FALSE, 
    CURRENT_TIMESTAMP, 
    CURRENT_TIMESTAMP + INTERVAL '30 days',
    '{"Conditions": {"MinCartValue": 1000}, "Reward": {"DiscountType": "Percentage", "Value": 10, "ApplyTo": "BILL", "MaxDiscountAmount": 500}}'
),
(
    'FESTIVAL500 - Flat 500 Off', 
    'Exclusive flat 500 off on total bill. Cannot be combined with other offers.', 
    'FLAT', 
    'FESTIVAL500', 
    10, 
    FALSE, 
    TRUE, 
    CURRENT_TIMESTAMP, 
    CURRENT_TIMESTAMP + INTERVAL '30 days',
    '{"Conditions": {"MinCartValue": 2000}, "Reward": {"DiscountType": "FlatAmount", "Value": 500, "ApplyTo": "BILL"}}'
);
