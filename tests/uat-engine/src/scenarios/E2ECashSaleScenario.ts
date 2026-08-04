import { Page } from '@playwright/test';
import { IExecutableScenario } from '../runner/interfaces/IExecutableScenario';
import { ILoginScreen } from '../screens/ILoginScreen';
import { IPOSBillingScreen } from '../screens/IPOSBillingScreen';
import { IReceiptHandler } from '../receipts/interfaces/IReceiptHandler';

export class E2ECashSaleScenario implements IExecutableScenario {
  constructor(
    private readonly loginScreen: ILoginScreen,
    private readonly billingScreen: IPOSBillingScreen,
    private readonly receiptHandler: IReceiptHandler,
    private readonly username: string,
    private readonly password: string,
    private readonly productName: string,
    private readonly cashAmount: number
  ) {}

  public setPage(page: Page): void {
    this.loginScreen.setPage(page);
    this.billingScreen.setPage(page);
    this.receiptHandler.setPage(page);
  }

  public async execute(): Promise<void> {
    // 1. Login Phase
    await this.loginScreen.enterUsername(this.username);
    await this.loginScreen.enterPassword(this.password);
    await this.loginScreen.clickLogin();
    await this.loginScreen.waitUntilLoggedIn();

    // 2. POS Billing Phase
    await this.billingScreen.searchProduct(this.productName);
    await this.billingScreen.verifyProductInCart(this.productName);
    
    // 3. Payment Phase
    await this.billingScreen.clickPayment();
    await this.billingScreen.selectCashPayment();
    await this.billingScreen.enterCashAmount(this.cashAmount);
    await this.billingScreen.confirmPayment();

    // 4. Receipt Phase
    await this.receiptHandler.waitUntilOpened();
  }
}
