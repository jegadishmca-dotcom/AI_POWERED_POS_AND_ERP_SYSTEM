export class TelemetryFramework {
  private timings: Map<string, { start: number; end?: number; duration?: number }> = new Map();
  private memoryStats: any[] = [];
  
  public startMeasurement(label: string): void {
    this.timings.set(label, { start: performance.now() });
  }
  
  public endMeasurement(label: string): number {
    const record = this.timings.get(label);
    if (!record) {
      throw new Error(`No measurement started for label: ${label}`);
    }
    record.end = performance.now();
    record.duration = record.end - record.start;
    return record.duration;
  }
  
  public recordMemoryUsage(label: string): void {
    const usage = process.memoryUsage();
    this.memoryStats.push({
      label,
      timestamp: Date.now(),
      rss: Math.round(usage.rss / 1024 / 1024), // MB
      heapTotal: Math.round(usage.heapTotal / 1024 / 1024),
      heapUsed: Math.round(usage.heapUsed / 1024 / 1024)
    });
  }
  
  public getReport(): any {
    const timingsObj: Record<string, number> = {};
    for (const [key, val] of this.timings.entries()) {
      if (val.duration !== undefined) {
        timingsObj[key] = val.duration;
      }
    }
    
    return {
      timingsMs: timingsObj,
      memorySnapshotsMB: this.memoryStats
    };
  }
}
