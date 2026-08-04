import { InventoryNonNegativeRule } from '../../src/rules/library/invariants/InventoryNonNegativeRule';
import { DebitEqualsCreditRule } from '../../src/rules/library/invariants/DebitEqualsCreditRule';
import { CashSalePack } from '../../src/rules/packs/CashSalePack';
import { EvaluationContext } from '../../src/rules/context/EvaluationContext';

describe('ERP Rule Library', () => {
  let context: EvaluationContext;

  beforeEach(() => {
    context = new EvaluationContext('test-run-id');
  });

  test('InventoryNonNegativeRule passes when all stock >= 0', async () => {
    context.snapshots['postTxnInventory'] = [{ sku: 'A', stock: 10 }, { sku: 'B', stock: 0 }];
    const rule = new InventoryNonNegativeRule();
    const result = await rule.evaluate(context);
    expect(result.status).toBe('PASSED');
  });

  test('InventoryNonNegativeRule fails when stock < 0', async () => {
    context.snapshots['postTxnInventory'] = [{ sku: 'A', stock: 10 }, { sku: 'B', stock: -1 }];
    const rule = new InventoryNonNegativeRule();
    const result = await rule.evaluate(context);
    expect(result.status).toBe('FAILED');
    expect(result.explanation.actual).toContain('Negative stock found');
  });

  test('DebitEqualsCreditRule passes when debit == credit', async () => {
    context.snapshots['postTxnLedger'] = [{ debit: 100, credit: 0 }, { debit: 0, credit: 100 }];
    const rule = new DebitEqualsCreditRule();
    const result = await rule.evaluate(context);
    expect(result.status).toBe('PASSED');
  });

  test('DebitEqualsCreditRule fails when debit != credit', async () => {
    context.snapshots['postTxnLedger'] = [{ debit: 100, credit: 0 }, { debit: 0, credit: 90 }];
    const rule = new DebitEqualsCreditRule();
    const result = await rule.evaluate(context);
    expect(result.status).toBe('FAILED');
  });

  test('CashSalePack evaluates underlying rules properly', async () => {
    context.snapshots['postTxnInventory'] = [{ sku: 'A', stock: 5 }];
    context.snapshots['postTxnLedger'] = [{ debit: 50, credit: 50 }];
    const pack = new CashSalePack();
    const result = await pack.evaluate(context);
    expect(result.status).toBe('PASSED');
    
    // Fail the inventory rule inside the pack
    context.snapshots['postTxnInventory'] = [{ sku: 'A', stock: -5 }];
    const result2 = await pack.evaluate(context);
    expect(result2.status).toBe('FAILED');
  });
});
