import { Page, expect } from "@playwright/test";
import * as fs from "fs";
import { IPOSBillingScreen } from "./IPOSBillingScreen";

export class PlaywrightPOSBillingScreen implements IPOSBillingScreen {
  private page!: Page;

  public setPage(page: Page): void {
    this.page = page;
  }

  public async searchProduct(productName: string): Promise<void> {
    const url = this.page.url();
    const title = await this.page.title();
    console.log(`[Diagnostics - searchProduct] URL: ${url} | Title: ${title}`);
    
    const counts = {
      inputs: await this.page.locator('input').count(),
      inputsWithPlaceholder: await this.page.locator('input[placeholder]').count(),
      scanBarcode: await this.page.getByPlaceholder(/scan barcode/i).count(),
      productNamePlaceholder: await this.page.getByPlaceholder(/product name/i).count(),
      f2Placeholder: await this.page.getByPlaceholder(/F2:/i).count(),
      inputTextContents: await this.page.locator('input').allTextContents()
    };
    
    console.log(`[Diagnostics - searchProduct] Locator Counts:`, counts);
    
    await this.page.screenshot({ path: 'billing-debug.png' });

    const input = this.page.getByPlaceholder("F2: Scan Barcode or Type Product Name (Press Enter)...");
    
    try {
      await input.waitFor({ state: 'visible', timeout: 5000 });
    } catch (error: any) {
      const html = await this.page.content();
      fs.writeFileSync('billing-debug.html', html);
      throw new Error(`searchProduct failed to locate textbox! Counts: ${JSON.stringify(counts)}. Original Error: ${error.message}`);
    }

    await input.click();
    await input.clear();
    await input.fill(productName);
  }

  public async verifyProductInCart(productName: string): Promise<void> {
    const productElement = this.page.getByText(productName, { exact: false }).first();
    await expect(productElement).toBeVisible();
  }

  public async clickPayment(): Promise<void> {
    const paymentButton = this.page.getByRole('button', { name: 'PAYMENT (F11)' });
    await paymentButton.click();
    
    // Wait for the payment dialog to become visible before returning
    await this.page.getByRole('dialog').waitFor({ state: 'visible' });
  }

  public async selectCashPayment(): Promise<void> {
    // Cash is selected by default when the Complete Payment dialog opens.
  }

  public async enterCashAmount(amount: number): Promise<void> {
    const cashInput = this.page.getByLabel("Cash Amount");
    await cashInput.click();
    await cashInput.clear();
    await cashInput.fill(amount.toString());
  }

  public async confirmPayment(): Promise<void> {
    throw new Error("Not implemented.");
  }
}
