# Workflow Studio 2.0

## Repository audit

The Next.js workspace forwards authenticated requests to the ASP.NET Core API. Keycloak provides the subject and the database provides tenant membership and permissions. The purchase catalog already supports revision-checked drafts, immutable published snapshots, and an Elsa compiler for the purchase graph. General processes use a separate versioned Elsa sequence with built-in form fields, parallel branches, timers, task assignments, and attachments.

Purchase approvals currently use a manager bookmark, an optional finance bookmark, and a transactional command queue. Decisions, reassignment, and overdue notifications already have audit or delivery records. Deadlines currently use a fixed elapsed-time rule. Existing browser scenarios cover real purchases, concurrent decisions, process attachments, role isolation, and the restart path in CI.

## Implementation and acceptance

| Milestone | Design and dependencies | Acceptance |
| --- | --- | --- |
| Simulator | Use the validated purchase graph and its stored threshold. Run an isolated interpreter with bounded steps and time. Store scenario inputs and run snapshots under tenant-scoped keys. | Draft revision conflicts, immutable version selection, branch and approval outcomes, validation, trace, history, export, comparison, and role isolation. Verify that live requests, tasks, and notifications are untouched. |
| Dynamic forms | Extend process form validation with reusable versioned schemas, immutable bindings, draft submissions, and typed input mapping. Reuse the process attachment and submission transaction. | Conditional server validation, published version stability, access checks, and an Elsa execution started by a validated form. |
| Approval policies | Snapshot policies at workflow start. Define explicit ordering, fallback and currency semantics. Model multiple approvers beneath a persistent stage and resume its bookmark once when the decision is final. | Deterministic matrix selection, parallel quorum, sequential stages, rejection, request changes, delegation, expiry, and concurrent replay. |
| SLA | Calculate due instants from versioned time-zone calendars. Persist deadlines and idempotent escalation work. Reuse the notification outbox and approval reassignment rules. | DST and holiday calculations, warning delivery, escalation history, restart recovery, and tenant isolation. |

The simulator uses synthetic actors and records notification intent in its trace. The purchase Elsa activities depend on live purchase records and approval tasks, so the simulator does not invoke them. Legacy published snapshots that predate graph compilation use the equivalent default path. General-process simulation and live breakpoint debugging are outside the first simulator implementation.

## Simulator behavior

Open a purchase workflow as an organization owner or administrator and choose **Workflow simulator**. Select a saved draft or a published version. Each run stores the selected graph, inputs, threshold, validation issues and executed trace in PostgreSQL. An older result always shows its stored snapshot. Saved draft scenarios reuse their inputs against the current saved draft. Trace review highlights recorded steps after the run has completed.

## Dynamic forms

Process editors can create reusable forms with text, long text, whole number, decimal, date, email, select and checkbox fields. A field can depend on an earlier answer and become required when it is visible. The editor supports drag reordering, keyboard alternatives, undo and redo, debounced autosave with revision checks, a narrow preview and publication history. Server validation rejects unknown keys and values supplied for hidden fields.

Bind a form ID and published version in a process draft, then publish the process. Manual and webhook requests use the bound schema. Each successful submission snapshots the validated answers, uses stable field keys as Elsa input names and records an audit event. Manual submissions can save and reload revision-checked draft answers. Files use the process attachment setting and are uploaded with final submission.

The field library also includes currency, date ranges, phone, radio groups, multiple choices, toggles, static content, directory selectors, and a restricted attachment on the final step. Fields can use validated defaults, length and numeric bounds, non-backtracking regex, conditional required rules, and two-column placement. Step numbers divide the form into up to five pages. A binding can map stable field keys to explicit Elsa input names. Repeating groups remain open because the submission contract uses scalar answers.

## Approval delegation

The current reviewer can transfer an open purchase task to a member eligible for its stage. A parallel reviewer transfers only their own pending vote; administrators can reassign any pending participant. The requester and reviewers already assigned to the stage or another stage are excluded. Delegation preserves the deadline, escalation state and Elsa bookmark. Assignment history, the purchase timeline and the notification outbox record the transfer.

The organization approval matrix applies to standalone purchases. A workflow editor can also bind the organization matrix, which is then copied into the published version. The lowest unique matching priority wins. Amount ranges include their lower bound and exclude their upper bound. Rules match the organization currency, category, requester role and department. The applicable threshold supplies the default route, and finance routing requires a finance node in a published graph.

An organization rule can snapshot up to eight additional manager-role reviewers and a minimum number of votes. A quorum of one accepts any one reviewer, and a quorum equal to the reviewer count requires all reviewers. The stage rejects when pending votes cannot reach quorum. The API locks the stage and records votes and its single Elsa resume command transactionally. Request changes records a reason and closes the current execution. The requester can open one linked revision using the current publication of the original workflow. The requester can also cancel a waiting purchase, which queues an Elsa cancellation. Reviewers can schedule delegation of new tasks for a UTC period. Parallel scheduling reserves the original participants and accepts at most one transfer to each eligible substitute.

## Approval SLA

The organization policy sets a time zone, working days, opening hours, holidays, a business-minute deadline and an optional warning. Purchases and general-process runs can snapshot an overall deadline that caps their approval or task deadlines. The worker locks an organization and commits the reminder or overdue marker, audit event and notification outbox together. Escalation can mark overdue, notify an administrator, manager or department lead, or reassign a single-reviewer purchase approval or an eligible process task. Reassignment preserves the process deadline and Elsa wait state. Process notifications link to the affected run. The operations report counts purchase decisions made within their deadline and includes process escalation events.
