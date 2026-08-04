import { Page } from '@playwright/test';
import * as fs from 'fs';
import { ILoginScreen } from './ILoginScreen';

export class PlaywrightLoginScreen implements ILoginScreen {
  private page!: Page;

  public setPage(page: Page): void {
    this.page = page;
  }

  private async logDiagnostics(stage: string): Promise<Record<string, number>> {
    const url = this.page.url();
    const title = await this.page.title();
    console.log(`[Diagnostics - ${stage}] URL: ${url} | Title: ${title}`);
    
    const counts = {
      allInputs: await this.page.locator('input').count(),
      typePassword: await this.page.locator('input[type="password"]').count(),
      namePassword: await this.page.locator('input[name="password"]').count(),
      labelPassword: await this.page.getByLabel(/password/i).count(),
      placeholderPassword: await this.page.getByPlaceholder(/password/i).count(),
    };
    
    console.log(`[Diagnostics - ${stage}] Locator Counts:`, counts);
    return counts;
  }

  public async enterUsername(username: string): Promise<void> {
    await this.logDiagnostics('enterUsername');
    const input = this.page.getByPlaceholder(/username|email/i).or(this.page.getByLabel(/username|email/i));
    await input.first().fill(username);
  }

  public async enterPassword(password: string): Promise<void> {
    const counts = await this.logDiagnostics('enterPassword');
    const input = this.page.locator('input[name="password"]')
      .or(this.page.locator('input[type="password"]'))
      .or(this.page.getByPlaceholder(/password/i))
      .or(this.page.getByLabel(/password/i))
      .first();

    try {
      await input.waitFor({ state: 'visible' });
      await this.page.screenshot({ path: 'login-debug.png' });
    } catch (error: any) {
      const html = await this.page.content();
      fs.writeFileSync('login-debug.html', html);
      throw new Error(`Password field not found! Counts: ${JSON.stringify(counts)}. Original Error: ${error.message}`);
    }

    await input.fill(password);
  }

  public async clickLogin(): Promise<void> {
    const button = this.page.getByRole('button', { name: /login|sign in|submit/i });
    await button.first().click();
  }

  public async waitUntilLoggedIn(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }
}
