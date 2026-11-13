# Workflow examples

These examples are synthetic. They contain no credentials, tenant IDs or exported execution data.

| Example | What it shows |
| --- | --- |
| [Elsa: purchase approval](elsa/purchase-approval.json) | A manager approves first. Purchases that reach the configured threshold continue to finance. |
| [Elsa: parallel timers](elsa/parallel-timers.json) | A form starts an Elsa run. Both timer branches must finish before the run completes. |
| [n8n: notification receiver](n8n/flowops-notification-receiver.json) | A header-authenticated webhook acknowledges a FlowOps notification without forwarding it. |

## Run the Elsa examples

The JSON files are **FlowOps draft inputs**. FlowOps compiles them to Elsa definitions when you publish. See the [API reference](../../docs/API.md) for authentication and payloads.

1. As an organization owner or administrator, send the purchase JSON to `POST /api/organizations/{org}/workflows`. Publish the returned workflow ID with the draft revision. You can also build the same graph with the **Manager and finance** template in the UI.
2. Run the purchase simulator against the published version with amounts **4,000** and **6,000**. Compare the branches. To see the real Elsa wait states, submit a purchase using that published version in an organization with manager and finance reviewers.
3. Send the parallel timer JSON to `POST /api/organizations/{org}/processes`. Publish the returned process ID, then start a run with a synthetic reference in the process UI. It completes once both timers have fired.

## Run the n8n example

1. Start the optional integrations profile with `./infrastructure/dev.ps1 integrations`. In [local n8n](http://localhost:55678), import the JSON using **Import from File**. See [n8n workflow import](https://docs.n8n.io/workflows/export-import/).
2. Attach an n8n Header Auth credential to **FlowOps webhook**. Set its header name to `X-FlowOps-Key` and use a key generated locally. Publish the workflow.
3. Set `Integrations__Webhook__Url` to `http://localhost:55678/webhook/flowops-notifications` and `Integrations__Webhook__Key` to the same key in the local FlowOps API environment. Restart the API and enable webhook delivery for a demo organization via `POST /api/organizations/{org}/integrations/webhook` with `{"enabled":true}`.
4. Submit a demo purchase to generate a notification. The receiver acknowledges the event ID and kind. It never changes a FlowOps decision. This receiver has no downstream side effects; use the FlowOps event ID or `Idempotency-Key` to deduplicate if you extend it.

Keep the credentials in local service configuration. No cloud account or paid API is needed to run these examples locally.
