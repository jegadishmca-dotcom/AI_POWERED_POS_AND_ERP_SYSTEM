import { Page } from '@playwright/test';
import { IExecutableScenario } from '../runner/interfaces/IExecutableScenario';
import { ILoginScreen } from '../screens/ILoginScreen';

export class LoginScenario implements IExecutableScenario {
  constructor(
    private readonly loginScreen: ILoginScreen,
    private readonly username: string,
    private readonly password: string
  ) {}

  public setPage(page: Page): void {
    this.loginScreen.setPage(page);
  }

  public async execute(): Promise<void> {
    await this.loginScreen.enterUsername(this.username);
    await this.loginScreen.enterPassword(this.password);
    await this.loginScreen.clickLogin();
    await this.loginScreen.waitUntilLoggedIn();
  }
}
