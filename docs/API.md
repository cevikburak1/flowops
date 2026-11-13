# API

The frontend forwards same-origin requests to ASP.NET Core. Authenticated writes using the session cookie require the configured Origin. API clients can present a Keycloak access token with audience `flowops-api`.

| Method | Path | Behavior |
| --- | --- | --- |
| GET | /api/me | Profile and tenant memberships |
| POST | /api/organizations | Create an organization and owner membership |
| GET | /api/organizations/{org}/settings | Member list, pending invitations, and purchase routing |
| POST | /api/organizations/{org}/invitations | Create an email-bound invitation and return the code once |
| POST | /api/organizations/{org}/invitations/{id}/revoke | Revoke a pending invitation |
| POST | /api/invitations/accept | Join the organization with the matching verified Keycloak email |
| POST | /api/organizations/{org}/members/{subject}/settings | Update role and manager assignment |
| POST | /api/organizations/{org}/routing | Update finance threshold and approver |
| GET | /api/organizations/{org}/workflows | List visible purchase workflows with optional `q`, `status`, and `page` filters |
| POST | /api/organizations/{org}/workflows | Create a purchase workflow draft |
| GET | /api/organizations/{org}/workflows/{id} | Read draft, published versions, and execution history as an owner or administrator |
| GET | /api/organizations/{org}/workflows/{id}/published | Get the active version available for submission |
| POST | /api/organizations/{org}/workflows/{id}/draft | Save a draft with its current revision |
| POST | /api/organizations/{org}/workflows/{id}/publish | Publish an immutable version of the saved draft |
| POST | /api/organizations/{org}/workflows/{id}/unpublish | Pause new submissions using the current draft revision |
| POST | /api/organizations/{org}/workflows/{id}/duplicate | Copy the saved settings to a new draft using `name` and `revision` |
| POST | /api/organizations/{org}/workflows/{id}/archive | Archive a workflow and stop new submissions |
| GET | /api/organizations/{org}/workflows/{id}/simulation | List saved scenarios, runs and selectable published versions with `page` and `versionPage` |
| POST | /api/organizations/{org}/workflows/{id}/simulation | Run with `input` containing `amount`, `manager`, `finance`, and either `version` or `revision`. Pass `scenarioName` to save the inputs. |
| GET | /api/organizations/{org}/workflows/{id}/simulation/runs/{runId} | Read the stored graph, inputs, trace and validation report |
| GET / POST | /api/organizations/{org}/forms | List forms or create a schema with `name` and `fields` |
| GET | /api/organizations/{org}/forms/{formId} | Read draft, current revision, and published version history |
| GET | /api/organizations/{org}/forms/{formId}/versions/{version} | Read the immutable field schema as a process editor |
| POST | /api/organizations/{org}/forms/{formId}/draft | Save `schema` with the current `revision`. Returns the resulting revision. |
| POST | /api/organizations/{org}/forms/{formId}/publish | Publish an immutable form version from the current `revision` |
| GET | /api/organizations/{org}/workspace | Personal requests and assigned approvals use `page`. Notifications use `notificationPage` and return `notificationHasMore`. Each list shows up to 10 items. Optional `months=6\|12\|48` adds personal purchase counts by UTC month and status. |
| GET | /api/organizations/{org}/tasks | Paginated approval tasks assigned to the current member. Filter with `status=pending|overdue|completed`, `priority=low|normal|high`, and `page` |
| POST | /api/organizations/{org}/purchases | Submit a purchase using a client-generated UUID for retries |
| GET | /api/organizations/{org}/purchases/{id} | Purchase, decision stages, and audit history |
| POST | /api/organizations/{org}/purchases/{id}/approvals/{stage} | Record an assigned decision |
| POST | /api/organizations/{org}/purchases/{id}/approvals/{stage}/delegate | The current reviewer transfers an open task to an eligible reviewer. Send `assignee` and `expectedAssignee`. The original deadline and Elsa bookmark are preserved. |
| POST | /api/organizations/{org}/purchases/{id}/cancel | The requester cancels a purchase waiting for a decision. The API records the cancellation and queues Elsa cleanup. |
| GET / POST | /api/organizations/{org}/approval-delegation | Read or update the caller's delegation window. Send `delegate`, `startsAt` and `endsAt`. Use a null delegate to remove the schedule. |
| GET / POST | /api/organizations/{org}/approval-matrix | Read the organization's rule list or save it with the current `revision` |
| POST | /api/organizations/{org}/approval-matrix/preview | Evaluate proposed rules against an amount, category, requester role and optional department |
| POST | /api/organizations/{org}/notifications/{id}/read | Mark an owned notification as read |
| GET | /health/live and /health/ready | Liveness and PostgreSQL readiness |

Missing memberships and inaccessible purchase records return 404. A decision accepts the same payload again and returns 409 for a conflicting replay. An invalid form returns 400. Development OpenAPI is available on the API port at `/openapi/v1.json`.

Organization settings require ownership or an administrator role. Only an owner can grant or remove the administrator role. Invitations expire after seven days and are stored as SHA-256 hashes. A new invitation for the same email revokes any previous pending link. Organization changes and invitation acceptance create tenant-scoped audit events.

