import { ITestContext, IEventBus, ILogger, IConfig, IDependencyContainer } from '../interfaces';

export class TestContext implements ITestContext {
  public browser?: any;
  public database?: any;
  public api?: any;
  public evidence?: any;
  public rules?: any;
  
  constructor(
    public readonly eventBus: IEventBus,
    public readonly logger: ILogger,
    public readonly config: IConfig,
    public readonly di: IDependencyContainer,
    public readonly runMetadata: {
      runId: string;
      startTime: number;
      environment: string;
    }
  ) {}
}
