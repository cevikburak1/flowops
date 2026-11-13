# Testing

From the repository root, use `npm.cmd --prefix apps/web run lint`, `npm.cmd --prefix apps/web run typecheck`, and `npm.cmd --prefix apps/web run build`. The backend targets SDK 11.0.100. Run `dotnet build src/FlowOps.Api` from `backend`.

With Docker, API, and web running, use `./infrastructure/dev.ps1 test`. Playwright uses the local Keycloak test realm and checks purchase approval and rejection, tenant and decision authorization, request replay, concurrent decisions, notifications, organization creation and switching, sign-out, and theme and language controls.

The first checkpoint passed all four Playwright tests. A separate manual recovery check restarted the API with a persisted finance bookmark, then approved the purchase and verified the resulting audit event. Hallmark rendered five public-page screenshots. The authenticated browser audit inspected the overview, request form, and approval center at the four target widths, with automated accessibility samples at desktop and mobile sizes.

The organization settings checkpoint passed all five tests. The new scenario verifies that the wrong Keycloak account cannot use an invitation, a recipient can sign in and join, an owner can assign the manager and configure the threshold, and a purchase in the new organization reaches its own manager. It also checks invitation revocation, tenant settings authorization, Turkish localization, four viewport widths, and light and dark axe samples.

The workflow catalog checkpoint passed all five tests. The expanded organization scenario creates a draft in the browser, verifies access boundaries, publishes two immutable versions, rejects a stale submission, and completes an Elsa execution on its original version after archiving the workflow. It also checks the catalog at four widths and runs axe against the draft detail in dark mode.

The purchase workflow editor checkpoint verifies four viewport widths and runs axe in dark mode. Playwright saves an incomplete graph, checks that both UI and API block publishing, repairs the connection, reloads the graph, and publishes it. A request above the finance threshold completes in Elsa after manager approval only, even after a newer graph enables finance approval.

The compiled workflow checkpoint passed all five Playwright tests. It exercised both outcomes of the manager decision, verified that a notification node ran once on approval and was skipped on rejection, and completed a separate compiled purchase through finance. The execution canvas uses persisted Elsa activity records.

See TASKS.md for executed checks and outstanding test coverage.

The general process browser tests cover publication, parallel timers, webhook replay, form submission, attachment isolation, and editor/operator permissions. To run the local HTTP receiver test, set `Processes__Targets__receipt=http://127.0.0.1:5084/receipt` for the API and `FLOWOPS_TEST_PROCESS_HTTP=1` for Playwright. With SeaweedFS running, set `FLOWOPS_TEST_PROCESS_STORAGE=1` to exercise a 10 MiB attachment together with the assigned-task and permission scenarios.

From `backend`, run `dotnet run --project tests/FlowOps.Api.Tests -c Release` with Docker running. The xUnit suite applies the real FlowOps migrations to disposable PostgreSQL containers, checks concurrent workflow quota enforcement and tenant isolation, and verifies that the notification and its delivery rows are persisted together. It also checks production authentication settings and key-ring reuse.

With Docker dependencies running and the API and web ports free, run `./infrastructure/ci-browser.ps1` to exercise the restart scenario used in CI. Playwright requests a restart after confirming that the purchase is waiting for finance. The runner stops the API process, starts a new one against the same database, and waits for readiness before Playwright verifies and completes the pending approval.

To include the invitation email assertion, start the integrations profile and set the following environment variables before starting the API and browser runner.

```powershell
$env:Integrations__Smtp__Host = 'localhost'
$env:Integrations__Smtp__Port = '51025'
$env:Integrations__Smtp__From = 'flowops@example.test'
$env:Integrations__Smtp__Tls = 'false'
$env:FLOWOPS_TEST_MAILPIT_ORIGIN = 'http://127.0.0.1:58025'
./infrastructure/ci-browser.ps1
```
