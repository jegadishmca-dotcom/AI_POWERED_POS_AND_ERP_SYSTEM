import { ICapability } from '../interfaces';

export class CapabilityRegistry {
  private capabilities: Map<string, ICapability> = new Map();

  public register(capability: ICapability): void {
    if (this.capabilities.has(capability.capabilityId)) {
      throw new Error(`Capability ${capability.capabilityId} is already registered.`);
    }
    this.capabilities.set(capability.capabilityId, capability);
  }

  public getCapability(id: string): ICapability | undefined {
    return this.capabilities.get(id);
  }

  public getAllCapabilities(): ICapability[] {
    return Array.from(this.capabilities.values());
  }
}
