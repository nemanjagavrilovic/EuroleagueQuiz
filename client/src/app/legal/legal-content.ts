export interface LegalSection {
  id: string;
  title: string;
  paragraphs: string[];
  items?: string[];
  links?: { label: string; href: string }[];
}

export interface LegalDocument {
  title: string;
  introduction: string;
  sections: LegalSection[];
}

export const legalDetails = {
  operator: 'Nemanja Gavrilovic',
  country: 'Serbia',
  email: 'pickandrollquiz@gmail.com',
  updated: '18 November 2026',
  updatedIso: '2026-11-18'
};

export const privacyPolicy: LegalDocument = {
  title: 'Privacy Policy',
  introduction: 'How Pick & Roll uses your information, what appears on the leaderboard, and how you can control optional tracking.',
  sections: [
    {
      id: 'operator', title: 'Who is responsible for your information',
      paragraphs: [
        `Pick & Roll at pickandroll.io is operated by ${legalDetails.operator}, based in ${legalDetails.country}. The operator is responsible for deciding how personal information is used to run this portal.`,
        `For privacy questions or requests about your information, write to ${legalDetails.email}. This policy covers the website and the account and game services it provides. External services have their own privacy notices.`
      ]
    },
    {
      id: 'information', title: 'Information we collect',
      paragraphs: ['The information involved depends on whether you play as a guest, create an account, or contact us.'],
      items: [
        'Account information: first name, last name, email address, optional phone number, account identifiers, confirmation status, and account creation and sign-in dates. Portal passwords are stored as password hashes, rather than readable passwords.',
        'Google sign-in information: the Google account identifier, email address and name Google provides to sign you in or create your account. We do not receive your Google password.',
        'Game information: guest or account identifiers, daily challenges, submitted answers, progress, timing, results and scores needed to run games and calculate rankings.',
        'Messages: your name, email address, subject and message when you use the contact form or email us.',
        'Technical information: requests, timestamps, device and browser information, IP addresses processed by our servers or security providers, and diagnostic information needed to deliver and protect the service.',
        'Audit information: records of account, administrator and game actions, including identifiers, action, outcome and time. Guest daily game starts, completions and scores use the existing guest browser identifier for administrator activity reports. Registered players’ Ranked and Practice game starts and completions are linked to their account. Game audit entries do not contain IP addresses or submitted answer contents.',
        'Optional measurement information: page visits, browser or device signals, cookie identifiers, campaign information and supported quiz interactions when you allow the relevant tracking category.'
      ]
    },
    {
      id: 'purposes', title: 'Why we use this information',
      paragraphs: ['Where data-protection law requires a legal basis, we use the basis appropriate to the activity.'],
      items: [
        'Providing the service and performing our agreement with you: creating and maintaining accounts, authenticating you, saving daily attempts, calculating points, showing rankings and sending account confirmation or password emails.',
        'Our legitimate interests in a secure and reliable portal: preventing abuse and cheating, investigating faults, keeping audit records and responding to messages. We consider the effect of this processing on your rights.',
        'Your consent: enabling optional Google Analytics or Meta Pixel tracking through the cookie panel. You can withdraw this permission at any time.',
        'Legal obligations: responding to valid legal requests or retaining information where the law requires it.'
      ]
    },
    {
      id: 'public-information', title: 'What other players can see',
      paragraphs: [
        'Ranked play is public. Leaderboards show your display name, rank and points. Your display name is currently formed from your first and last name, so these names can be visible to other visitors and may be indexed by search engines.',
        'Your email address, optional phone number, password details and private contact messages are not displayed on public leaderboards. Guest scores are not included in the ranked leaderboard.',
        'You can update your first name, last name and optional phone number on your profile. Contact us if you need help with information you cannot change there.'
      ]
    },
    {
      id: 'cookies', title: 'Cookies, browser storage and your choices',
      paragraphs: [
        'We use cookies and local browser storage to keep sign-in secure, recognise a guest daily attempt, and remember your privacy and interface choices. These essential functions remain available when you reject optional tracking.',
        'Google Analytics is enabled only after you allow analytics. Meta Pixel is enabled only after you allow marketing. You can accept, reject or change these categories independently in Cookie settings. Registering or accepting the Terms of Use does not grant permission for optional tracking.',
        'Withdrawing permission stops future optional measurement by the portal. It does not undo earlier lawful processing or automatically delete information already held by a provider. Your choice applies to this browser; other devices and browsers may have different choices.',
        'You can also clear cookies and local storage in your browser. This can sign you out, reset consent choices and prevent us from recognising your previous guest attempt.'
      ]
    },
    {
      id: 'providers', title: 'Services that help us run the portal',
      paragraphs: [
        'Authorised administrators and service providers can access information needed for their responsibilities. Providers include Supabase for PostgreSQL database hosting, Resend for account and contact emails, Google for optional sign-in and analytics, Meta for optional advertising measurement, and Cloudflare for security, Turnstile verification and Web Analytics. Hosting and network providers also process requests needed to deliver the website.',
        'Resend processes email addresses and message contents needed to deliver emails. Google Analytics measures website usage when permitted. Meta Pixel measures Facebook and Instagram advertising and may associate activity with information Meta already holds. We do not intentionally include your account email, phone number, password or contact-message contents in our analytics event parameters.',
        'Cloudflare Web Analytics provides aggregate traffic measurement without analytics cookies. Turnstile processes browser and device signals to detect automated requests; security processing is separate from the optional marketing choice.',
        'Some providers process information for their own purposes under their notices. Their policies explain their practices and available controls.'
      ],
      links: [
        { label: 'Google Privacy Policy', href: 'https://policies.google.com/privacy' },
        { label: 'How Google uses data from partner sites', href: 'https://policies.google.com/technologies/partner-sites' },
        { label: 'Meta Privacy Policy', href: 'https://www.facebook.com/privacy/policy/' },
        { label: 'Cloudflare Privacy Policy', href: 'https://www.cloudflare.com/privacypolicy/' },
        { label: 'Cloudflare Turnstile Privacy Addendum', href: 'https://www.cloudflare.com/turnstile-privacy-policy/' },
        { label: 'Resend Privacy Policy', href: 'https://resend.com/legal/privacy-policy' },
        { label: 'Supabase Privacy Policy', href: 'https://supabase.com/privacy' }
      ]
    },
    {
      id: 'international-transfers', title: 'Processing in other countries',
      paragraphs: [
        'The operator is based in Serbia. Our providers may process information in other countries, including countries outside Serbia or the European Economic Area. The locations depend on the provider, the database region and the services used.',
        'Where applicable law requires protection for an international transfer, appropriate transfer arrangements are required. Contact us for information about the destinations and safeguards relevant to your data.'
      ]
    },
    {
      id: 'retention', title: 'How long information is kept',
      paragraphs: [
        'Account information is kept to maintain your account. Game attempts and scores are retained to preserve progress, historical rankings and the integrity of daily challenges. Contact messages are kept for handling the conversation and any related issue. Security and audit records support investigation, accountability and the protection of the service.',
        'The current service does not apply a scheduled automatic deletion period to account, game or audit records. You can request deletion by contacting us. We assess what can be erased or anonymised and whether specific records remain necessary for a legal obligation, dispute or security investigation.',
        'An administrator deactivating an account is a logical deletion: access is disabled, but stored records are not automatically erased. Backups and provider-held records may remain until they are deleted or overwritten under the relevant retention arrangements. Contact us for details about a particular record or deletion request.'
      ]
    },
    {
      id: 'rights', title: 'Your privacy rights',
      paragraphs: [
        `Depending on the law that applies to you, you may request access, correction, deletion, restriction or a portable copy of your information, and object to certain processing. To make a request, email ${legalDetails.email}. We may need enough information to verify that the request concerns your account. Do not send us your password.`,
        'You can withdraw optional tracking consent through Cookie settings. Requests and rights can be subject to legal conditions and exceptions. We will respond within the time required by the applicable law.',
        'You can complain to the Serbian Commissioner for Information of Public Importance and Personal Data Protection, or another competent data-protection authority where applicable. You can contact us first, but you do not have to do so before making a complaint.'
      ],
      links: [{ label: 'Serbian Commissioner — information about your rights', href: 'https://poverenik.rs/zastita-podataka/kako-da-ostvarite-svoja-prava/?script=lat' }]
    },
    {
      id: 'security', title: 'Security and younger users',
      paragraphs: [
        'We use access controls, password hashing and other technical measures to protect information. No online service can guarantee complete security. Keep your login details private and contact us if you think your account has been misused.',
        'The portal is not specifically directed at children. If you are below the age at which you can independently agree to use the service or consent to the relevant data processing, involve a parent or guardian as required by local law. Contact us if you believe a child has provided information that should be removed.'
      ]
    },
    {
      id: 'policy-changes', title: 'Changes and questions',
      paragraphs: [
        'We may update this notice as the service or its providers change. The date at the top identifies the current notice. Material changes will be brought to your attention through an appropriate notice; where new consent is needed, we will ask for it.',
        `For questions about this policy, contact ${legalDetails.operator} at ${legalDetails.email}.`
      ]
    }
  ]
};

