import { IBaseline } from './IBaseline';

export interface IBaselineSource {
  load(scenarioId: string): Promise<IBaseline>;
  exists(scenarioId: string): Promise<boolean>;
}
