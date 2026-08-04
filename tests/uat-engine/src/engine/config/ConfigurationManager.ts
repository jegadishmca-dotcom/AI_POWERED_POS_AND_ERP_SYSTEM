import { IConfig } from '../interfaces';
import * as fs from 'fs';
import * as path from 'path';
import { ConfigurationException } from '../exceptions';

export class ConfigurationManager implements IConfig {
  private config: Map<string, any> = new Map();
  
  constructor(defaultValues?: Record<string, any>) {
    if (defaultValues) {
      for (const [key, value] of Object.entries(defaultValues)) {
        this.set(key, value);
      }
    }
  }

  public get<T>(key: string, defaultValue?: T): T {
    if (this.config.has(key)) {
      return this.config.get(key) as T;
    }
    if (defaultValue !== undefined) {
      return defaultValue;
    }
    throw new ConfigurationException(`Missing required configuration key: ${key}`);
  }

  public set<T>(key: string, value: T): void {
    this.config.set(key, value);
  }

  public loadFromFile(filePath: string): void {
    try {
      const absolutePath = path.resolve(process.cwd(), filePath);
      if (!fs.existsSync(absolutePath)) {
        throw new Error(`File not found: ${absolutePath}`);
      }
      
      const fileContent = fs.readFileSync(absolutePath, 'utf8');
      const parsed = JSON.parse(fileContent);
      
      this.flattenAndSet(parsed);
    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      throw new ConfigurationException(`Failed to load config from file ${filePath}: ${msg}`);
    }
  }

  public loadFromEnv(): void {
    // Prefix convention: UAT_ENGINE_
    const PREFIX = 'UAT_ENGINE_';
    for (const [key, value] of Object.entries(process.env)) {
      if (key.startsWith(PREFIX)) {
        const configKey = key.substring(PREFIX.length).toLowerCase().replace(/_/g, '.');
        
        // Attempt to parse JSON/boolean/numbers if possible
        let parsedValue: any = value;
        if (value === 'true') parsedValue = true;
        else if (value === 'false') parsedValue = false;
        else if (!isNaN(Number(value))) parsedValue = Number(value);
        
        this.set(configKey, parsedValue);
      }
    }
  }

  private flattenAndSet(obj: Record<string, any>, prefix: string = ''): void {
    for (const [key, value] of Object.entries(obj)) {
      const newKey = prefix ? `${prefix}.${key}` : key;
      if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
        this.flattenAndSet(value, newKey);
      } else {
        this.set(newKey, value);
      }
    }
  }
}
