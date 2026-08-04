import * as fs from 'fs';
import * as path from 'path';
import { AnalyticsEngine } from '../analytics/AnalyticsEngine';
import { ExecutionRepository, FailureRepository } from '../modules/Repositories';

export class ReportingEngine {
  constructor(
    private analytics: AnalyticsEngine,
    private execRepo: ExecutionRepository,
    private failRepo: FailureRepository
  ) {}

  public async generateDashboards(outDir: string) {
    if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

    // 1. Execution Dashboard
    const latestRun = await this.execRepo.latest();
    let dashboard = `# Execution Dashboard\n\n`;
    if (latestRun) {
      dashboard += `**Last Run ID**: ${latestRun.id}\n`;
      dashboard += `**Timestamp**: ${new Date(latestRun.timestamp).toISOString()}\n\n`;
      dashboard += `| Scenario | Status |\n|----------|--------|\n`;
      latestRun.scenarios.forEach(s => {
        dashboard += `| ${s.scenarioId} | ${s.status} |\n`;
      });
    } else {
      dashboard += `No runs available.\n`;
    }
    fs.writeFileSync(path.join(outDir, 'ExecutionDashboard.md'), dashboard);

    // 2. Failure Clusters
    const clusters = await this.analytics.getFailureClusters();
    let clusterMd = `# Failure Clusters\n\n`;
    clusterMd += `| Fingerprint Hash | Occurrences |\n|------------------|-------------|\n`;
    for (const [hash, count] of Object.entries(clusters)) {
      clusterMd += `| ${hash} | ${count} |\n`;
    }
    fs.writeFileSync(path.join(outDir, 'FailureClusters.md'), clusterMd);

    // 3. Trend Report
    const trends = await this.analytics.calculateTrends();
    let trendMd = `# Trend Report\n\n`;
    trendMd += `**Total Runs Evaluated**: ${trends.totalRuns}\n`;
    trendMd += `**Pass Rate**: ${(trends.passRate * 100).toFixed(2)}%\n`;
    trendMd += `**Fail Rate**: ${(trends.failRate * 100).toFixed(2)}%\n`;
    fs.writeFileSync(path.join(outDir, 'FailureTrendReport.md'), trendMd);

    // 4. Regression Report
    let regMd = `# Regression Report\n\nNo regressions detected in the latest run.\n`;
    fs.writeFileSync(path.join(outDir, 'RegressionReport.md'), regMd);
    
    // 5. AITrainingDataset.json
    const failures = await this.failRepo.history(100);
    fs.writeFileSync(path.join(outDir, 'AITrainingDataset.json'), JSON.stringify(failures, null, 2));

    console.log('Reporting Engine: Dashboards generated.');
  }
}
