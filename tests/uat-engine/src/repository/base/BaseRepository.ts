import * as fs from 'fs';
import * as path from 'path';
import { IRepository, IRepositoryQuery } from '../interfaces';

export abstract class BaseRepository<T extends { id: string; timestamp: number }> implements IRepository<T> {
  protected storageDir: string;

  constructor(moduleName: string) {
    this.storageDir = path.resolve(__dirname, '../../../../.storage', moduleName);
    if (!fs.existsSync(this.storageDir)) {
      fs.mkdirSync(this.storageDir, { recursive: true });
    }
  }

  public async save(record: T): Promise<void> {
    // Immutable snapshots: Always write a new file based on timestamp and ID
    const fileName = `${record.timestamp}_${record.id}.json`;
    const filePath = path.join(this.storageDir, fileName);
    fs.writeFileSync(filePath, JSON.stringify(record, null, 2));
  }

  public async load(id: string): Promise<T | null> {
    const files = fs.readdirSync(this.storageDir);
    const match = files.find(f => f.includes(`_${id}.json`));
    if (!match) return null;
    const data = fs.readFileSync(path.join(this.storageDir, match), 'utf8');
    return JSON.parse(data) as T;
  }

  public async query(query: IRepositoryQuery): Promise<T[]> {
    let records = this.getAllRecords();
    
    if (query.startDate) {
      records = records.filter(r => r.timestamp >= query.startDate!);
    }
    if (query.endDate) {
      records = records.filter(r => r.timestamp <= query.endDate!);
    }
    
    if (query.filter) {
      records = records.filter(r => {
        for (const [k, v] of Object.entries(query.filter!)) {
          if ((r as any)[k] !== v) return false;
        }
        return true;
      });
    }

    records.sort((a, b) => b.timestamp - a.timestamp); // Descending

    if (query.limit) {
      return records.slice(0, query.limit);
    }
    return records;
  }

  public async latest(): Promise<T | null> {
    const records = this.getAllRecords();
    if (records.length === 0) return null;
    records.sort((a, b) => b.timestamp - a.timestamp);
    return records[0];
  }

  public async history(limit: number = 50): Promise<T[]> {
    const records = this.getAllRecords();
    records.sort((a, b) => b.timestamp - a.timestamp);
    return records.slice(0, limit);
  }

  public async betweenDates(start: number, end: number): Promise<T[]> {
    return this.query({ startDate: start, endDate: end });
  }

  public async statistics(): Promise<Record<string, any>> {
    const records = this.getAllRecords();
    return {
      totalRecords: records.length,
      oldest: records.length ? Math.min(...records.map(r => r.timestamp)) : null,
      newest: records.length ? Math.max(...records.map(r => r.timestamp)) : null
    };
  }

  protected getAllRecords(): T[] {
    const files = fs.readdirSync(this.storageDir).filter(f => f.endsWith('.json'));
    return files.map(f => {
      const data = fs.readFileSync(path.join(this.storageDir, f), 'utf8');
      return JSON.parse(data) as T;
    });
  }
}
