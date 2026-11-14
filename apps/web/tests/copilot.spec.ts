import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("workflow Copilot explains unavailable configuration and preserves manual drafting", async ({ page }) => {
  test.skip(process.env.FLOWOPS_TEST_COPILOT_ENABLED === "1", "This scenario checks the default disabled configuration");
  await page.goto("/auth/login");
  await page.locator("#username").fill("finance");
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//);
  await page.goto("/workspace/17213e02-c104-49cf-93b5-a625628e5906/workflows");
  const trigger = page.getByText("Draft with Copilot", { exact: true });
  await trigger.focus();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("status").filter({ hasText: "Copilot is unavailable" })).toBeVisible();
  await expect(page.getByRole("link", { name: "New purchase workflow" })).toBeVisible();
  for (const width of [1440, 1024, 768, 390]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
  for (const theme of ["dark", "light"]) {
    if (await page.locator("html").getAttribute("data-theme") !== theme) {
      await page.getByRole("button", { name: "Switch color theme" }).click();
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    }
    expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  }
});


test("a reviewed Copilot proposal saves only a draft", async ({ page }) => {
  await page.goto("/auth/login");
  await page.locator("#username").fill("finance");
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//);
  const org = "17213e02-c104-49cf-93b5-a625628e5906";
  const api = "/api/organizations/" + org;
  const title = "Copilot review " + Date.now();
  await page.route("**/copilot/v1/status", route => route.fulfill({
    json: { enabled: true, requests: 0, monthlyLimit: 30 },
  }));
  await page.route("**/copilot/v1/purchase-proposals", route => route.fulfill({ json: {
    draft: {
      name: title, financeThreshold: 0, revision: 0,
      graph: {
        schemaVersion: 1,
        nodes: ["start", "manager", "end"].map((kind, index) => ({ id: kind, kind, x: index * 210, y: 110 })),
        edges: [{ source: "start", target: "manager" }, { source: "manager", target: "end" }],
      },
    },
    simulation: { steps: ["start", "manager", "end"].map(nodeId => ({ nodeId })) },
  } }));
  await page.goto("/workspace/" + org + "/workflows");
  await page.getByText("Draft with Copilot", { exact: true }).click();
  await page.getByLabel("Describe the purchase approval").fill("Manager reviews all equipment purchases.");
  await page.getByRole("button", { name: "Generate proposal" }).click();
  await expect(page.getByRole("heading", { name: "Review the proposed workflow" })).toBeVisible();
  expect((await (await page.request.get(api + "/workflows?q=" + encodeURIComponent(title))).json()).items).toHaveLength(0);
  const created = page.waitForResponse(response => response.url().endsWith(api + "/workflows") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save as workflow draft" }).click();
  const response = await created;
  expect(response.status()).toBe(201);
  const { id } = await response.json();
  await expect(page.getByRole("heading", { name: title })).toBeVisible();
  expect((await page.request.get(api + "/workflows/" + id + "/published")).status()).toBe(404);
  expect((await page.request.post(api + "/workflows/" + id + "/archive", {
    headers: { origin: "http://localhost:3000" }, data: { revision: 1 },
  })).status()).toBe(204);
});

test("forms and reports expose Copilot and keep provider calls disabled", async ({ page }) => {
  await page.goto("/auth/login");
  await page.locator("#username").fill("finance");
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//);
  const org = "17213e02-c104-49cf-93b5-a625628e5906";
  const api = "/api/organizations/" + org + "/copilot/v1";
  for (const [path, heading] of [["forms", "Generate a form with Copilot"], ["reports", "Ask about workflow performance"]]) {
    await page.goto("/workspace/" + org + "/" + path);
    await page.getByText(heading, { exact: true }).click();
    await expect(page.getByRole("status").filter({ hasText: "Copilot is unavailable" })).toBeVisible();
  }
  const headers = { origin: "http://localhost:3000" };
  expect((await page.request.post(api + "/form-proposals", { headers, data: { prompt: "Create a leave request form" } })).status()).toBe(503);
  expect((await page.request.post(api + "/analytics", { headers, data: { prompt: "Count recent completed processes", days: 30 } })).status()).toBe(503);
});

test("Copilot previews a notification and saves a new draft without replacing the publication", async ({ page }) => {
  await page.goto("/auth/login");
  await page.locator("#username").fill("finance");
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//);
  const org = "17213e02-c104-49cf-93b5-a625628e5906";
  const api = "/api/organizations/" + org + "/workflows";
  const headers = { origin: "http://localhost:3000" };
  const created = await page.request.post(api, { headers, data: { name: "Notification review fixture", financeThreshold: 5000, revision: 0 } });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  const path = api + "/" + id;
  expect((await page.request.post(path + "/publish", { headers, data: { revision: 1 } })).status()).toBe(204);
  const before = await (await page.request.get(path)).json();
  const graph = structuredClone(before.graph);
  graph.nodes.push({ id: "copilot-notify", kind: "notify", x: 650, y: 210 });
  graph.edges.find((edge: { branch?: string }) => edge.branch === "below").target = "copilot-notify";
  graph.edges.push({ source: "copilot-notify", target: "end", branch: null });
  await page.route("**/copilot/v1/status", route => route.fulfill({ json: { enabled: true, requests: 1, monthlyLimit: 30 } }));
  await page.route("**/copilot/v1/workflows/" + id + "/edit", route => route.fulfill({ json: {
    before, draft: { name: before.name, financeThreshold: 5000, revision: before.revision, graph },
    notifyOn: { source: "threshold", target: "end", branch: "below" },
  } }));
  await page.goto("/workspace/" + org + "/workflows/" + id);
  await page.getByText("Workflow Copilot", { exact: true }).click();
  await page.getByRole("combobox", { name: "Copilot action" }).selectOption("edit");
  await page.getByLabel("Your request").fill("Notify the requester when the purchase is below the finance threshold.");
  await page.getByRole("button", { name: "Ask Copilot" }).click();
  await expect(page.getByRole("heading", { name: "Proposed workflow" })).toBeVisible();
  await expect(page.getByText(/Add a notification on the connection/)).toBeVisible();
  expect((await (await page.request.get(path)).json()).graph).toEqual(before.graph);
  await page.getByRole("button", { name: "Apply to workflow draft" }).click();
  await expect(page.getByText("Draft updated.", { exact: true })).toBeVisible();
  const after = await (await page.request.get(path)).json();
  expect(after.graph.nodes).toContainEqual(expect.objectContaining({ kind: "notify" }));
  expect(after.publishedVersion).toBe(before.publishedVersion);
  expect((await page.request.post(path + "/archive", { headers, data: { revision: after.revision } })).status()).toBe(204);
});

test("saved Copilot questions survive reload and can be cleared", async ({ page }) => {
  await page.goto("/auth/login");
  await page.locator("#username").fill("finance");
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//);
  const org = "17213e02-c104-49cf-93b5-a625628e5906";
  const api = "/api/organizations/" + org + "/workflows";
  const headers = { origin: "http://localhost:3000" };
  const status = await page.request.get("/api/organizations/" + org + "/copilot/v1/status");
  expect(status.status()).toBe(200);
  test.skip((await status.json()).historyDays !== undefined && (await status.json()).historyDays === 0,
    "Enable local Copilot history to exercise persistence");
  const created = await page.request.post(api, { headers, data: { name: "Saved question fixture", financeThreshold: 5000, revision: 0 } });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  const history = "/api/organizations/" + org + "/copilot/v1/workflows/" + id + "/history";
  const pref = await page.request.get(history);
  expect(pref.status()).toBe(200);
  test.skip((await pref.json()).retentionDays === 0, "Enable local Copilot history to exercise persistence");
  await page.route("**/copilot/v1/status", route => route.fulfill({ json: { enabled: true, requests: 0, monthlyLimit: 30 } }));
  const question = "Which branch requires finance approval?";
  async function openHistory() {
    await page.getByText("Workflow Copilot", { exact: true }).click();
    await page.getByText("Saved questions", { exact: true }).click();
  }
  const answer = "The finance branch runs when the amount exceeds 5,000.";
  await page.route("**/copilot/v1/workflows/" + id + "/explain", route => route.fulfill({ json: { answer } }));
  await page.goto("/workspace/" + org + "/workflows/" + id);
  await page.getByLabel("Copilot", { exact: true }).click();
  await page.getByRole("link", { name: "Ask about this workflow" }).click();
  await expect(page.locator("#workflow-copilot")).toHaveAttribute("open", "");
  await page.getByText("Saved questions", { exact: true }).click();
  await page.getByLabel("Your request").fill(question);
  await page.getByRole("button", { name: "Ask Copilot" }).click();
  await expect(page.getByText(answer, { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Save question and answer" }).click();
  await expect(page.getByRole("button", { name: question })).toBeVisible();
  await page.goto("/workspace/" + org + "/workflows/" + id);
  await openHistory();
  await page.getByRole("button", { name: question }).click();
  await expect(page.getByLabel("Your request")).toHaveValue(question);
  await page.getByText("Read saved answer", { exact: true }).click();
  await expect(page.getByText(answer, { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Clear saved questions" }).click();
  await expect(page.getByText("No saved questions.")).toBeVisible();
  expect((await page.request.post(api + "/" + id + "/archive", { headers, data: { revision: 1 } })).status()).toBe(204);
});
