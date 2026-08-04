import { BaseRepository } from '../base/BaseRepository';
import { ExecutionRecord, MetricRecord, TrendRecord, FailureRecord, BaselineRecord, ArtifactRecord, IndexRecord } from '../interfaces';

export class ExecutionRepository extends BaseRepository<ExecutionRecord> {
  constructor() { super('executions'); }
}

export class MetricRepository extends BaseRepository<MetricRecord> {
  constructor() { super('metrics'); }
}

export class TrendRepository extends BaseRepository<TrendRecord> {
  constructor() { super('trends'); }
}

export class FailureRepository extends BaseRepository<FailureRecord> {
  constructor() { super('failures'); }
}

export class BaselineRepository extends BaseRepository<BaselineRecord> {
  constructor() { super('baselines'); }
}

export class ArtifactRepository extends BaseRepository<ArtifactRecord> {
  constructor() { super('artifacts'); }
}

export class IndexRepository extends BaseRepository<IndexRecord> {
  constructor() { super('indexes'); }
}
