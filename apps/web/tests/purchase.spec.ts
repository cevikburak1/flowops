import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const workspace = "/workspace/14e41a68-86a8-4653-980c-07e1ec2b0562";
const organizationApi = "/api/organizations/14e41a68-86a8-4653-980c-07e1ec2b0562";
const origin = "http://localhost:3000";

async function login(page: Page, user: string) {
  const password = process.env.FLOWOPS_DEV_PASSWORD;
  if (!password) throw new Error("Run the test action through infrastructure/dev.ps1.");
  await page.goto("/auth/login");
  await page.locator("#username").fill(user);
  await page.locator("#password").fill(password);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//, { timeout: 30000 });
}

test("public page adapts to four viewport widths and both themes", async ({ page }) => {
  await page.goto("/");
  for (const width of [1440, 1024, 768, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Switch color theme" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Language" }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "tr");
});

test("a persisted purchase reaches manager and finance before completion", async ({ browser }) => {
  const employee = await browser.newPage({ viewport: { width: 390, height: 844 } });
  const manager = await browser.newPage();
  const finance = await browser.newPage();
  await login(employee, "employee");
  const navigation = employee.locator("summary[aria-label='Workspace navigation']");
  await expect(navigation).toBeVisible();
  await navigation.press("Space");
  await expect(employee.locator(".mobile-nav")).toHaveJSProperty("open", true);
  await navigation.press("Space");
  await employee.getByRole("link", { name: "New purchase request" }).click();
  await employee.getByRole("button", { name: "Submit request" }).click();
  expect(await employee.locator("input[name=title]").evaluate((input: HTMLInputElement) => input.validity.valueMissing)).toBe(true);
  const title = `Field equipment ${Date.now()}`;
  await employee.getByLabel("Purchase title").fill(title);
  await employee.getByLabel(/Amount/).fill("6400");
  await employee.getByLabel("Category").selectOption("equipment");
  await employee.locator("select[name=priority]").selectOption("high");
  await employee.getByLabel("Business reason").fill("Replace the inspection equipment used by field staff.");
  await employee.getByRole("button", { name: "Submit request" }).click();
  await expect(employee.getByRole("heading", { name: title })).toBeVisible();
  const detail = new URL(employee.url()).pathname;
  const purchaseId = detail.split("/").at(-1);
  expect((await employee.request.get("/api/organizations/17213e02-c104-49cf-93b5-a625628e5906/workspace")).status()).toBe(404);
  const approval = `${organizationApi}/purchases/${purchaseId}/approvals/1`;
  expect((await employee.request.post(approval, { headers: { origin }, data: { decision: "approved" } })).status()).toBe(404);

  await login(manager, "manager");
  await expect.poll(async () => {
    const response = await manager.request.get(`${organizationApi}/workspace`);
    return (await response.json()).approvals.some((item: { id: string }) => item.id === purchaseId);
  }).toBe(true);
  const queue = `${organizationApi}/tasks`;
  const assigned = await (await manager.request.get(`${queue}?status=pending&priority=high`)).json();
  const task = assigned.items.find((item: { id: string }) => item.id === purchaseId);
  expect(task).toMatchObject({ stage: 1, priority: "high", overdue: false });
  expect(Date.parse(task.dueAt) - Date.now()).toBeGreaterThan(47 * 60 * 60 * 1000);
  const other = await (await employee.request.get(`${queue}?priority=high`)).json();
  expect(other.items.some((item: { id: string }) => item.id === purchaseId)).toBe(false);
  expect((await manager.request.get("/api/organizations/17213e02-c104-49cf-93b5-a625628e5906/tasks")).status()).toBe(404);
  await manager.getByRole("link", { name: "My tasks" }).click();
  for (const width of [1440, 1024, 768, 390]) {
    await manager.setViewportSize({ width, height: 900 });
    expect(await manager.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  await manager.locator("select[name=priority]").selectOption("low");
  await manager.getByRole("button", { name: "Apply filters" }).click();
  await expect(manager).toHaveURL(/priority=low/);
  await expect(manager.getByRole("link", { name: title })).toHaveCount(0);
  await manager.locator("select[name=priority]").selectOption("high");
  await manager.getByRole("button", { name: "Apply filters" }).click();
  await manager.getByRole("link", { name: title }).click();
  await manager.getByRole("button", { name: "Approve", exact: true }).click();
  const beforeConfirmation = await manager.request.get(`${organizationApi}/purchases/${purchaseId}`);
  expect((await beforeConfirmation.json()).stages[0].decision).toBeNull();
  await manager.getByRole("button", { name: /Confirm decision.*Approve/ }).click();
  await expect.poll(async () => {
    const response = await employee.request.get(`${organizationApi}/purchases/${purchaseId}`);
    return (await response.json()).status;
  }).toBe("financeReview");
  await manager.goto(`${workspace}/tasks?status=completed&priority=high`);
  await expect(manager.getByRole("row").filter({ hasText: title }).getByText("Approved")).toBeVisible();
  expect((await manager.request.post(approval, { headers: { origin }, data: { decision: "rejected" } })).status()).toBe(409);
  expect((await manager.request.post(approval, { headers: { origin: "https://example.test" }, data: { decision: "approved" } })).status()).toBe(403);

  await login(finance, "finance");
  await finance.goto(`${workspace}?view=approvals`);
  await finance.getByRole("link", { name: title }).click();
  await finance.getByRole("button", { name: "Approve", exact: true }).click();
  const subscribed = employee.waitForResponse(response => response.url().includes("/notifications/hub") &&
    response.request().method() === "POST" && (response.request().postData() ?? "").includes('"protocol":"json"') && response.ok());
  await employee.goto(`${workspace}?view=notifications`);
  await subscribed;
  await finance.getByRole("button", { name: /Confirm decision.*Approve/ }).click();
  await expect.poll(async () => {
    const response = await employee.request.get(`${organizationApi}/purchases/${purchaseId}`);
    return (await response.json()).status;
  }).toBe("approved");
  const notification = employee.getByRole("listitem").filter({ has: employee.getByRole("link", { name: title }) });
  await expect(notification.getByText(/Approved/)).toBeVisible({ timeout: 12000 });
  await notification.getByRole("link", { name: title }).click();
  await expect(employee.getByText("Finance approved", { exact: true })).toBeVisible();
  await employee.goto(`${workspace}?view=notifications`);
  await notification.getByRole("button", { name: "Mark as read" }).click();
  await expect.poll(async () => {
    const response = await employee.request.get(`${organizationApi}/workspace`);
    return (await response.json()).notifications.find((item: { purchaseId: string }) => item.purchaseId === purchaseId)?.readAt;
  }).toBeTruthy();
  await employee.goto(workspace);
  await expect(employee.getByRole("link", { name: title })).toBeVisible();
  await expect(employee.getByRole("row").filter({ hasText: title }).getByText("Approved", { exact: true })).toBeVisible();
});

test("an owner can create and switch organizations and sign out", async ({ page }) => {
  await login(page, "finance");
  await page.getByRole("link", { name: "Create organization" }).click();
  const slug = `verification-${Date.now()}`;
  const name = `Verification ${Date.now()}`;
  await page.getByLabel("Organization name").fill(name);
  await page.getByLabel("Organization slug").fill(slug);
  await page.getByRole("button", { name: "Create organization" }).click();
  await expect(page.locator(".org-menu summary")).toHaveText(name);
  await page.locator(".org-menu summary").click();
  await page.getByRole("link", { name: "Juniper Labs", exact: true }).click();
  await expect(page.locator(".org-menu summary")).toHaveText("Juniper Labs");
  await page.getByRole("button", { name: "Sign out" }).click();
  const confirmLogout = page.locator("#kc-logout");
  if (await confirmLogout.isVisible()) await confirmLogout.click();
  await expect.poll(async () => (await page.request.get("/api/me")).status()).toBe(401);
});

test("one competing approval wins and rejection skips finance", async ({ browser }) => {
  const employee = await browser.newPage();
  const manager = await browser.newPage();
  await login(employee, "employee");
  const purchaseId = crypto.randomUUID();
  const input = { id: purchaseId, title: "Supplier test purchase", description: "Evaluate a supplier purchase for the operation.",
    category: "services", currency: "USD", amount: 6400 };
  const url = `${organizationApi}/purchases`;
  expect((await employee.request.post(url, { headers: { origin }, data: input })).status()).toBe(201);
  expect((await employee.request.post(url, { headers: { origin }, data: input })).status()).toBe(200);
  await login(manager, "manager");
  const detailUrl = `${url}/${purchaseId}`;
  await expect.poll(async () => (await (await employee.request.get(detailUrl)).json()).status).toBe("managerReview");
  const decideUrl = `${detailUrl}/approvals/1`;
  const results = await Promise.all(["Supplier quote exceeded scope", "Duplicate quote"].map(comment =>
    manager.request.post(decideUrl, { headers: { origin }, data: { decision: "rejected", comment } })));
  expect(results.map(result => result.status()).sort()).toEqual([202, 409]);
  await expect.poll(async () => (await (await employee.request.get(detailUrl)).json()).status).toBe("rejected");
  const detail = await (await employee.request.get(detailUrl)).json();
  expect(detail.stages).toHaveLength(1);
  expect(detail.timeline.filter((event: { action: string }) => event.action === "manager-rejected")).toHaveLength(1);
});

test("a new organization configures routing and runs an immutable published workflow", async ({ browser, page: owner }) => {
  test.setTimeout(150000);
  const manager = await browser.newPage();
  const employee = await browser.newPage();
  await login(owner, "finance");
  const suffix = Date.now();
  const created = await owner.request.post("/api/organizations", {
    headers: { origin }, data: { name: `Routing test ${suffix}`, slug: `routing-${suffix}` },
  });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  const base = `/api/organizations/${id}`;
  const settings = `/workspace/${id}/settings`;
  await owner.goto(settings);
  await expect(owner.getByRole("heading", { name: "Organization settings" })).toBeVisible();
  for (const width of [1440, 1024, 768, 390]) {
    await owner.setViewportSize({ width, height: 900 });
    expect(await owner.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  expect((await new AxeBuilder({ page: owner }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await owner.getByRole("button", { name: "Switch color theme" }).click();
  await expect(owner.locator("html")).toHaveAttribute("data-theme", "dark");
  expect((await new AxeBuilder({ page: owner }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await owner.getByRole("button", { name: "Language" }).click();
  await expect(owner.getByRole("heading", { name: "Kuruluş ayarları" })).toBeVisible();
  await owner.getByRole("button", { name: "Dil" }).click();
  await owner.getByLabel("Email address").fill("employee@example.test");
  await owner.getByRole("button", { name: "Invite a member", exact: true }).click();
  const invite = owner.getByLabel("Invitation link");
  await expect(invite).toHaveValue(/\/join#[A-Za-z0-9_-]{43}$/);
  const link = await invite.inputValue();
  const code = new URL(link).hash.slice(1);

  await login(manager, "manager");
  const accept = "/api/invitations/accept";
  expect((await manager.request.post(accept, { headers: { origin }, data: { code } })).status()).toBe(400);
  expect((await manager.request.get(`${base}/settings`)).status()).toBe(404);
  const managerInvite = await owner.request.post(`${base}/invitations`, {
    headers: { origin }, data: { email: "manager@example.test", role: "Employee" },
  });
  expect(managerInvite.status()).toBe(201);
  expect((await manager.request.post(accept, { headers: { origin }, data: { code: (await managerInvite.json()).code } })).status()).toBe(200);
  expect((await manager.request.get(`${base}/settings`)).status()).toBe(404);

  await employee.goto(link);
  await employee.getByRole("button", { name: "Accept invitation" }).click();
  await employee.getByRole("link", { name: "Sign in to accept" }).click();
  await employee.locator("#username").fill("employee");
  await employee.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await employee.locator("#kc-login").click();
  await employee.getByRole("button", { name: "Accept invitation" }).click();
  await expect(employee).toHaveURL(new RegExp(`/workspace/${id}$`));
  expect((await employee.request.get(`${base}/settings`)).status()).toBe(404);
  expect((await employee.request.post(accept, { headers: { origin }, data: { code } })).status()).toBe(200);

  await owner.reload();
  const managerRow = owner.locator(".member-details").filter({ hasText: "manager@example.test" });
  await managerRow.locator("summary").click();
  await managerRow.getByLabel("Role").selectOption("Manager");
  await managerRow.getByRole("button", { name: "Save" }).click();
  await expect(managerRow.getByText("Saved", { exact: true })).toBeVisible();
  const groups = owner.getByRole("heading", { name: "Teams and departments" }).locator("..");
  await groups.getByLabel("Name", { exact: true }).fill("Procurement operations");
  await groups.getByLabel("Approval lead").selectOption({ label: "manager@example.test" });
  await groups.getByRole("button", { name: "Add group" }).click();
  await expect(groups.getByText("Procurement operations", { exact: true })).toBeVisible();
  const employeeRow = owner.locator(".member-details").filter({ hasText: "employee@example.test" });
  await employeeRow.locator("summary").click();
  await employeeRow.locator("select[name=team]").selectOption({ label: "Procurement operations" });
  await employeeRow.getByRole("button", { name: "Save" }).click();
  await expect(employeeRow.getByText("Saved", { exact: true })).toBeVisible();
  const routing = owner.getByRole("heading", { name: "Purchase routing" }).locator("..");
  await routing.getByLabel(/Finance approval threshold/).fill("1000");
  await routing.getByRole("button", { name: "Save" }).click();
  await expect(routing.getByText("Saved", { exact: true })).toBeVisible();
  const revoked = await owner.request.post(`${base}/invitations`, {
    headers: { origin }, data: { email: "former-member@example.test", role: "Viewer" },
  });
  expect(revoked.status()).toBe(201);
  const { id: inviteId } = await revoked.json();
  expect((await owner.request.post(`${base}/invitations/${inviteId}/revoke`, { headers: { origin }, data: {} })).status()).toBe(204);
  const organizationSettings = await (await owner.request.get(`${base}/settings`)).json();
  expect(organizationSettings.invitations).toHaveLength(0);
  expect(organizationSettings.financeThreshold).toBe(1000);
  expect((await employee.request.get(`${base}/reports`)).status()).toBe(404);
  expect((await employee.request.get("/api/platform/organizations")).status()).toBe(404);
  expect((await owner.request.get(`${organizationApi}/reports`)).status()).toBe(404);
  await owner.goto(`/workspace/${id}/reports?days=7`);
  await expect(owner.getByRole("heading", { name: "Operations report" })).toBeVisible();
  await expect(owner.getByText("Group created", { exact: true })).toBeVisible();
  for (const width of [1440, 1024, 768, 390]) {
    await owner.setViewportSize({ width, height: 900 });
    expect(await owner.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  expect((await new AxeBuilder({ page: owner }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await owner.getByRole("button", { name: "Switch color theme" }).click();
  await expect(owner.locator("html")).toHaveAttribute("data-theme", "light");
  expect((await new AxeBuilder({ page: owner }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await owner.getByRole("link", { name: "90 days" }).click();
  await expect(owner.getByRole("link", { name: "90 days" })).toHaveAttribute("aria-current", "page");

  await owner.goto(`/workspace/${id}/workflows`);
  for (const width of [1440, 1024, 768, 390]) {
    await owner.setViewportSize({ width, height: 900 });
    expect(await owner.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  await owner.getByRole("link", { name: "New purchase workflow" }).click();
  await owner.getByLabel("Workflow name").fill("Operations equipment");
  await owner.getByRole("spinbutton", { name: "Finance threshold (USD)" }).fill("2000");
  await owner.getByRole("button", { name: "Save draft" }).click();
  await expect(owner.getByRole("heading", { name: "Operations equipment" })).toBeVisible();
  expect((await new AxeBuilder({ page: owner }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  const workflowId = new URL(owner.url()).pathname.split("/").at(-1);
  const workflowApi = `${base}/workflows/${workflowId}`;
  expect((await employee.request.get(`${workflowApi}/published`)).status()).toBe(404);
  expect((await employee.request.get(workflowApi)).status()).toBe(404);
  expect((await owner.request.get(`${organizationApi}/workflows/${workflowId}`)).status()).toBe(404);
  const canvas = owner.getByRole("region", { name: "Purchase workflow canvas" });
  await expect(canvas.getByText("Finance approval", { exact: true })).toBeVisible();
  await owner.getByRole("button", { name: "Manager only", exact: true }).click();
  await expect(canvas.getByText("Finance approval", { exact: true })).toHaveCount(0);
  await owner.getByRole("group", { name: "Steps", exact: true }).getByRole("button", { name: "End", exact: true }).click();
  await owner.getByRole("button", { name: "Save draft" }).click();
  await expect(owner.getByText("Draft saved", { exact: true })).toBeVisible();
  await expect(owner.getByRole("button", { name: "Publish version" })).toBeDisabled();
  const invalidDraft = await (await owner.request.get(workflowApi)).json();
  expect((await owner.request.post(`${workflowApi}/publish`, { headers: { origin }, data: { revision: invalidDraft.revision } })).status()).toBe(400);
  await owner.getByRole("group", { name: "Steps", exact: true }).getByRole("button", { name: "End", exact: true }).click();
  await owner.locator("summary").filter({ hasText: "Connections" }).click();
  await owner.getByLabel("From step").selectOption("manager");
  await owner.getByLabel("To step").selectOption("end");
  await owner.getByRole("button", { name: "Connect steps" }).click();
  await owner.getByRole("button", { name: "Save draft" }).click();
  await expect(owner.getByText("Draft saved", { exact: true })).toBeVisible();
  await owner.reload();
  await expect(owner.getByText("Ready to publish", { exact: true })).toBeVisible();
  const linear = await (await owner.request.get(workflowApi)).json();
  const enriched = {
    schemaVersion: 1,
    nodes: [...linear.graph.nodes, { id: "decision", kind: "decision", x: 420, y: 110 },
      { id: "notify", kind: "notify", x: 630, y: 0 }].map(node => node.id === "end" ? { ...node, x: 840 } : node),
    edges: [{ source: "start", target: "manager" }, { source: "manager", target: "decision" },
      { source: "decision", target: "notify", branch: "approved" }, { source: "decision", target: "end", branch: "rejected" },
      { source: "notify", target: "end" }],
  };
  expect((await owner.request.post(`${workflowApi}/draft`, {
    headers: { origin }, data: { name: linear.name, financeThreshold: linear.financeThreshold, revision: linear.revision, graph: enriched },
  })).status()).toBe(204);
  await owner.reload();
  await expect(owner.getByRole("region", { name: "Purchase workflow canvas" }).getByText("Notify requester", { exact: true })).toBeVisible();
  await expect(owner.getByText("Ready to publish", { exact: true })).toBeVisible();
  for (const width of [1440, 1024, 768, 390]) {
    await owner.setViewportSize({ width, height: 900 });
    expect(await owner.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  expect((await new AxeBuilder({ page: owner }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await owner.getByRole("button", { name: "Publish version" }).click();
  await expect(owner.getByRole("heading", { name: "Version 1 / Operations equipment" })).toBeVisible();

  const purchaseId = crypto.randomUUID();
  const input = { id: purchaseId, title: "New tenant equipment", description: "Equipment for the new operations team.",
    category: "equipment", currency: "USD", amount: 700 };
  const aboveThreshold = await employee.request.post(`${base}/purchases`, {
    headers: { origin }, data: { ...input, amount: 1100 },
  });
  expect(aboveThreshold.status()).toBe(409);
  expect(await aboveThreshold.json()).toEqual({ code: "financeMissing" });
  expect((await employee.request.post(`${base}/purchases`, { headers: { origin }, data: input })).status()).toBe(201);
  const detail = `${base}/purchases/${purchaseId}`;
  await expect.poll(async () => (await (await employee.request.get(detail)).json()).status).toBe("managerReview");
  const before = await (await owner.request.get(detail)).json();
  const current = before.stages[0].assignee;
  const next = before.eligibleMembers.find((member: { role: string }) => member.role === "OrganizationOwner").subject;
  const assign = `${detail}/approvals/1/assign`;
  const assignment = { assignee: next, expectedAssignee: current };
  expect((await manager.request.post(assign, { headers: { origin }, data: assignment })).status()).toBe(404);
  expect((await owner.request.post(assign, { headers: { origin }, data: assignment })).status()).toBe(204);
  expect((await owner.request.post(assign, { headers: { origin }, data: assignment })).status()).toBe(204);
  expect((await manager.request.post(`${detail}/approvals/1`, { headers: { origin }, data: { decision: "approved" } })).status()).toBe(404);
  const after = await (await owner.request.get(detail)).json();
  expect(after.assignmentHistory.map((event: { assignee: string }) => event.assignee)).toEqual([current, next]);
  expect((await owner.request.post(`${detail}/approvals/1`, { headers: { origin }, data: { decision: "approved" } })).status()).toBe(202);
  await expect.poll(async () => (await (await employee.request.get(detail)).json()).status).toBe("approved");

  await employee.goto(`/workspace/${id}/workflows`);
  await employee.getByRole("link", { name: "Submit a request" }).click();
  await employee.getByLabel("Purchase title").fill("Versioned equipment");
  await employee.getByLabel(/Amount/).fill("2500");
  await employee.getByLabel("Category").selectOption("equipment");
  await employee.getByLabel("Business reason").fill("Equipment covered by the published workflow.");
  await employee.getByRole("button", { name: "Submit request" }).click();
  await expect(employee.getByRole("heading", { name: "Versioned equipment" })).toBeVisible();
  await expect(employee.getByText("Operations equipment / Version 1")).toBeVisible();
  const executionId = new URL(employee.url()).pathname.split("/").at(-1);
  const execution = `${base}/purchases/${executionId}`;
  await expect.poll(async () => (await (await employee.request.get(execution)).json()).status).toBe("managerReview");
  await employee.reload();
  await expect(employee.getByRole("list", { name: "Steps" }).getByText("Manager approval: Active")).toBeVisible();
  const rejectedId = crypto.randomUUID();
  const rejectedExecution = `${base}/purchases/${rejectedId}`;
  expect((await employee.request.post(`${base}/purchases`, {
    headers: { origin }, data: { ...input, id: rejectedId, workflowId, workflowVersion: 1 },
  })).status()).toBe(201);
  await expect.poll(async () => (await (await employee.request.get(rejectedExecution)).json()).status).toBe("managerReview");
  expect((await manager.request.post(`${rejectedExecution}/approvals/1`, { headers: { origin }, data: { decision: "rejected" } })).status()).toBe(202);
  await expect.poll(async () => (await (await employee.request.get(rejectedExecution)).json()).status).toBe("rejected");
  await employee.goto(`/workspace/${id}/purchases/${rejectedId}`);
  await expect(employee.getByRole("list", { name: "Steps" }).getByText("Notify requester: Skipped")).toBeVisible();

  await owner.getByRole("button", { name: "Manager and finance", exact: true }).click();
  await owner.getByRole("spinbutton", { name: "Finance threshold (USD)" }).fill("500");
  await expect(owner.getByRole("button", { name: "Publish version" })).toBeDisabled();
  await owner.getByRole("button", { name: "Save draft" }).click();
  await expect(owner.getByText("Draft saved", { exact: true })).toBeVisible();
  expect((await (await employee.request.get(`${workflowApi}/published`)).json()).number).toBe(1);
  await owner.getByRole("button", { name: "Publish version" }).click();
  await expect(owner.getByRole("heading", { name: "Version 2 / Operations equipment" })).toBeVisible();
  expect((await (await owner.request.get(workflowApi)).json()).versions.map((version: { financeThreshold: number }) => version.financeThreshold))
    .toEqual([500, 2000]);
  const stale = await employee.request.post(`${base}/purchases`, {
    headers: { origin }, data: { ...input, id: crypto.randomUUID(), workflowId, workflowVersion: 1 },
  });
  expect(stale.status()).toBe(409);
  expect(await stale.json()).toEqual({ code: "workflowChanged" });

  await owner.getByRole("button", { name: "Unpublish workflow" }).click();
  await owner.getByRole("button", { name: "Confirm unpublish" }).click();
  await expect(owner.locator(".page-header").getByText("Unpublished", { exact: true })).toBeVisible();
  expect((await employee.request.get(`${workflowApi}/published`)).status()).toBe(404);
  const inactive = await employee.request.post(`${base}/purchases`, {
    headers: { origin }, data: { ...input, id: crypto.randomUUID(), workflowId, workflowVersion: 2 },
  });
  expect(inactive.status()).toBe(404);
  expect((await employee.request.post(`${workflowApi}/duplicate`, { headers: { origin }, data: { name: "Forbidden copy" } })).status()).toBe(404);
  await owner.getByRole("button", { name: "Publish version" }).click();
  await expect(owner.getByRole("heading", { name: "Version 3 / Operations equipment" })).toBeVisible();

  await owner.getByRole("button", { name: "Archive workflow" }).click();
  await owner.getByRole("button", { name: "Confirm archive" }).click();
  await expect(owner.getByText("Archived", { exact: true })).toBeVisible();
  expect((await employee.request.get(`${workflowApi}/published`)).status()).toBe(404);
  expect((await manager.request.post(`${execution}/approvals/1`, { headers: { origin }, data: { decision: "approved" } })).status()).toBe(202);
  await expect.poll(async () => (await (await employee.request.get(execution)).json()).status).toBe("approved");
  await owner.reload();
  await expect(owner.getByRole("link", { name: "Versioned equipment" })).toBeVisible();
  await owner.getByRole("link", { name: "Versioned equipment" }).click();
  await expect(owner.getByText("Operations equipment / Version 1")).toBeVisible();
  await expect(owner.getByRole("list", { name: "Steps" }).getByText("Notify requester: Completed")).toBeVisible();
  const requestWorkspace = await (await employee.request.get(`${base}/workspace`)).json();
  expect(requestWorkspace.notifications.filter((item: { purchaseId: string; kind: string }) =>
    item.purchaseId === executionId && item.kind === "workflowNotice")).toHaveLength(1);
  await owner.goto(`/workspace/${id}/workflows/${workflowId}`);
  await owner.getByRole("button", { name: "Duplicate as draft" }).click();
  await owner.getByLabel("Name for the copy").fill("Facilities approval");
  await owner.getByRole("button", { name: "Duplicate as draft" }).click();
  await expect(owner.getByRole("heading", { name: "Facilities approval" })).toBeVisible();
  await expect(owner.getByRole("spinbutton", { name: "Finance threshold (USD)" })).toHaveValue("500");
  const copyId = new URL(owner.url()).pathname.split("/").at(-1);
  const copy = await (await owner.request.get(`${base}/workflows/${copyId}`)).json();
  expect(copy.versions).toEqual([]);
  expect(copy.runs).toEqual([]);
  const members = (await (await owner.request.get(`${base}/settings`)).json()).members;
  const ownerSubject = members.find((member: { role: string }) => member.role === "OrganizationOwner").subject;
  const employeeSubject = members.find((member: { email: string }) => member.email === "employee@example.test").subject;
  const managerSubject = members.find((member: { email: string }) => member.email === "manager@example.test").subject;
  const group = organizationSettings.groups.find((item: { name: string }) => item.name === "Procurement operations");
  expect((await owner.request.post(`${base}/groups/${group.id}`, {
    headers: { origin }, data: { name: group.name, kind: group.kind, lead: ownerSubject },
  })).status()).toBe(204);
  for (const [subject, role, assignedManager] of [[employeeSubject, "Employee", ownerSubject], [managerSubject, "Approver", null]]) {
    expect((await owner.request.post(`${base}/members/${subject}/settings`, {
      headers: { origin }, data: { role, manager: assignedManager },
    })).status()).toBe(204);
  }
  const copyApi = `${base}/workflows/${copyId}`;
  expect((await owner.request.post(`${copyApi}/publish`, { headers: { origin }, data: { revision: copy.revision } })).status()).toBe(204);
  const financeId = crypto.randomUUID();
  const financeExecution = `${base}/purchases/${financeId}`;
  expect((await employee.request.post(`${base}/purchases`, {
    headers: { origin }, data: { ...input, id: financeId, workflowId: copyId, workflowVersion: 1 },
  })).status()).toBe(201);
  await expect.poll(async () => (await (await employee.request.get(financeExecution)).json()).status).toBe("managerReview");
  expect((await owner.request.post(`${financeExecution}/approvals/1`, { headers: { origin }, data: { decision: "approved" } })).status()).toBe(202);
  await expect.poll(async () => (await (await employee.request.get(financeExecution)).json()).status).toBe("financeReview");
  expect((await manager.request.post(`${financeExecution}/approvals/2`, { headers: { origin }, data: { decision: "approved" } })).status()).toBe(202);
  await expect.poll(async () => (await (await employee.request.get(financeExecution)).json()).status).toBe("approved");
  expect((await owner.request.post(`${copyApi}/unpublish`, { headers: { origin }, data: { revision: copy.revision } })).status()).toBe(204);
  await owner.goto(`/workspace/${id}/workflows`);
  await owner.getByLabel("Search by name").fill("FACILITIES");
  await owner.locator("select[name=status]").selectOption("unpublished");
  await owner.getByRole("button", { name: "Apply filters" }).click();
  await expect(owner.getByRole("link", { name: "Facilities approval" })).toBeVisible();
  await expect(owner.getByRole("link", { name: "Operations equipment" })).toHaveCount(0);
  await owner.locator("select[name=status]").selectOption("archived");
  await owner.getByRole("button", { name: "Apply filters" }).click();
  await expect(owner.getByText("No workflows match these filters.")).toBeVisible();
});
