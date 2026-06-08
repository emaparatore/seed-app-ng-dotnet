export interface SeedFeature {
  readonly slug: string;
  readonly title: string;
  readonly eyebrow: string;
  readonly summary: string;
  readonly status: 'Included' | 'Optional setup' | 'Production-ready';
  readonly audience: string;
  readonly highlights: readonly string[];
  readonly routes: readonly string[];
  readonly docs: readonly string[];
  readonly codeAreas: readonly string[];
  readonly setupNotes: readonly string[];
}

export const seedFeatures: readonly SeedFeature[] = [
  {
    slug: 'authentication',
    title: 'Authentication and account flows',
    eyebrow: 'Identity foundation',
    summary:
      'JWT authentication, registration, login, refresh token rotation, password reset, confirm email, profile management, and forced password change are already wired.',
    status: 'Included',
    audience: 'Use this when your new app needs a complete auth foundation without rebuilding the account lifecycle.',
    highlights: [
      'Registration, login, logout, forgot password, reset password, confirm email',
      'JWT bearer auth with refresh token rotation',
      'Protected routes plus forced password change flow',
      'Profile management already exposed in the frontend',
    ],
    routes: ['/login', '/register', '/forgot-password', '/reset-password', '/change-password', '/profile'],
    docs: ['docs/architecture/authentication.md'],
    codeAreas: ['backend/src/Seed.Api', 'backend/src/Seed.Infrastructure', 'frontend/web/projects/shared-auth'],
    setupNotes: [
      'Change branding and email copy for your product.',
      'Configure SMTP only when you need real email delivery.',
    ],
  },
  {
    slug: 'admin-rbac',
    title: 'Admin dashboard and RBAC',
    eyebrow: 'Operational control',
    summary:
      'The seed already includes an admin area with users, roles, permissions, audit log, system settings, dashboard metrics, and system health.',
    status: 'Included',
    audience: 'Useful when your app needs a secure back office from day one.',
    highlights: [
      'Role-based access control with granular permissions',
      'User and role management screens',
      'Audit log for sensitive actions',
      'System settings and health views',
    ],
    routes: ['/admin', '/admin/users', '/admin/roles', '/admin/audit-log', '/admin/settings', '/admin/system-health'],
    docs: ['docs/modules/admin-dashboard.md'],
    codeAreas: ['frontend/web/projects/app/src/app/pages/admin', 'backend/src/Seed.Application', 'backend/src/Seed.Infrastructure'],
    setupNotes: [
      'Review seeded permissions and roles before extending the admin area.',
      'Configure the initial SuperAdmin credentials for first deployment.',
    ],
  },
  {
    slug: 'subscriptions-billing',
    title: 'Subscriptions, plans, and billing',
    eyebrow: 'Monetization module',
    summary:
      'Plan catalog, checkout, customer portal, subscription management, invoice requests, and feature gating are already built behind a module toggle.',
    status: 'Optional setup',
    audience: 'Enable this when the new app needs paid plans or feature-based access control.',
    highlights: [
      'Public pricing page and Stripe checkout flow',
      'Customer subscription page and invoice request flow',
      'Admin plan, subscription, and invoice request management',
      'Backend and frontend feature gating patterns already present',
    ],
    routes: ['/pricing', '/billing/subscription', '/billing/invoice-requests', '/admin/plans', '/admin/subscriptions'],
    docs: ['docs/modules/subscription-payments.md', 'docs/modules/stripe-payments-setup.md'],
    codeAreas: ['frontend/web/projects/app/src/app/pages/pricing', 'frontend/web/projects/app/src/app/pages/billing', 'backend/src/Seed.Application/Features/Billing'],
    setupNotes: [
      'Leave it disabled if your app does not need payments.',
      'Configure Stripe keys and webhooks before turning it on in production.',
    ],
  },
  {
    slug: 'email-delivery',
    title: 'Email delivery and fallback mode',
    eyebrow: 'Communication layer',
    summary:
      'The application can run without SMTP and falls back to console logging, so auth and notification flows work in development before a mail provider is configured.',
    status: 'Optional setup',
    audience: 'Ideal when you want email-dependent features without blocking local development.',
    highlights: [
      'Console fallback keeps local flows usable',
      'SMTP provider integration documented for real delivery',
      'Works with authentication and subscription notifications',
    ],
    routes: [],
    docs: ['docs/modules/smtp-configuration.md'],
    codeAreas: ['backend/src/Seed.Infrastructure/Services/Email', 'backend/src/Seed.Shared/Configuration/SmtpSettings.cs'],
    setupNotes: [
      'You can postpone SMTP setup until staging or production.',
      'Once real email matters, configure provider credentials and DNS records.',
    ],
  },
  {
    slug: 'bootstrap-seeding',
    title: 'Bootstrap console and application seeding',
    eyebrow: 'Deployment bootstrap',
    summary:
      'A dedicated bootstrap runner validates configuration and seeds roles, permissions, admin user, and system settings during first deployment.',
    status: 'Production-ready',
    audience: 'This matters when you want repeatable environment initialization instead of manual setup.',
    highlights: [
      'Dedicated console app for deployment-time initialization',
      'Idempotent seeding for required data',
      'Clean extension point for custom seeders',
    ],
    routes: [],
    docs: ['docs/architecture/bootstrap-console.md', 'docs/getting-started/new-project-deploy-guide.md'],
    codeAreas: ['backend/src/Seed.Bootstrap', 'backend/src/Seed.Infrastructure/Persistence/Seeders'],
    setupNotes: [
      'Add your own seeders when the new app requires domain-specific bootstrap data.',
      'Keep this flow even after cloning the seed because it removes manual setup work.',
    ],
  },
  {
    slug: 'deploy-cicd',
    title: 'CI, Docker publishing, and VPS deploy',
    eyebrow: 'Delivery pipeline',
    summary:
      'The repository already includes CI, Docker image publishing, SSH deploy, migrations, seeding, health checks, and branch workflow conventions.',
    status: 'Production-ready',
    audience: 'Useful when you want the cloned app to keep a ready delivery path instead of designing one later.',
    highlights: [
      'GitHub Actions for CI and image publishing',
      'Parameterized deploy workflow for seed reuse',
      'Docker-based runtime and deployment scripts',
    ],
    routes: [],
    docs: ['docs/operations/ci-cd.md', 'docs/getting-started/vps-setup-guide.md'],
    codeAreas: ['.github/workflows', 'docker/docker-compose.deploy.yml', 'docker/scripts'],
    setupNotes: [
      'Adjust repository variables and secrets after cloning.',
      'Review deploy defaults before the first production rollout.',
    ],
  },
  {
    slug: 'operations-observability',
    title: 'Monitoring, rollback, and troubleshooting',
    eyebrow: 'Runbook layer',
    summary:
      'The seed includes operational guidance for monitoring, rollback, troubleshooting, and environment backup so the project is not limited to code scaffolding only.',
    status: 'Production-ready',
    audience: 'Important once the app moves beyond local development and starts needing repeatable operations.',
    highlights: [
      'Monitoring stack guidance with dashboards and metrics',
      'Rollback strategies for images, code, and database',
      'Centralized troubleshooting and environment backup docs',
    ],
    routes: [],
    docs: ['docs/operations/monitoring.md', 'docs/operations/rollback.md', 'docs/operations/troubleshooting.md', 'docs/operations/env-backup.md'],
    codeAreas: ['docs/operations', 'docker'],
    setupNotes: [
      'Most of this value is documentation and runbook structure rather than UI.',
      'Keep only the operational pieces your cloned app will actually use.',
    ],
  },
];

export function findSeedFeatureBySlug(slug: string | null): SeedFeature | undefined {
  if (!slug) {
    return undefined;
  }

  return seedFeatures.find((feature) => feature.slug === slug);
}
