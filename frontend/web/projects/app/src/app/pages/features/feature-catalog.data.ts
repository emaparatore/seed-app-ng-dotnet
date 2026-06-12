export interface SeedFeature {
  readonly slug: string;
  readonly title: string;
  readonly eyebrow: string;
  readonly summary: string;
  readonly overview: readonly string[];
  readonly status: 'Included' | 'Optional setup' | 'Production-ready';
  readonly audience: string;
  readonly highlights: readonly string[];
  readonly sections: readonly FeatureDocSection[];
  readonly routes: readonly string[];
  readonly docs: readonly string[];
  readonly codeAreas: readonly string[];
  readonly setupNotes: readonly string[];
}

export interface FeatureDocSection {
  readonly id: string;
  readonly title: string;
  readonly paragraphs?: readonly string[];
  readonly items?: readonly string[];
  readonly table?: FeatureDocTable;
  readonly code?: FeatureCodeBlock;
  readonly callout?: FeatureCallout;
}

export interface FeatureDocTable {
  readonly headers: readonly string[];
  readonly rows: readonly (readonly string[])[];
}

export interface FeatureCodeBlock {
  readonly language: string;
  readonly value: string;
}

export interface FeatureCallout {
  readonly title: string;
  readonly body: string;
}

export const seedFeatures: readonly SeedFeature[] = [
  {
    slug: 'authentication',
    title: 'Authentication and account flows',
    eyebrow: 'Identity foundation',
    summary:
      'JWT authentication, registration, login, refresh token rotation, password reset, confirm email, profile management, and forced password change are already wired.',
    overview: [
      'The authentication module is a complete account lifecycle spanning ASP.NET Core Identity, JWT bearer authentication, refresh token rotation, email confirmation, password reset, protected Angular routes, and profile management.',
      'It follows the backend Clean Architecture already used by the project: API controllers call MediatR commands and queries, application handlers depend on interfaces, and infrastructure provides Identity, EF Core persistence, token generation, and email delivery.',
    ],
    status: 'Included',
    audience: 'Use this when your new app needs a complete auth foundation without rebuilding the account lifecycle.',
    highlights: [
      'Registration, login, logout, forgot password, reset password, confirm email',
      'JWT bearer auth with refresh token rotation',
      'Protected routes plus forced password change flow',
      'Profile management already exposed in the frontend',
    ],
    sections: [
      {
        id: 'flow',
        title: 'Account flow',
        paragraphs: [
          'Registration creates an inactive email-confirmation flow instead of immediately issuing JWT credentials. The backend generates an ASP.NET Identity email token, builds a frontend confirmation URL, sends it through IEmailService, and returns a neutral message to the client.',
          'After email confirmation, the API marks EmailConfirmed as true and returns the same AuthResponse used by login: access token, refresh token, user profile, permissions, and the MustChangePassword flag.',
        ],
        items: [
          'Register: POST /api/v1/auth/register creates the user and sends the confirmation link.',
          'Confirm email: POST /api/v1/auth/confirm-email validates the token and performs auto-login.',
          'Login: POST /api/v1/auth/login validates credentials and blocks unconfirmed users.',
          'Refresh: POST /api/v1/auth/refresh rotates the refresh token and returns a new token pair.',
          'Logout: POST /api/v1/auth/logout revokes the active refresh token.',
          'Password reset: forgot/reset endpoints use email tokens and anti-enumeration responses.',
        ],
      },
      {
        id: 'token-rotation',
        title: 'Refresh token rotation',
        paragraphs: [
          'Refresh tokens are opaque, persisted in the database, and single-use. Every refresh operation revokes the old token, creates a new one, and stores the replacement chain for audit purposes.',
          'This design limits the blast radius of a stolen refresh token: once it is used legitimately, the previous value can no longer be replayed.',
        ],
        table: {
          headers: ['Property', 'Meaning'],
          rows: [
            ['ExpiresAt', 'Absolute refresh token lifetime.'],
            ['RevokedAt', 'Set when the token is used or explicitly revoked.'],
            ['ReplacedByToken', 'Tracks the next token in the rotation chain.'],
            ['IsActive', 'Computed state used to accept or reject refresh attempts.'],
          ],
        },
      },
      {
        id: 'backend-structure',
        title: 'Backend structure',
        table: {
          headers: ['Layer', 'Main pieces'],
          rows: [
            ['Domain', 'ApplicationUser, ApplicationRole, RefreshToken.'],
            ['Application', 'Register, ConfirmEmail, Login, RefreshToken, Logout, ForgotPassword, ResetPassword, GetCurrentUser handlers.'],
            ['Infrastructure', 'ApplicationDbContext, TokenService, SmtpEmailService, ConsoleEmailService, Identity configuration.'],
            ['API', 'AuthController endpoints, JWT authentication, CORS, rate-limited reset flows.'],
          ],
        },
      },
      {
        id: 'frontend-structure',
        title: 'Frontend integration',
        paragraphs: [
          'The Angular side centralizes authentication in the shared-auth library. Guards protect private routes, guest-only routes, and the forced password-change state. The application stores token data client-side and uses the current user response to drive navigation and permissions.',
        ],
        items: [
          'Pages: login, register, forgot password, reset password, confirm email, change password, profile.',
          'Guards: authGuard, guestGuard, mustChangePasswordGuard, adminGuard.',
          'Shared models include AuthResponse, User, login/register/reset request payloads, and permission data.',
        ],
      },
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
    overview: [
      'The admin module is a real back-office area, not a placeholder. It provides RBAC, user and role management, audit logging, system settings, dashboard statistics, and health checks.',
      'Access is permission-driven end to end: the frontend hides or shows UI elements based on the logged-in user permissions, and the backend protects endpoints with permission attributes validated server-side.',
    ],
    status: 'Included',
    audience: 'Useful when your app needs a secure back office from day one.',
    highlights: [
      'Role-based access control with granular permissions',
      'User and role management screens',
      'Audit log for sensitive actions',
      'System settings and health views',
    ],
    sections: [
      {
        id: 'initial-setup',
        title: 'Initial setup',
        paragraphs: [
          'During bootstrap the system can create the first SuperAdmin user from configuration. In development the same seeders run automatically with migrations; in staging and production the deploy pipeline runs migrations and then the dedicated bootstrap console.',
        ],
        code: {
          language: 'env',
          value: 'SuperAdmin__Email=admin@seedapp.local\nSuperAdmin__Password=Admin123!\nSuperAdmin__FirstName=Super\nSuperAdmin__LastName=Admin',
        },
        callout: {
          title: 'Production note',
          body: 'After the first successful login, remove the temporary SuperAdmin password from the production .env file. Existing SuperAdmin users are not overwritten by the seeder.',
        },
      },
      {
        id: 'rbac',
        title: 'RBAC permission model',
        paragraphs: [
          'Permissions are atomic strings in the Resource.Action format and are defined centrally in the domain layer. Roles aggregate permissions, while users receive the union of permissions from their assigned roles.',
        ],
        table: {
          headers: ['Area', 'Examples'],
          rows: [
            ['Users', 'Users.Read, Users.Create, Users.Update, Users.Delete, Users.AssignRoles, Users.ToggleStatus.'],
            ['Roles', 'Roles.Read, Roles.Create, Roles.Update, Roles.Delete.'],
            ['Audit', 'AuditLog.Read, AuditLog.Export.'],
            ['Settings', 'Settings.Read, Settings.Manage.'],
            ['Dashboard/System', 'Dashboard.ViewStats, SystemHealth.Read.'],
          ],
        },
      },
      {
        id: 'admin-screens',
        title: 'Admin screens',
        items: [
          'Dashboard: overview metrics and operational signals.',
          'Users: list, detail, create, edit, activate/deactivate, role assignment.',
          'Roles: system roles, custom roles, permission assignment, delete protection for system roles.',
          'Audit log: sensitive action history and CSV export.',
          'Settings: system configuration values with read/manage permissions.',
          'System health: API, database, and runtime health information.',
        ],
      },
      {
        id: 'token-invalidation',
        title: 'Immediate permission changes',
        paragraphs: [
          'When an account is disabled or its roles change, active JWT tokens are invalidated through a server-side blacklist mechanism. The user is forced to authenticate again and receives a fresh permission set.',
        ],
      },
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
    overview: [
      'The subscription payments module is opt-in. When disabled, billing controllers, routes, pricing UI, and Stripe-specific behavior are not exposed. When enabled, the application supports plans, checkout, customer portal sessions, subscriptions, invoice requests, webhooks, and feature gating.',
      'The design keeps Stripe behind an application interface so local development and tests can use a mock gateway without requiring a Stripe account.',
    ],
    status: 'Optional setup',
    audience: 'Enable this when the new app needs paid plans or feature-based access control.',
    highlights: [
      'Public pricing page and Stripe checkout flow',
      'Customer subscription page and invoice request flow',
      'Admin plan, subscription, and invoice request management',
      'Backend and frontend feature gating patterns already present',
    ],
    sections: [
      {
        id: 'architecture',
        title: 'Architecture',
        code: {
          language: 'text',
          value: 'User -> Checkout -> Stripe -> Webhook -> StripeWebhookController\n                                      -> StripeWebhookEventHandler\n                                      -> DB update + email notification\n\nFallback:\nCheckout success page -> POST /billing/checkout/confirm -> Stripe API verification -> DB update',
        },
        table: {
          headers: ['Component', 'Role'],
          rows: [
            ['IPaymentGateway', 'Application abstraction for checkout, portal sessions, and plan sync.'],
            ['StripePaymentGateway', 'Production Stripe implementation.'],
            ['MockPaymentGateway', 'Development/test implementation with no Stripe account.'],
            ['StripeWebhookController', 'Receives and validates Stripe webhook posts.'],
            ['PaymentsModuleConvention', 'Removes billing controllers when the module is disabled.'],
            ['ConfigService/paymentsEnabledGuard', 'Expose and enforce module state in Angular.'],
          ],
        },
      },
      {
        id: 'module-toggle',
        title: 'Module toggle',
        paragraphs: [
          'Payment functionality is controlled by configuration. This lets a product clone keep billing code in the repository without exposing billing routes before the business is ready.',
        ],
        code: {
          language: 'json',
          value: '{\n  "Modules": {\n    "Payments": {\n      "Enabled": false,\n      "Provider": ""\n    }\n  },\n  "Stripe": {\n    "SecretKey": "",\n    "PublishableKey": "",\n    "WebhookSecret": ""\n  }\n}',
        },
      },
      {
        id: 'webhooks',
        title: 'Webhook processing',
        paragraphs: [
          'Stripe webhook events are idempotent. Processed event ids are persisted with a unique index and cached in memory to skip duplicates quickly. Email notification failures are logged but do not block subscription state updates.',
        ],
        table: {
          headers: ['Stripe event', 'Action'],
          rows: [
            ['checkout.session.completed', 'Create subscription and send confirmation email.'],
            ['invoice.payment_succeeded', 'Update billing period and restore Active state.'],
            ['invoice.payment_failed', 'Mark subscription as PastDue.'],
            ['customer.subscription.updated', 'Sync status, period, trial end, and plan changes.'],
            ['customer.subscription.deleted', 'Set status to Canceled and send cancellation email.'],
            ['customer.subscription.trial_will_end', 'Send trial-ending notification.'],
          ],
        },
      },
      {
        id: 'invoice-requests',
        title: 'Invoice request workflow',
        paragraphs: [
          'Manual invoice requests are linked to a concrete subscription reference and store service/payment snapshots. This keeps requests auditable even if the plan or subscription later changes.',
        ],
        items: [
          'User request payload includes userSubscriptionId.',
          'Backend validates ownership and blocks duplicates for the same billing transaction.',
          'Stored snapshot includes service name, service period, Stripe invoice/payment ids, currency, totals, tax, proration, and billing reason.',
          'User and admin screens show the purchase reference in the invoice request detail dialog.',
        ],
      },
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
    overview: [
      'Email delivery is intentionally non-blocking for local development. If SMTP is not configured, the infrastructure layer registers a console implementation that logs links and message details instead of sending real email.',
      'The same IEmailService abstraction is used by authentication and billing notifications, so enabling SMTP upgrades all email-dependent flows without changing application code.',
    ],
    status: 'Optional setup',
    audience: 'Ideal when you want email-dependent features without blocking local development.',
    highlights: [
      'Console fallback keeps local flows usable',
      'SMTP provider integration documented for real delivery',
      'Works with authentication and subscription notifications',
    ],
    sections: [
      {
        id: 'auto-switch',
        title: 'SMTP auto-switch',
        paragraphs: [
          'Infrastructure registration checks the Smtp configuration. When Smtp:Host is present, the real MailKit SMTP service is used. Otherwise the console fallback is registered.',
        ],
        items: [
          'Development works without provider credentials.',
          'Password reset and email confirmation links remain visible in logs.',
          'Production can switch to real delivery using configuration only.',
        ],
      },
      {
        id: 'configuration',
        title: 'Configuration keys',
        code: {
          language: 'env',
          value: 'Smtp__Host=smtp-relay.brevo.com\nSmtp__Port=587\nSmtp__Username=your-account@example.com\nSmtp__Password=<smtp-key>\nSmtp__FromEmail=noreply@example.com\nSmtp__FromName=Your App\nSmtp__Security=StartTls',
        },
      },
      {
        id: 'production-readiness',
        title: 'Production readiness',
        paragraphs: [
          'Real delivery requires more than credentials. Configure domain authentication with the chosen provider, including SPF and DKIM records, before relying on email for account recovery or billing communications.',
        ],
        callout: {
          title: 'Operational note',
          body: 'Keep the console fallback useful in development, but treat missing SMTP in production as a deployment checklist item if authentication emails are required.',
        },
      },
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
    overview: [
      'Bootstrap is separated from the API process through a dedicated console application. This gives deployments an explicit initialization step for configuration validation and required seed data.',
      'The approach avoids hidden production side effects inside normal API startup while still keeping local development convenient.',
    ],
    status: 'Production-ready',
    audience: 'This matters when you want repeatable environment initialization instead of manual setup.',
    highlights: [
      'Dedicated console app for deployment-time initialization',
      'Idempotent seeding for required data',
      'Clean extension point for custom seeders',
    ],
    sections: [
      {
        id: 'what-it-seeds',
        title: 'What bootstrap seeds',
        items: [
          'Roles and permissions required by RBAC.',
          'Initial SuperAdmin user when configured.',
          'Default system settings.',
          'Any future domain-specific seeders registered in infrastructure.',
        ],
      },
      {
        id: 'how-to-run',
        title: 'How to run it',
        code: {
          language: 'bash',
          value: 'dotnet run --project backend/src/Seed.Bootstrap',
        },
        paragraphs: [
          'In production, the deploy scripts run migrations first and bootstrap after that. The API is restarted only after initialization succeeds.',
        ],
      },
      {
        id: 'adding-seeders',
        title: 'Adding custom seeders',
        items: [
          'Create a seeder class in Seed.Infrastructure/Persistence/Seeders.',
          'Register it in InfrastructureServiceCollectionExtensions.',
          'Call it from Seed.Bootstrap Program.cs in the application data seeding flow.',
          'Keep seeders idempotent so repeated deployments are safe.',
        ],
      },
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
    overview: [
      'The delivery pipeline covers the path from pull request checks to Docker image publishing and VPS deployment. It is designed for a product cloned from the seed, with project-specific values supplied through variables and environment files.',
      'The same documentation describes first deploy, VPS preparation, Cloudflare setup, GitHub variables/secrets, smoke tests, and operating commands.',
    ],
    status: 'Production-ready',
    audience: 'Useful when you want the cloned app to keep a ready delivery path instead of designing one later.',
    highlights: [
      'GitHub Actions for CI and image publishing',
      'Parameterized deploy workflow for seed reuse',
      'Docker-based runtime and deployment scripts',
    ],
    sections: [
      {
        id: 'workflows',
        title: 'GitHub Actions workflows',
        table: {
          headers: ['Workflow', 'Purpose'],
          rows: [
            ['ci.yml', 'Build and test pull requests and branch updates.'],
            ['docker-publish.yml', 'Build and push API and web images to GHCR.'],
            ['deploy.yml', 'Deploy to VPS over SSH with Docker Compose, migrations, bootstrap, and health checks.'],
            ['hotfix-backmerge.yml', 'Open a back-merge PR from master to dev after hotfixes.'],
          ],
        },
      },
      {
        id: 'branch-strategy',
        title: 'Branch strategy',
        items: [
          'Feature branches start from dev and merge back through pull requests.',
          'Hotfix branches start from master for urgent production fixes.',
          'After hotfixes, master changes are back-merged into dev.',
        ],
      },
      {
        id: 'first-deploy',
        title: 'First deploy path',
        items: [
          'Fork or create the product repository from the seed.',
          'Configure GitHub variables and secrets for image publishing and deploy.',
          'Prepare the VPS directory and .env file.',
          'Configure domain, Cloudflare DNS, SSL/TLS, and optional staging protection.',
          'Run the deploy workflow and verify health checks and smoke tests.',
        ],
      },
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
    overview: [
      'Operations documentation turns the seed into a deployable baseline rather than just application code. It covers monitoring, rollback, troubleshooting, and environment backup practices that become important immediately after first production deploy.',
      'Most of this capability is runbook structure plus Docker/CI conventions. A cloned product can keep the pieces it uses and delete what is not relevant.',
    ],
    status: 'Production-ready',
    audience: 'Important once the app moves beyond local development and starts needing repeatable operations.',
    highlights: [
      'Monitoring stack guidance with dashboards and metrics',
      'Rollback strategies for images, code, and database',
      'Centralized troubleshooting and environment backup docs',
    ],
    sections: [
      {
        id: 'monitoring',
        title: 'Monitoring stack',
        paragraphs: [
          'The monitoring docs describe a Docker-based stack with Prometheus, Grafana, cAdvisor, Node Exporter, Portainer, custom metrics, dashboards, and alerting guidance.',
        ],
        items: [
          'Prometheus scrapes application and infrastructure metrics.',
          'Grafana provides dashboards and alert visualization.',
          'cAdvisor and Node Exporter expose container and host metrics.',
          'Portainer provides operational visibility into Docker services.',
        ],
      },
      {
        id: 'rollback',
        title: 'Rollback strategy',
        paragraphs: [
          'Rollback guidance is split by impact. Prefer image rollback first, then code revert, and only restore the database from backup when data state requires it.',
        ],
        table: {
          headers: ['Scenario', 'Typical action'],
          rows: [
            ['Bad container image', 'Change image tag in .env and restart the stack.'],
            ['Bad code change', 'Revert in git and redeploy through CI/CD.'],
            ['Bad data migration', 'Use backup/restore procedure after assessing data loss impact.'],
          ],
        },
      },
      {
        id: 'env-backup',
        title: 'Environment backup',
        paragraphs: [
          'Production .env files contain deployment-critical configuration and secrets. The runbook documents daily backup with retention cleanup and restore steps.',
        ],
      },
      {
        id: 'troubleshooting',
        title: 'Troubleshooting hub',
        paragraphs: [
          'The troubleshooting document is the catch-all place for issues not tied to a specific module. It should grow with real deployment and development problems discovered while using the seed.',
        ],
      },
    ],
    routes: [],
    docs: ['docs/operations/monitoring.md', 'docs/operations/rollback.md', 'docs/operations/troubleshooting.md', 'docs/operations/env-backup.md'],
    codeAreas: ['docs/operations', 'docker'],
    setupNotes: [
      'Most of this value is documentation and runbook structure rather than UI.',
      'Keep only the operational pieces your cloned app will actually use.',
    ],
  },
  {
    slug: 'in-app-documentation',
    title: 'In-app documentation viewer',
    eyebrow: 'Built-in docs',
    summary:
      'Project documentation from docs/ is exposed as a navigable markdown viewer with sidebar index, sanitized rendering, and previous/next navigation.',
    overview: [
      'A build script copies selected markdown files from the repository docs/ folder into Angular public assets and generates a manifest. The viewer loads this manifest at runtime, renders each document with marked + DOMPurify, and presents it in a sidebar + content layout.',
      'The feature is frontend-only and requires no backend API. Documents can be included or excluded per-file via the build script, and the manifest is regenerated on every npm start or npm run build.',
    ],
    status: 'Included',
    audience: 'Use this to surface developer and ops documentation directly inside the running application.',
    highlights: [
      'Sidebar index grouped by category with active/hover styling',
      'Deep-linkable routes per document',
      'Sanitized markdown rendering with marked + DOMPurify (lazy loaded)',
      'Previous/next document navigation across all categories',
      'Per-file exclusion list for maintainer-only documents',
    ],
    sections: [
      {
        id: 'routes',
        title: 'Available routes',
        table: {
          headers: ['Route', 'Behavior'],
          rows: [
            ['/docs', 'Redirects to the first available document'],
            ['/docs/:category', 'Redirects to the first document in the category'],
            ['/docs/:category/:slug', 'Loads and renders the document'],
          ],
        },
      },
      {
        id: 'categories',
        title: 'Documentation categories',
        paragraphs: [
          'Six public categories are exposed: Getting Started, Architecture, Modules, Operations, Compliance, and Seed. Internal working folders (plans, requirements, wishes, skills) are excluded.',
        ],
      },
      {
        id: 'sidebars',
        title: 'Excluding documents',
        code: {
          language: 'javascript',
          value: "const EXCLUDED_FILES = new Set([\n  'docs/operations/auto-execute.md',\n  'docs/operations/adding-collaborators.md',\n]);",
        },
        paragraphs: [
          'Add a path to the EXCLUDED_FILES set in scripts/build-docs.mjs to hide it from the viewer without removing it from the repository.',
        ],
      },
    ],
    routes: ['/docs'],
    docs: ['docs/modules/in-app-documentation.md'],
    codeAreas: ['frontend/web/scripts/build-docs.mjs', 'frontend/web/projects/app/src/app/pages/docs/'],
    setupNotes: [
      'No setup needed — the feature is always available.',
      'New .md files added to any public docs/ subfolder appear automatically after the next npm start or npm run build.',
    ],
  },
];

export function findSeedFeatureBySlug(slug: string | null): SeedFeature | undefined {
  if (!slug) {
    return undefined;
  }

  return seedFeatures.find((feature) => feature.slug === slug);
}
