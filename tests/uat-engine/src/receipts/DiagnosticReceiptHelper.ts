import { Page } from '@playwright/test';

export class DiagnosticReceiptHelper {
  constructor(private readonly page: Page) {}

  public async runDiagnostic(): Promise<void> {
    console.log("[Diagnostic] Running receipt diagnostic...");
    
    // Give the browser a moment to spawn any new windows or tabs
    await this.page.waitForTimeout(2000);

    const context = this.page.context();
    const pages = context.pages();
    
    console.log(`[Diagnostic] Total pages in context: ${pages.length}`);
    
    for (let i = 0; i < pages.length; i++) {
      const p = pages[i];
      try {
        const url = p.url();
        const title = await p.title();
        console.log(`[Diagnostic] Page ${i + 1}: URL = ${url} | Title = ${title}`);
      } catch (err: any) {
        console.log(`[Diagnostic] Page ${i + 1}: Could not read URL or Title. Error: ${err.message}`);
      }
    }
    
    console.log("[Diagnostic] Waiting 5 seconds...");
    await this.page.waitForTimeout(5000);
    console.log("[Diagnostic] Diagnostic finished.");
  }
}
