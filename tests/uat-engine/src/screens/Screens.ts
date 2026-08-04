import { IInteractionEngine, ElementId } from '../interaction/interfaces';
import { UIForm, UITable, UISearch, UIDialog } from '../interaction/components/UIComponents';

export class LoginScreen {
  private loginForm: UIForm;

  constructor(engine: IInteractionEngine) {
    this.loginForm = new UIForm(engine);
  }

  public async login(username: string, password: string): Promise<void> {
    await this.loginForm.fillField(ElementId.LoginUsername, username);
    await this.loginForm.fillField(ElementId.LoginPassword, password);
    await this.loginForm.submit(ElementId.LoginSubmit);
  }
}

export class POSScreen {
  private posSearch: UISearch;
  private posTable: UITable;
  private paymentDialog: UIDialog;
  public engine: IInteractionEngine;

  constructor(engine: IInteractionEngine) {
    this.engine = engine;
    this.posSearch = new UISearch(engine);
    this.posTable = new UITable(engine);
    this.paymentDialog = new UIDialog(engine);
  }

  public async scanBarcode(sku: string): Promise<void> {
    await this.posSearch.engine.setValue(ElementId.PosBarcodeScanner, sku);
    await this.posSearch.engine.submit(ElementId.PosBarcodeScanner);
  }

  public async openPayment(): Promise<void> {
    await this.engine.open(ElementId.PosPayButton);
  }

  public async enterCash(amount: number): Promise<void> {
    await this.engine.setValue(ElementId.PosCashAmount, amount.toString());
  }

  public async tender(): Promise<void> {
    await this.engine.submit(ElementId.PosTenderButton);
  }
}

export class InventoryScreen {
  private searchForm: UISearch;
  private inventoryTable: UITable;

  constructor(engine: IInteractionEngine) {
    this.searchForm = new UISearch(engine);
    this.inventoryTable = new UITable(engine);
  }

  public async searchProduct(sku: string): Promise<void> {
    await this.searchForm.search(sku);
  }

  public async selectProductRow(index: number): Promise<void> {
    await this.inventoryTable.selectRow(index);
  }
}
