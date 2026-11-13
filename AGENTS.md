# FlowOps

Read TASKS.md and inspect the affected implementation before changing code. Keep milestones and verification results current. PRODUCT.md defines the product, DESIGN.md defines the visual system, and ARCHITECTURE.md records system boundaries.

## Engineering

- Deliver tested vertical slices. Expose routes and controls as their real application behavior becomes available.
- Use the Next.js App Router, strict TypeScript, and server-side OIDC sessions. Keep credentials and tokens out of client bundles and browser storage.
- Keep the main API on .NET 11/C# 15. Verify Elsa compatibility with an execution test before deciding on a .NET 10 workflow host.
- Validate Keycloak tokens and resolve organization membership server-side. Scope every query, write, background operation, and notification by organization. Test authorization, cross-tenant isolation, retries, and concurrent decisions.
- Use migrations, durable approval tasks, idempotent workflow actions, and a transactional outbox. Elsa owns business execution. n8n owns internal integrations subject to licensing.
- Run relevant builds, tests, and lint. Record actual results and blockers. Keep local configuration in .env.example and use local Git until a remote or deployment is authorized.

## Design workflow

For substantial UI changes, inspect the existing UI and tokens, read PRODUCT.md and DESIGN.md, and load the project Impeccable skill. Use Hallmark for new major screens, significant redesigns, or a new direction. After implementing connected UI, use the relevant Impeccable critique/audit and apply Unslop to visual patterns. Fix findings while preserving business behavior. Pick only relevant guidance for small changes and keep existing design context.

Run Playwright against the real application at 1440, 1024, 768, and 390px. Check light/dark, keyboard access, form validation, interactions, and API connectivity. Keep English and Turkish strings in structured dictionaries.
