# Technical decisions

- The Windows host originally had SDKs through .NET 8. A Microsoft-signed installer provided the workspace-local 11.0.100 SDK. The Docker build uses the matching SDK image.
- Elsa 3.9 selects its .NET 10 assemblies under the .NET 11 API. Its PostgreSQL persistence, pause/resume, and restart recovery worked in the first purchase slice, so Elsa stays in process. EF Core 10.0.15 and Npgsql EF provider 10.0.3 share the Elsa-compatible major version.
- Use the tenant's currency and finance threshold at submission and snapshot the assigned subjects. Serialize submission with membership and routing writes using a tenant row lock. Organization settings expose manager assignment, a finance threshold, and an optional finance approver.
- Use shareable, expiring links for invitations. Bind acceptance to the verified Keycloak email and keep the code in the URL fragment during sharing. Persist only a hash and never include the code in an organization audit event.
- Keep the local integration profile optional. SeaweedFS supplies S3-compatible object storage, and operator-managed n8n runs with a loopback-only editor.
- Keep the first slice local. Development seeding and Keycloak bootstrap credentials are supplied by the local operator.
