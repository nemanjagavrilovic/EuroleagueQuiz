# Privacy Policy and Terms of Use

The Angular routes `/privacy` and `/terms` share the component in
`client/src/app/legal/`. Wording and operator details live in `legal-content.ts`.

Operator details supplied on 18 November 2026:

- Nemanja Gavrilovic
- Serbia
- pickandrollquiz@gmail.com

## Review before publishing

These pages are a project-specific draft, not a legal compliance certification.
Have the wording reviewed for the operator's circumstances and applicable law.

1. Confirm whether a postal/business address must be published and add it to the
   operator section if required. An email address was supplied as the contact.
2. Confirm the audience and any minimum-age policy. No 16+ restriction was
   assumed or introduced; the pages refer to applicable age/parental requirements.
3. Establish retention periods, or documented criteria, for account data,
   guest/game attempts, audit logs, contact emails, provider data and backups.
   There is no scheduled purge for core database records in the current code.
   The policy discloses that fact; do not promise automatic erasure without
   implementing it. Admin soft deletion is not a GDPR/ZZPL erasure workflow.
4. Verify the active hosting/database/email providers, geographic regions,
   processor agreements and any required international-transfer safeguards.
   The draft names Supabase, Resend, Cloudflare, Google and Meta based on the
   configured project features. Update it if production uses different providers.
5. Confirm the Google Analytics retention setting and actual cookie lifetimes in
   the live browser. Provider-controlled lifetimes are not hard-coded here.
6. Monitor the privacy email and be ready to verify and handle access,
   correction, deletion, portability and objection requests within applicable
   deadlines. Do not ask users to send passwords.
7. Keep an archive of policy versions and update the displayed revision date
   when the wording changes materially.

## Registration and consent

- Email registration requires a Terms of Use checkbox in the web form.
- Google sign-in has an adjacent notice linking to the terms and privacy notice.
- Terms acceptance does not enable analytics or marketing; the existing cookie
  preference service controls those categories separately.
- This change does not add a server-side acceptance record or change API/mobile
  registration contracts. The checkbox is a web UI validation, not an auditable
  acceptance history. Add versioned server-side agreement recording before
  relying on it as proof of acceptance, and align mobile at that time.
- Opening the policy from the cookie banner dismisses the panel temporarily
  without saving or granting consent. It remains accessible through Cookie
  settings and reappears on a new visit if no choice has been saved.

## References used for the draft

- Serbian personal data protection law: https://www.parlament.gov.rs/upload/archive/files/lat/pdf/zakoni/2018/2959-18-lat.pdf
- Serbian Commissioner, exercising rights: https://poverenik.rs/zastita-podataka/kako-da-ostvarite-svoja-prava/?script=lat
- Google Analytics disclosure policy: https://support.google.com/analytics/answer/7318509?hl=en
- Google Analytics retention controls: https://support.google.com/analytics/answer/7667196?hl=en
- Cloudflare Turnstile privacy addendum: https://www.cloudflare.com/turnstile-privacy-policy/

No database migration is required for these pages.
