import { Injectable, signal } from '@angular/core';

export type ConfirmationModalTone = 'default' | 'danger';

export interface ConfirmationModalOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmationModalTone;
}

interface ConfirmationModalState extends Required<ConfirmationModalOptions> {}

@Injectable({ providedIn: 'root' })
export class ConfirmationModalService {
  private readonly modalState = signal<ConfirmationModalState | null>(null);
  private resolveActive?: (confirmed: boolean) => void;

  readonly state = this.modalState.asReadonly();

  confirm(options: ConfirmationModalOptions): Promise<boolean> {
    this.close(false);
    this.modalState.set({
      title: options.title,
      message: options.message,
      confirmLabel: options.confirmLabel ?? 'Confirm',
      cancelLabel: options.cancelLabel ?? 'Cancel',
      tone: options.tone ?? 'default'
    });

    return new Promise<boolean>(resolve => { this.resolveActive = resolve; });
  }

  accept(): void { this.close(true); }
  cancel(): void { this.close(false); }

  private close(confirmed: boolean): void {
    if (!this.modalState()) return;
    const resolve = this.resolveActive;
    this.resolveActive = undefined;
    this.modalState.set(null);
    resolve?.(confirmed);
  }
}
