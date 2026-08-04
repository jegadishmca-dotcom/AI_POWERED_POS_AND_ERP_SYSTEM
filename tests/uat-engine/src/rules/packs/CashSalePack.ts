import { AllRule } from '../composition/CompositeRules';
import { InventoryNonNegativeRule } from '../library/invariants/InventoryNonNegativeRule';
import { DebitEqualsCreditRule } from '../library/invariants/DebitEqualsCreditRule';
import { IRulePackMetadata, RuleOwner } from '../interfaces';

export class CashSalePack extends AllRule {
  constructor() {
    super('PACK-CASH-SALE', [
      new InventoryNonNegativeRule(),
      new DebitEqualsCreditRule()
    ]);
  }

  public get packMetadata(): IRulePackMetadata {
    return {
      packId: 'PACK-CASH-SALE',
      name: 'Cash Sale Validation Pack',
      version: '1.0.0',
      owner: RuleOwner.POS,
      knowledgeAreas: ['POS', 'Inventory', 'Finance'],
      dependencies: [],
      estimatedRuntimeMs: 50
    };
  }
}
