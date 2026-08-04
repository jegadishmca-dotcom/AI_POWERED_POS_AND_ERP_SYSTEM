import { IDependencyContainer } from '../interfaces';
import { Scope } from '../types';
import { ConfigurationException } from '../exceptions';

type Factory<T> = (container: IDependencyContainer) => T;

interface Registration<T> {
  factory?: Factory<T>;
  instance?: T;
  scope: Scope;
}

export class DependencyContainer implements IDependencyContainer {
  private registrations = new Map<string | symbol, Registration<any>>();
  private scopedInstances = new Map<string | symbol, any>();
  
  public register<T>(token: string | symbol, instance: T): void {
    this.registrations.set(token, {
      instance,
      scope: Scope.SINGLETON
    });
  }
  
  public registerFactory<T>(token: string | symbol, factory: Factory<T>, scope: Scope = Scope.TRANSIENT): void {
    this.registrations.set(token, {
      factory,
      scope
    });
  }
  
  public registerClass<T>(token: string | symbol, constructor: { new(...args: any[]): T }, scope: Scope = Scope.TRANSIENT): void {
    this.registerFactory(token, () => new constructor(), scope);
  }
  
  public resolve<T>(token: string | symbol): T {
    const registration = this.registrations.get(token);
    
    if (!registration) {
      throw new ConfigurationException(`No registration found for token: ${String(token)}`);
    }
    
    if (registration.scope === Scope.SINGLETON) {
      if (registration.instance === undefined) {
        if (!registration.factory) {
          throw new ConfigurationException(`Singleton registration missing factory and instance for token: ${String(token)}`);
        }
        registration.instance = registration.factory(this);
      }
      return registration.instance;
    }
    
    if (registration.scope === Scope.SCOPED) {
      if (!this.scopedInstances.has(token)) {
        if (!registration.factory) {
          throw new ConfigurationException(`Scoped registration missing factory for token: ${String(token)}`);
        }
        this.scopedInstances.set(token, registration.factory(this));
      }
      return this.scopedInstances.get(token);
    }
    
    // TRANSIENT
    if (!registration.factory) {
      throw new ConfigurationException(`Transient registration missing factory for token: ${String(token)}`);
    }
    return registration.factory(this);
  }
  
  public createScope(): DependencyContainer {
    const scopedContainer = new DependencyContainer();
    // Copy registrations (but not instances, they are lazy evaluated or singleton-shared)
    for (const [key, value] of this.registrations.entries()) {
      scopedContainer.registrations.set(key, value);
    }
    return scopedContainer;
  }
}
