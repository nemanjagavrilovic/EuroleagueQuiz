import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-contact', standalone: true,
  imports: [NgIf, ReactiveFormsModule, RouterLink],
  templateUrl: './contact.component.html', styleUrl: './contact.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ContactComponent {
  private readonly http = inject(HttpClient);
  private readonly detector = inject(ChangeDetectorRef);
  private readonly builder = inject(FormBuilder);
  readonly form = this.builder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/^[^\r\n]*\S[^\r\n]*$/)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    subject: ['', [Validators.required, Validators.maxLength(150), Validators.pattern(/^[^\r\n]*\S[^\r\n]*$/)]],
    message: ['', [Validators.required, Validators.maxLength(5000), (control: AbstractControl<string>) =>
      typeof control.value === 'string' && control.value.trim().length >= 10 ? null : { minlength: true }]],
    website: ['', Validators.maxLength(200)]
  });
  sending = false;
  sent = false;
  errorMessage = '';

  invalid(field: 'name' | 'email' | 'subject' | 'message'): boolean {
    const control = this.form.controls[field];
    return control.touched && control.invalid;
  }

  submit(): void {
    if (this.sending || this.sent) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const values = this.form.getRawValue();
    const request = Object.fromEntries(Object.entries(values).map(([key, value]) => [key, value.trim()]));
    this.sending = true;
    this.errorMessage = '';
    this.http.post(`${environment.apiUrl.replace(/\/+$/, '')}/contact`, request).pipe(
      finalize(() => { this.sending = false; this.detector.markForCheck(); })
    ).subscribe({
      next: () => { this.sent = true; this.form.reset(); },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.status === 429
          ? 'Too many messages have been sent recently. Please wait a minute and try again.'
          : error.status === 400
            ? 'Please check your details and message, then try again.'
            : 'Your message could not be sent. Your text is still here—please try again later.';
      }
    });
  }
}
