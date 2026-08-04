import { IRule } from '../interfaces';

export class RuleRegistry {
  private rules: Map<string, IRule> = new Map();

  public register(rule: IRule): void {
    if (this.rules.has(rule.metadata.ruleId)) {
      throw new Error(`Rule ${rule.metadata.ruleId} is already registered.`);
    }
    this.rules.set(rule.metadata.ruleId, rule);
  }

  public getRule(id: string): IRule | undefined {
    return this.rules.get(id);
  }

  public getAllRules(): IRule[] {
    return Array.from(this.rules.values());
  }

  public getRulesByCategory(category: string): IRule[] {
    return this.getAllRules().filter(r => r.metadata.category === category);
  }
}
