import { ChangeDetectionStrategy, Component, ElementRef, HostListener, ViewChild, effect, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { ConfirmationModalService } from './confirmation-modal.service';

@Component({
  selector: 'app-confirmation-modal',
  standalone: true,
  imports: [NgIf],
  templateUrl: './confirmation-modal.component.html',
  styleUrl: './confirmation-modal.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmationModalComponent {
  @ViewChild('cancelButton') private cancelButton?: ElementRef<HTMLButtonElement>;
  private previouslyFocused?: HTMLElement;
  readonly confirmation = inject(ConfirmationModalService);

  constructor() {
    effect(() => {
      const isOpen = this.confirmation.state() !== null;
      if (isOpen) {
        this.previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
        queueMicrotask(() => this.cancelButton?.nativeElement.focus());
      } else if (this.previouslyFocused) {
        const element = this.previouslyFocused;
        this.previouslyFocused = undefined;
        queueMicrotask(() => element.focus());
      }
    });
  }

  cancelFromBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.confirmation.cancel();
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyboard(event: KeyboardEvent): void {
    if (!this.confirmation.state()) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.confirmation.cancel();
      return;
    }
    if (event.key !== 'Tab') return;

    const controls = Array.from(document.querySelectorAll<HTMLButtonElement>('.confirmation-dialog button:not(:disabled)'));
    if (!controls.length) return;
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
