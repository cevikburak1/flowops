# Testing

From the repository root, use `npm.cmd --prefix apps/web run lint`, `npm.cmd --prefix apps/web run typecheck`, and `npm.cmd --prefix apps/web run build`. The backend targets SDK 11.0.100. Run `dotnet build src/FlowOps.Api` from `backend`.

With Docker, API, and web running, use `./infrastructure/dev.ps1 test`. Playwright uses the local Keycloak test realm and checks purchase approval and rejection, tenant and decision authorization, request replay, concurrent decisions, notifications, organization creation and switching, sign-out, and theme and language controls.

The first checkpoint passed all four Playwright tests. A separate manual recovery check restarted the API with a persisted finance bookmark, then approved the purchase and verified the resulting audit event. Hallmark rendered five public-page screenshots. The authenticated browser audit inspected the overview, request form, and approval center at the four target widths, with automated accessibility samples at desktop and mobile sizes.

The organization settings checkpoint passed all five tests. The new scenario verifies that the wrong Keycloak account cannot use an invitation, a recipient can sign in and join, an owner can assign the manager and configure the threshold, and a purchase in the new organization reaches its own manager. It also checks invitation revocation, tenant settings authorization, Turkish localization, four viewport widths, and light and dark axe samples.

The workflow catalog checkpoint passed all five tests. The expanded organization scenario creates a draft in the browser, verifies access boundaries, publishes two immutable versions, rejects a stale submission, and completes an Elsa execution on its original version after archiving the workflow. It also checks the catalog at four widths and runs axe against the draft detail in dark mode.

The purchase workflow editor checkpoint verifies four viewport widths and runs axe in dark mode. Playwright saves an incomplete graph, checks that both UI and API block publishing, repairs the connection, reloads the graph, and publishes it. A request above the finance threshold completes in Elsa after manager approval only, even after a newer graph enables finance approval.

The compiled workflow checkpoint passed all five Playwright tests. It exercised both outcomes of the manager decision, verified that a notification node ran once on approval and was skipped on rejection, and completed a separate compiled purchase through finance. The execution canvas uses persisted Elsa activity records.

See TASKS.md for executed checks and outstanding test coverage.
