# Deployment

The Compose file is for local development. PostgreSQL, Keycloak, RabbitMQ, and optional Mailpit bind their host ports to loopback. Development realm users and credentials come from the local environment.

## Production topology

Use a dedicated Keycloak realm and HTTPS. Configure the confidential OIDC client, its PKCE callback, allowed logout URIs, and the API audience mapper. Route public traffic to Next.js and keep the API, PostgreSQL, and RabbitMQ private.

The API and web Dockerfiles build independently using `docker build -t flowops-api:local backend` and `docker build -t flowops-web:local apps/web`. Set the web image build argument `FLOWOPS_API_ORIGIN` to the internal API URL and use the same value at runtime. Configure the API with `ConnectionStrings__FlowOps`, `Auth__Authority`, `Auth__ClientSecret`, and `Auth__WebOrigin`. Route public HTTPS traffic to the web container and keep the API private.

| API setting | Value |
| --- | --- |
| `Auth__DataProtectionKeyPath` | Absolute, existing directory on a persistent volume writable by the API runtime user. Restrict access to the API identity and encrypt the backing volume. |
| `Auth__TrustedProxy` | IP of the direct proxy when it forwards protocol or client IP information. |
| `Integrations__RabbitMq` | AMQP(S) URI for the confirmed publisher. Use a dedicated identity and virtual host. |
| `Integrations__Smtp__Host` / `Port` / `From` | Optional SMTP relay and sender. Port defaults to 587. |
| `Integrations__Smtp__Tls` / `User` / `Password` | STARTTLS is enabled by default. Set relay credentials when required. |
| `Integrations__Webhook__Url` / `Key` | Optional operator-managed HTTPS destination and header authentication secret. |
| `Platform__OperatorSubjects__0` | Keycloak subject allowed to access platform administration. Use additional numbered keys for additional operators. |

Use one API replica for SignalR delivery with the current in-process hub. Replicas require session key sharing and a SignalR scale-out configuration. Keep operator subjects narrowly scoped. Plans record a tier and the audit event for a tier change.

## Notification destinations

The API publishes persistent RabbitMQ messages to `flowops.notifications`. An n8n operator can configure a production webhook with Header Auth named `X-FlowOps-Key`, use its URL and secret as the API settings above, and enable it for the organization in FlowOps. Use the notification ID or `Idempotency-Key` to deduplicate repeated deliveries. The payload contains the event ID, organization ID, purchase ID, event type, and timestamp.

In local development, run `./infrastructure/dev.ps1 integrations` and configure SMTP host `localhost`, port `51025`, sender `flowops@example.test`, and TLS `false`. View captured mail at [Mailpit](http://localhost:58025). SMTP sends purchase updates to members with stored email addresses.

## Recovery

1. Back up both PostgreSQL databases and the API key volume. Protect backups with access controls and encryption.
2. Restore into an isolated environment. Verify Keycloak login and organization isolation, then resume a request with a pending Elsa bookmark.
3. During a release, stop additional API instances before applying migrations. Inspect readiness and the operations report after startup. Verify RabbitMQ, SMTP, and webhook delivery separately.
4. If an upgrade requires rollback across schema changes, restore the compatible database and application versions together.

GitHub Actions builds and tests the project. See [implementation checkpoints](../TASKS.md) for live rollout and restore verification status.
