import { ILogger } from '../interfaces';
import { LogLevel } from '../types';

export class StructuredLogger implements ILogger {
  private contextData: Record<string, any> = {};
  
  constructor(private minLevel: LogLevel = LogLevel.INFO) {}
  
  public setContext(key: string, value: string): void {
    this.contextData[key] = value;
  }
  
  public clearContext(): void {
    this.contextData = {};
  }
  
  public trace(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.TRACE, message, undefined, context);
  }
  
  public debug(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.DEBUG, message, undefined, context);
  }
  
  public info(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.INFO, message, undefined, context);
  }
  
  public warn(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.WARN, message, undefined, context);
  }
  
  public error(message: string, error?: Error, context?: Record<string, any>): void {
    this.log(LogLevel.ERROR, message, error, context);
  }
  
  private log(level: LogLevel, message: string, error?: Error, additionalContext?: Record<string, any>): void {
    if (level < this.minLevel) return;
    
    const logEntry = {
      timestamp: new Date().toISOString(),
      level: LogLevel[level],
      message,
      ...this.contextData,
      ...additionalContext,
      ...(error && { 
        error: {
          name: error.name,
          message: error.message,
          stack: error.stack
        }
      })
    };
    
    const output = JSON.stringify(logEntry);
    
    if (level >= LogLevel.ERROR) {
      console.error(output);
    } else if (level === LogLevel.WARN) {
      console.warn(output);
    } else {
      console.log(output);
    }
  }
}
