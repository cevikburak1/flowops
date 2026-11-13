# Workflow Studio 2.0 roadmap

- [x] Milestone 1. Audit the repository and document dependencies and acceptance criteria.
- [x] Milestone 2. Verify the purchase workflow simulator, scenario persistence, and authorization.
- [x] Milestone 3. Finish reusable form editing and verification.
  - [x] Verify versioned form editing, conditional validation, private submission drafts, and a bound form starting Elsa.
  - [x] Verify multi-step forms and configured Elsa input names.
  - [x] Verify restricted uploads and successful attachment storage in a real process run.
  - [x] Verify that the user selector and draft endpoint exclude users from another tenant.
  - [x] Verify department selection and reject unknown department IDs.
  - Repeating groups are deferred because the current Elsa input contract uses scalar answers.
- [x] Milestone 4. Implement approval policies, parallel quorum, request changes, and delegation management.
  - [x] Implement task-specific delegation with eligibility, audit and deadline preservation.
  - [x] Verify organization matrix editing, routing preview, revision conflicts, and a matched rule executing in Elsa.
  - [x] Verify concurrent parallel votes, quorum, duplicate replay, and a single final Elsa decision.
  - [x] Verify the changes-requested outcome, reviewer reason, and status in a live Elsa execution.
  - [x] Verify immutable matrix bindings, linked resubmission and requester cancellation.
  - [x] Verify scheduled delegation for new single-reviewer assignments in Playwright.
  - [x] Support scheduled and manual transfers for individual quorum participants; verify duplicate targets, cross-tenant targets, and deadline and quorum preservation in PostgreSQL.
- [x] Milestone 5. Implement SLA calendars and persistent escalation rules.
  - [x] Verify timezone and holiday calculation, persisted policy, and the deadline applied to a real approval.
  - [x] Verify SLA compliance counts and recovery of pending reminder and overdue work with fresh worker instances on the same PostgreSQL database.
  - [x] Verify fallback reassignment in PostgreSQL and the purchase-wide deadline in the browser.
  - [x] Verify general-process run and task deadlines in a live browser scenario.
  - [x] Verify general-process SLA escalation across a full API restart: preserve the deadline, reassign the task, deliver one in-app notification, and complete the Elsa run as the new assignee.
- [x] Milestone 6. Verify cross-module integration, restart recovery, and tenant authorization.
  - [x] Verify that a pending finance approval survives a real API restart and completes in Elsa.
  - [x] Pass all 10 Playwright scenarios with two real API restarts, all 19 backend tests, and frontend lint, type checks and production build.

See [technical design](docs/WORKFLOW_STUDIO_V2.md).

The approval matrix covers the purchase manager and finance stages; arbitrary workflow-variable predicates and additional stage types are outside this implementation. Purchase SLA fallback reassignment applies to a single reviewer; parallel participants receive reminders and can be transferred individually. The simulator records a trace after execution and does not expose a live debugger. External delivery depends on the configured notification channels.