A catalog purchase includes both `workflowId` and `workflowVersion` from the published endpoint. The API returns 409 for a stale version and 404 for unavailable workflows. Exact retries of a previously stored request can succeed even after archiving.

Purchase submission accepts an optional `priority` of `low`, `normal`, or `high` and defaults to `normal`. Approval tasks inherit purchase priority and receive a due date 48 hours after assignment. Pending tasks include overdue tasks. Dates returned by the API are in UTC.

Workflow detail returns the draft `graph`. Create and draft update accept an optional schema version 1 graph with nodes (`id`, `kind`, `x`, `y`) and edges (`source`, `target`, `branch`). Leaving the graph out of an update preserves the saved graph. Publishing an invalid purchase graph returns 400 with `invalidGraph`. Purchases running a compiled graph return the published graph and node states in `execution`, subject to purchase access rules.
## Operations API

| Method and path | Access and behavior |
| --- | --- |
| `POST /api/organizations/{org}/groups` | Owner or administrator creates a team or department. Body has `kind`, `name`, and optional `lead`. |
| `POST /api/organizations/{org}/groups/{groupId}` | Updates a group's name and approval lead. Group kind is fixed. |
| `POST /api/organizations/{org}/purchases/{id}/approvals/{stage}/assign` | Owner or administrator sends `assignee` and `expectedAssignee`. Stale assignments or completed decisions return 409. |
| `POST /api/organizations/{org}/integrations/webhook` | Owner or administrator enables or disables delivery to the operator-configured destination using `enabled`. |
| `POST /api/organizations/{org}/members/{subject}/remove` | Owner or administrator removes a member. Owners and the caller are protected. Only an owner can remove an administrator. Returns 409 while the member has active assignments or is part of approval routing. Repeating a successful removal returns 204. |
| `GET /api/organizations/{org}/reports?days=30` | Owner or administrator receives aggregates, audit events, and current notification retries. Supported windows are 7, 30, and 90 days. Optional `auditSource=organization` or `auditSource=purchase` filters the events. `auditPage` starts at 1 and returns up to 12 events per page, with `auditHasMore` indicating whether another page exists. |
| `GET /api/platform/organizations?page=1` | Configured platform operator lists organizations and usage. |
| `POST /api/platform/organizations/{org}/plan` | Configured platform operator sets `plan` to `starter`, `business`, or `enterprise`. |

Member updates accept an optional `groups` object with `teamId` and `departmentId`. A direct manager takes precedence over a team lead, followed by a department lead. Reassignment starts a new 48-hour window and records the actor and assignee. The notification hub sends a refresh signal to the authenticated recipient. Notification data is fetched separately and scoped to the organization.

Member removal revokes organization access and outstanding invitations for that address. Purchase records and historical approvals remain available to authorized users.

If the organization reaches its configured limit of non-archived workflows, create and duplicate return HTTP 409 with code `workflowPlanLimit`.

## General processes

Routes under `/api/organizations/{org}/processes` support listing, creating, viewing, saving drafts, and publishing. Draft and publish writes use the current revision. The `/{processId}/run` and `/{processId}/webhook` endpoints accept a client-generated UUID, the current published version, an answers object keyed by form field, and an optional attachment with `name` and base64 `content`. Repeat the same UUID and payload to retry a submission without starting another run.

The process catalog accepts `page` and returns up to 10 definitions. Process detail accepts `page` for run history and returns up to eight runs with their tasks. Purchase workflow detail accepts `page` for runs and `versionPage` for published versions.

A process draft can include `form: { id, version, inputMap }` to bind a published form in the same organization. The optional input map associates field keys with Elsa input names. For bound manual processes, post `version`, `revision` and `answers` to `/{processId}/submission-draft`. Include the resulting `draftRevision` in the final run request to clear that revision atomically with submission.

Use the organization session or a Keycloak access token for the webhook endpoint. The token's subject must have permission to start runs in the organization. Download via `/{processId}/runs/{runId}/attachment` as the submitter, an assigned participant, or a member with permission to view all runs. With S3 configured, attachments can be up to 10 MiB. Otherwise the limit is 1 MiB.

`GET /api/organizations/{org}/process-tasks` lists pending assignments for the caller, including the optional `dueAt`. Pass `page` to receive a page of up to 10 assignments with `page` and `hasMore`. Complete via `POST /{processId}/runs/{runId}/tasks/{stepId}/complete` under the process route. Only the current assignee can complete a task. Repeating completion returns 204.

The organization approval SLA policy supplies business-calendar deadlines for purchases and general processes. A process run snapshots the policy when started. A task can be reassigned when overdue; the completion route and deadline stay the same. The process detail returns `dueAt` and `escalatedAt` for visible runs and tasks. Workspace notifications for processes identify the `processId` and `processRunId`; purchase notifications identify the `purchaseId`.

`POST /api/organizations/{org}/settings/process-permissions` accepts the role-to-permission map returned by organization settings. The owner controls the `edit`, `run`, and `inspect` flags for configurable roles.