export const termsOfUse: LegalDocument = {
  title: 'Terms of Use',
  introduction: 'The rules for playing Pick & Roll, using an account and competing fairly on the leaderboard.',
  sections: [
    {
      id: 'about', title: 'About Pick & Roll',
      paragraphs: [
        `Pick & Roll is an independent basketball quiz portal at pickandroll.io, operated by ${legalDetails.operator}, based in ${legalDetails.country}. Contact us at ${legalDetails.email}.`,
        'These terms cover use of the portal and its games. Read them before creating an account. By creating an account or choosing Continue with Google after being shown these terms, you agree to them. If you cannot enter into this agreement independently under applicable law, involve a parent or guardian.',
        'Pick & Roll is a fan project and is not affiliated with, endorsed by or an official service of EuroLeague or the clubs and players featured.'
      ]
    },
    {
      id: 'playing', title: 'Guest, practice and ranked play',
      paragraphs: ['The current games are free to play. Access depends on the mode you choose.'],
      items: [
        'Guests can play the daily challenge for each game, including a separate daily Player Grid at each of the four difficulty levels. Guest points are visible but do not count toward the ranked leaderboard.',
        'Registered users can use unlimited, unranked practice and participate in daily ranked challenges.',
        'Each Player Grid difficulty has its own ranked daily challenge. Guest daily challenges are separate from ranked challenges, and guest results do not become ranked scores when you register.',
        'Practice can generate new combinations. Daily play is tied to the challenge assigned by the server, rather than to a fresh puzzle on every refresh.',
        'If paid features are introduced later, their price and additional terms will be presented before you agree to purchase them.'
      ]
    },
    {
      id: 'accounts', title: 'Your account',
      paragraphs: [
        'Provide accurate account information and keep your login credentials private. Email registration requires confirmation. Google sign-in uses your selected Google account. Your phone number is optional.',
        'You are responsible for activity you authorise through your account. Let us know promptly if you suspect unauthorised access. Do not impersonate another person, share an account to manipulate rankings or create multiple accounts to gain extra ranked attempts.',
        'Your display name is currently your first and last name. When you play ranked games, that name can be publicly shown with your rank and points. The Privacy Policy explains how account and game information is handled.'
      ]
    },
    {
      id: 'fair-play', title: 'Fair play',
      paragraphs: ['Ranked results should reflect your own basketball knowledge. When using the portal, do not:'],
      items: [
        'Use scripts, bots, answer extraction, automated submissions or manipulated requests to obtain an unfair score.',
        'Bypass daily limits, timers, identity checks, security controls or restrictions on repeated answers.',
        'Exploit a bug to influence rankings, or share current ranked answers to help others manipulate the leaderboard.',
        'Disrupt the service, attempt unauthorised access, overload endpoints or harass other people.'
      ]
    },
    {
      id: 'scores', title: 'Points and leaderboard results',
      paragraphs: [
        'The Scoring page explains the current rules for each game, including clue bonuses, reveals and Player Grid difficulty weights. Only eligible ranked results contribute to leaderboard totals. Practice and guest scores remain unranked.',
        'The server validates answers and calculates results. Rankings can change when other players complete challenges or when an error or an ineligible result is corrected. Points are for in-game comparison and have no cash value.',
        'We may correct inaccurate results and remove scores obtained through cheating or technical abuse. Material scoring changes will be explained through the portal. If you think a result is wrong, contact us with the game and relevant details.'
      ]
    },
    {
      id: 'quiz-data', title: 'Quiz data and content',
      paragraphs: [
        'Quiz answers are based on the player, club and season records available in our dataset, currently covering seasons from 2007 onward. A real-world connection can be absent or recorded differently, so not every event in basketball history is necessarily a valid answer in the portal.',
        'Names, club marks, photographs and other third-party material remain the property of their respective owners. Their appearance does not imply a partnership or endorsement. These terms do not grant rights to reuse that material.',
        'You may use the portal for personal play and share links or your own results. Do not copy or commercially redistribute the portal, its original design, software or compiled quiz content without the relevant permission. Contact us about a suspected error or rights concern.'
      ]
    },
    {
      id: 'availability', title: 'Availability and changes',
      paragraphs: [
        'We aim to provide a reliable service, but games can be interrupted by maintenance, connection problems or faults. We cannot promise uninterrupted availability, perfect historical data or recovery of every interrupted attempt.',
        'We may update games, features and rules as the portal develops. Where a change materially affects account access or ranked play, we will provide an appropriate notice. These terms do not exclude any mandatory rights or liability that cannot lawfully be excluded.'
      ]
    },
    {
      id: 'account-actions', title: 'Restrictions, account closure and questions',
      paragraphs: [
        'We may investigate suspected abuse, remove ineligible scores, restrict features or deactivate an account where necessary to protect the portal or enforce these rules. Contact us if you believe an action was taken in error.',
        `To request account closure or deletion of personal information, email ${legalDetails.email}. Account deactivation disables access; it does not automatically erase all records. Deletion requests are handled as explained in the Privacy Policy.`,
        'If these terms change materially, we will notify users through an appropriate channel and request acceptance where required. Contact us if you have a question or wish to raise a complaint. Applicable mandatory consumer and privacy rights remain available to you.'
      ]
    }
  ]
};
