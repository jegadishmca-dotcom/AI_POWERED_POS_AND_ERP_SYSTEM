import { Page } from '@playwright/test';
import { IReceiptHandler } from './interfaces/IReceiptHandler';

export class PlaywrightReceiptHandler implements IReceiptHandler {
  private page!: Page;
  private receiptPage: Page | null = null;
  private readonly receiptRegex = /receipt|invoice|bill|tax invoice|gst invoice/i;

  public setPage(page: Page): void {
    this.page = page;
  }

  public async waitUntilOpened(): Promise<void> {
    const context = this.page.context();
    
    // 1. Check if a new tab or popup already exists
    const existingPages = context.pages();
    const newPage = existingPages.find(p => p !== this.page);
    
    if (newPage) {
      this.receiptPage = newPage;
      await this.receiptPage.waitForLoadState('load');
      return;
    }

    // 2. Wait for either a new page event or the receipt appearing on the same page.
    // We use a custom race to prevent unhandled promise rejections from the loser
    // while perfectly relying on Playwright's native timeouts and wait mechanisms.
    let resolveRace: (page: Page) => void;
    let rejectRace: (err: Error) => void;
    
    const racePromise = new Promise<Page>((resolve, reject) => {
      resolveRace = resolve;
      rejectRace = reject;
    });

    let errors = 0;
    const handleError = (err: Error) => {
      errors++;
      if (errors === 2) {
        rejectRace(new Error(`Timeout waiting for receipt to open. Underlying errors: ${err.message}`));
      }
    };

    context.waitForEvent('page')
      .then(async (p) => { 
        await p.waitForLoadState('load'); 
        resolveRace(p); 
      })
      .catch(handleError);

    this.page.getByText(this.receiptRegex).first()
      .waitFor({ state: 'visible' })
      .then(() => resolveRace(this.page))
      .catch(handleError);

    this.receiptPage = await racePromise;
  }

  public async validateReceipt(): Promise<void> {
    if (!this.receiptPage) {
      throw new Error("Receipt page is not initialized. Call waitUntilOpened() first.");
    }

    // Verify meaningful receipt content exists to ensure it successfully rendered.
    // We rely on the page's default timeout configuration here.
    const receiptMarker = this.receiptPage.getByText(this.receiptRegex).first();
    await receiptMarker.waitFor({ state: 'visible' });
  }

  public async close(): Promise<void> {
    // Only close if it's a completely separate page (tab/popup).
    // Do not close the main application page.
    if (this.receiptPage && this.receiptPage !== this.page) {
      if (!this.receiptPage.isClosed()) {
        await this.receiptPage.close();
      }
    }
    this.receiptPage = null;
  }
}
