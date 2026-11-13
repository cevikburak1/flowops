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
| GET | /api/organizations/{org}/workspace | Paginated personal requests and assigned approvals with current counts and notifications |
| GET | /api/organizations/{org}/tasks | Paginated approval tasks assigned to the current member. Filter with `status=pending|overdue|completed`, `priority=low|normal|high`, and `page` |
| POST | /api/organizations/{org}/purchases | Submit a purchase using a client-generated UUID for retries |
| GET | /api/organizations/{org}/purchases/{id} | Purchase, decision stages, and audit history |
| POST | /api/organizations/{org}/purchases/{id}/approvals/{stage} | Record an assigned decision |
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
| `GET /api/organizations/{org}/reports?days=30` | Owner or administrator receives aggregates, recent audit events, and current notification retries. Supported windows are 7, 30, and 90 days. |
| `GET /api/platform/organizations?page=1` | Configured platform operator lists organizations and usage. |
| `POST /api/platform/organizations/{org}/plan` | Configured platform operator sets `plan` to `starter`, `business`, or `enterprise`. |

Member updates accept an optional `groups` object with `teamId` and `departmentId`. A direct manager takes precedence over a team lead, followed by a department lead. Reassignment starts a new 48-hour window and records the actor and assignee. The notification hub sends a refresh signal to the authenticated recipient. Notification data is fetched separately and scoped to the organization.
