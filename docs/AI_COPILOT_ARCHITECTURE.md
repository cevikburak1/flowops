# AI Copilot architecture

The first slice adds purchase workflow proposals to the existing workflow catalog. Phase 1 provides the purchase DSL, bounded simulator, versioned drafts, form definitions, approval matrix and SLA policies. Elsa still owns published execution. Keycloak and FlowOps memberships still control access.

## Provider and proposal

The API uses an internal provider interface with an OpenAI Responses implementation over .NET HTTP. The endpoint is fixed and redirects are disabled. The operator configures the provider key and model in the API process environment. Model access is checked on first request. Generation defaults to off. The request uses a strict structured response schema and disables response storage. [OpenAI Responses API](https://developers.openai.com/api/reference/resources/responses/methods/create)

Copilot v1 sends the administrator's prompt and organization currency. It passes no form contents, membership lists, logs, credentials or production workflow records as context. It registers no tools and accepts no code, SQL, resource IDs or execution commands. The response names a supported route and threshold, or explains an unsupported request. The backend compiles a graph from the accepted route, validates it using the existing DSL and runs a sample in the bounded simulator. The user reviews the preview and uses the ordinary draft endpoint to save. Existing publication and quotas apply.

## Usage and failure handling

A short transaction locks the organization and reserves usage before calling the provider. Each organization has a UTC monthly request limit, and each user has four requests per minute within that organization. Reservations count even when the provider fails. The usage table stores tenant, subject, model, timing, status and reported tokens. It stores no prompt or response. Estimated cost is null unless both configured prices and both token counts are available. The API uses a capped output budget, linked cancellation, a deadline and one retry on HTTP 503 within that deadline. Provider failures and invalid proposals become bounded API errors.

## Local configuration

See the optional `Copilot__` settings in [the environment example](../.env.example). Export them in the backend process environment before launch. Keep the key in local secret configuration. Leave generation disabled to use the standard product without provider calls. Use a model that supports Responses structured outputs. The live provider may incur charges when enabled.

## Additional Copilot surfaces

Workflow detail supports analysis of a selected draft or published version and reviewable name or finance threshold edits. The edit retains the source graph and applies only through the existing revision checked draft endpoint. The last four questions stay in the open panel. Form proposals use a strict schema and the Studio form validator, then save through the ordinary form draft API. Reports send tenant-scoped counts grouped by status for 7, 30 or 90 days, plus the current overdue count. Optimization receives the workflow graph, organization SLA summary, and 30 days of purchase counts grouped by status. Failed process analysis uses version, status and retry count. It does not send raw logs or form answers.

All surfaces share the same meter. Reports show reported input and output tokens and the known estimated cost. An operator can optionally restrict Copilot to a list of organization IDs.

## Follow-up milestones

Workflow edits also support inserting one requester notification on an existing terminal edge. The server preserves the edge's branch, validates the resulting graph and returns both the original and proposed draft. The panel highlights the added node and identifies the affected connection. The existing draft endpoint checks the revision when the user applies the proposal.

Workflow administrators can explicitly save and reuse up to 20 questions per workflow. The records are private to the authenticated subject and organization. Set `Copilot__HistoryDays` to 1–90 to enable saving. The default is zero. Each record expires based on the policy in effect when saved. The API hides expired entries immediately and a background task removes them hourly. Clearing history deletes only the current user's saved questions. Saved questions are sent to the provider only when a user reuses and submits them.

The workspace header provides shortcuts to generation, forms, analytics and the current workflow's Copilot panel. An administrator can explicitly save a question together with its answer. The answer is stored as private reference text and is displayed as plain text. It is not sent as model context when the question is reused.

Live provider verification, general graph edits, persisted proposal history, a standalone assistant conversation, richer failure traces, latency and department analytics, per-tenant self-service settings and broader end-to-end testing remain open. Extra approval roles, custom SLA changes, form bindings and variable mappings require later contracts. No automatic repair call is enabled.
