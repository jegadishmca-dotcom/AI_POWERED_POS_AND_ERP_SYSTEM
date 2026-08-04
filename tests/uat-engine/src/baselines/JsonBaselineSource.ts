import { IBaseline } from './interfaces/IBaseline';
import { IBaselineSource } from './interfaces/IBaselineSource';

export class JsonBaselineSource implements IBaselineSource {
  public async load(scenarioId: string): Promise<IBaseline> {
    throw new Error("Not implemented.");
  }

  public async exists(scenarioId: string): Promise<boolean> {
    throw new Error("Not implemented.");
  }
}
