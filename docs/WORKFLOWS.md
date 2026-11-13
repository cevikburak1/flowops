# Purchase approval

The API records the request, audit entry, and start command in one PostgreSQL transaction. The worker executes the published C# Elsa definition `flowops-purchase-approval`. The stable workflow instance ID is the organization UUID and purchase UUID joined by a colon.

1. Elsa persists a manager bookmark. The worker projects it into an assigned task and notification.
2. The manager records a decision. A concurrency token and unique command key prevent a second decision from changing it.
3. The worker resumes the exact bookmark. Purchases over the organization threshold require a separate finance approver after manager approval.
4. A finished Elsa instance is projected to the final purchase status, requester notification, and audit record.

The queue leases commands with PostgreSQL row locks and retries with backoff. When a worker restarts after Elsa advanced, it reads the instance and projects its persisted progress. Each request snapshots the assigned manager and finance approver at submission. Owners and administrators assign managers, set the finance threshold, and optionally select a specific approver. Without a specific finance approver, the system selects an eligible organization member with the Approver role. Requests above the threshold require a finance approver distinct from the requester and manager. Role changes that would leave active requests without an authorized reviewer are rejected.

## Purchase workflow catalog

Owners and administrators can create a workflow from the purchase approval template, edit its name and finance threshold, and publish successive versions. Each publication stores an immutable snapshot and pins the Elsa engine definition version. Members can open a published workflow and submit a request with the version shown in the form. If a newer version was published in the meantime, the API returns a conflict so the requester can review the current version.

The submission transaction stores the published workflow version and resolves the tenant's approvers. Draft changes, publication and archiving do not rewrite an existing request. Archiving removes the workflow from member discovery and prevents new submissions. Owners and administrators can inspect past executions and open the corresponding purchase record from the catalog.

The existing purchase form uses the organization routing threshold when opened without a catalog workflow.

The purchase workflow editor stores a versioned graph alongside the draft. It supports Start, Manager approval, Manager decision, Finance threshold, Finance approval, Notify requester, and End. Incomplete drafts can be saved. Publishing validates reachability, cycles, branch exits, and approval order, then compiles the graph into a persistent Elsa Flowchart. Each catalog version points to its own Elsa definition, so later edits do not change running instances. Older versions use the original purchase approval definition.

Manager decision reads the actual manager approval outcome. Finance threshold uses the finance assignment resolved when the request was submitted and sends rejected requests past finance. Notify requester creates one in-app notification per execution. The purchase detail draws the published snapshot and marks node states using Elsa activity records.

Use the catalog search to match workflow names. Owners and administrators can filter drafts, published, unpublished, and archived workflows. Unpublishing hides the workflow from members and stops new submissions while preserving versions and active executions. Publishing again creates a new numbered version. Duplicating creates an independent draft with the saved finance threshold and a name chosen by the administrator.

## General processes

Open **Processes** to create a draft with a manual form or an authenticated webhook trigger. Add text, number and date fields, and optionally require one attachment up to 10 MiB with S3 storage configured. Published revisions compile into Elsa sequences. Parallel groups wait for all branches, and timer and assignment bookmarks persist in PostgreSQL. A run keeps the published definition it started with. Drag nodes to arrange the canvas and use the step controls to change execution order.

The HTTP step sends a POST with the organization ID, run ID, and step ID. A named target is configured by the operator using `Processes__Targets__<name>`. Use HTTPS in production. Receivers can deduplicate using the `Idempotency-Key` header. Form answers and attachment contents remain in PostgreSQL.

Organization owners can configure edit and publish, start runs, and view all runs separately for each member role. Owners and administrators retain all process permissions. Assigned members can open their tasks from **My tasks**, access the associated run and attachment, and resume Elsa by completing the task. Other participants can access their own submissions. The API checks organization membership on each request.
