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

Set `Plans__starter__ActiveWorkflows`, `Plans__business__ActiveWorkflows`, or `Plans__enterprise__ActiveWorkflows` to a nonnegative count to enforce a workflow limit for that plan. Omit a setting to leave that plan unlimited. The limit counts drafts and all other non-archived workflows. It applies to new workflows and copies. A lower limit leaves existing workflows usable and blocks new ones until capacity is available.

Production startup requires HTTPS for both the public origin and Keycloak issuer. The API checks that it can use the configured Data Protection key ring before starting. Test the mounted directory under the runtime user, then restart the deployment and verify that a preexisting session and queued invitation remain readable.

## Notification destinations

The API publishes persistent RabbitMQ messages to `flowops.notifications`. An n8n operator can configure a production webhook with Header Auth named `X-FlowOps-Key`, use its URL and secret as the API settings above, and enable it for the organization in FlowOps. Use the notification ID or `Idempotency-Key` to deduplicate repeated deliveries. The payload contains the event ID, organization ID, purchase ID, event type, and timestamp.

### Verify n8n delivery locally

Use a dedicated local workflow and a test organization. In n8n, add a POST Webhook node with Header Auth and set its credential header to `X-FlowOps-Key`. Configure the API with the node's production URL and the matching credential value. Enable webhook delivery in **Organization settings**.

Leave the n8n workflow unpublished and submit a test purchase. The failed delivery appears under **Operations report → Delivery retries**. Publish the workflow and verify the retry reaches n8n and disappears from the report. An unauthenticated request to the published endpoint should return 403. If downstream work needs to finish before FlowOps marks the delivery successful, configure n8n to respond after that work completes.

In local development, run `./infrastructure/dev.ps1 integrations` and configure SMTP host `localhost`, port `51025`, sender `flowops@example.test`, and TLS `false`. View captured mail at [Mailpit](http://localhost:58025). SMTP sends purchase updates to members with stored email addresses and sends invitations to their named recipients. The API queues invitations with SMTP configured and retries delivery until successful or until the invitation expires. Pending invitation codes are protected using the API key ring and cleared after delivery, acceptance, revocation, or expiration. Administrators can also copy the link from organization settings.

### Verify object storage locally

After starting the integration profile, run `./infrastructure/verify-s3.ps1` with PowerShell 7 and curl with AWS Signature Version 4 support. The script uses the development credentials from `.env` to write, read, and delete a unique object on the loopback S3 endpoint. It also checks that unsigned reads are rejected.

Process attachments larger than 1 MiB use private S3-compatible storage. The local API script configures SeaweedFS using the development storage credentials. Configure `Processes__Storage__Endpoint`, `Processes__Storage__Bucket`, `Processes__Storage__AccessKey`, `Processes__Storage__SecretKey`, and optionally `Processes__Storage__Region` in production. Allow up to 15 MB of request body through the reverse proxy for the base64 upload format. Back up the object bucket alongside PostgreSQL so stored attachments remain available after recovery.

## Recovery

1. Back up both PostgreSQL databases and the API key volume. Protect backups with access controls and encryption.
2. Restore into an isolated environment. Verify Keycloak login and organization isolation, then resume a request with a pending Elsa bookmark.
3. During a release, stop additional API instances before applying migrations. Inspect readiness and the operations report after startup. Verify RabbitMQ, SMTP, and webhook delivery separately.
4. If an upgrade requires rollback across schema changes, restore the compatible database and application versions together.

GitHub Actions builds and tests the project. See [implementation checkpoints](../TASKS.md) for live rollout and restore verification status.

The local recovery drill restored the FlowOps database, including the Elsa schema, into a separate PostgreSQL database. The restored API completed a purchase that had been waiting for a manager decision at backup time.
