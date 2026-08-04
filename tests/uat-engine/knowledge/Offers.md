# Offers Module Knowledge Base

## Module Overview
The Offers module manages promotions, discounts, and coupons.

## Configurable Business Policies
- **Discount Allocation Policy**: Item-level vs Header-level distribution.

## Business Rules

### OFF-01: Active Check
- **Module**: Offers
- **Priority**: High
- **Rule Type**: Validation
- **Configurable**: No
- **Source**: `Offer.cs`
- **Applies To**: Cart
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Offer is applicable only if active and within date range.

### OFF-02: Minimum Order
- **Module**: Offers
- **Priority**: Medium
- **Rule Type**: Validation
- **Configurable**: No
- **Source**: `Offer.cs`
- **Applies To**: Cart
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Pre-discount subtotal must exceed minimum order amount.

### OFF-03: Max Discount Cap
- **Module**: Offers
- **Priority**: Medium
- **Rule Type**: Validation
- **Configurable**: No
- **Source**: `Offer.cs`
- **Applies To**: Discount Engine
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Calculated discount cannot exceed `MaxDiscountAmount`.

### OFF-04: Exclusivity
- **Module**: Offers
- **Priority**: High
- **Rule Type**: Logic
- **Configurable**: No
- **Source**: `Offer.cs`
- **Applies To**: Discount Engine
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: If `IsExclusive == true`, no other offers can be applied.

### OFF-05: Promo Codes
- **Module**: Offers
- **Priority**: Medium
- **Rule Type**: Process
- **Configurable**: No
- **Source**: `Offer.cs`
- **Applies To**: Discount Engine
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Offers with a promo code require explicit input.

## Expected Behaviour
- Cashier enters code, system updates `NetPayable`.
- System auto-applies best non-promo offers.
