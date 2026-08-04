import { IInteractionEngine, IUIComponent, ElementId } from '../interfaces';

export class UIForm implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async fillField(elementId: ElementId, value: string): Promise<void> {
    await this.engine.setValue(elementId, value);
  }

  public async submit(elementId: ElementId = ElementId.SubmitButton): Promise<void> {
    await this.engine.submit(elementId);
  }

  public async cancel(elementId: ElementId = ElementId.CancelButton): Promise<void> {
    await this.engine.cancel(elementId);
  }
}

export class UITable implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async selectRow(rowIndex: number): Promise<void> {
    // We map row index to a generic table row element selection
    await this.engine.choose(ElementId.TableRow, rowIndex.toString());
  }

  public async clickCell(rowIndex: number, colIndex: number): Promise<void> {
    await this.engine.choose(ElementId.TableCell, `${rowIndex},${colIndex}`);
  }
}

export class UIDialog implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async confirm(): Promise<void> {
    await this.engine.confirm(ElementId.ConfirmButton);
  }

  public async close(): Promise<void> {
    await this.engine.close(ElementId.CloseButton);
  }
}

export class UISearch implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async search(query: string): Promise<void> {
    await this.engine.search(ElementId.SearchInput, query);
  }
}

export class UIToolbar implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async clickAction(actionId: ElementId): Promise<void> {
    await this.engine.submit(actionId);
  }
}

export class UINotification implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async dismiss(elementId: ElementId): Promise<void> {
    await this.engine.close(elementId);
  }
}

export class UIGrid implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async selectItem(itemId: string): Promise<void> {
    await this.engine.choose(ElementId.TableRow, itemId);
  }
}

export class UIPanel implements IUIComponent {
  constructor(public engine: IInteractionEngine) {}

  public async expand(elementId: ElementId): Promise<void> {
    await this.engine.open(elementId);
  }

  public async collapse(elementId: ElementId): Promise<void> {
    await this.engine.close(elementId);
  }
}
