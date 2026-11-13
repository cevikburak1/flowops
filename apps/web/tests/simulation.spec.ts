import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const origin = "http://localhost:3000";
const org = "17213e02-c104-49cf-93b5-a625628e5906";
const root = `/api/organizations/${org}`;

async function login(page: Page, user: string) {
  await page.goto("/auth/login");
  await page.locator("#username").fill(user);
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//, { timeout: 30000 });
}

test("simulator persists isolated traces and scenarios for drafts and versions", async ({ page, browser }) => {
  await login(page, "finance");
  const post = (path: string, data: object) => page.request.post(path, { headers: { origin }, data });
  const created = await post(`${root}/workflows`, { name: "Simulation fixture", financeThreshold: 2000, revision: 0 });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  const flow = `${root}/workflows/${id}`;
  const path = `${flow}/simulation`;
  const original = await (await page.request.get(flow)).json();
  const { graph } = original;
  graph.nodes.push({ id: "notice", kind: "notify", x: 720, y: 110 });
  graph.edges = graph.edges.map((edge: { source: string; target: string }) =>
    edge.target === "end" ? { ...edge, target: "notice" } : edge);
  graph.edges.push({ source: "notice", target: "end" });
  expect((await post(`${flow}/draft`, { name: original.name, financeThreshold: 2000, revision: 1, graph })).status()).toBe(204);
  expect((await post(`${flow}/publish`, { revision: 2 })).status()).toBe(204);
  const workspaceBefore = await (await page.request.get(`${root}/workspace`)).json();
  const href = `/workspace/${org}/workflows/${id}`;
  await page.goto(href);
  await page.getByRole("link", { name: "Workflow simulator" }).click();
  await page.getByLabel("Workflow version").selectOption("1");
  await page.getByLabel(/Sample amount/).fill("2001");
  await page.getByLabel("Simulated finance decision").selectOption("rejected");
  await page.getByLabel(/Save as scenario/).fill("Finance decline");
  await page.getByRole("button", { name: "Run simulation" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Rejected" })).toBeVisible();
  await expect(page.getByText("Simulated notification to requester")).toBeVisible();
  await expect(page.locator(".simulation-visited")).not.toHaveCount(0);
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export JSON" }).click();
  expect((await download).suggestedFilename()).toMatch(/^flowops-simulation-.*\.json$/);
  await page.getByLabel("Review recorded steps").selectOption("1");
  await expect(page.locator(".simulation-trace li")).toHaveCount(2);
  const list = await (await page.request.get(path)).json();
  const first = list.runs[0].id;
  expect(list.scenarios[0].name).toBe("Finance decline");
  const invalidGraph = { ...graph, edges: [{ source: "start", target: "manager" }] };
  const published = await (await page.request.get(flow)).json();
  expect((await post(`${flow}/draft`, { name: original.name, financeThreshold: 9000, revision: published.revision, graph: invalidGraph })).status()).toBe(204);
  const current = await (await page.request.get(path)).json();
  const input = { amount: 2001, manager: "approved", finance: "approved" };
  expect((await post(path, { input: { ...input, revision: published.revision } })).status()).toBe(409);
  const invalid = await (await post(path, { input: { ...input, revision: current.revision } })).json();
  expect(invalid.snapshot.result.issues).toContainEqual({ code: "unreachable", nodeId: "finance" });
  const rerun = await (await post(path, { input: { ...input, version: 1 } })).json();
  expect(rerun.snapshot.threshold).toBe(2000);
  expect(rerun.snapshot.result.steps.find((step: { kind: string }) => step.kind === "threshold").branch).toBe("above");
  await page.reload();
  await page.getByLabel("Previous run").selectOption(first);
  await page.getByLabel("Compare with another run").selectOption(rerun.id);
  await expect(page.getByRole("heading", { name: "Comparison" })).toBeVisible();
  await page.getByLabel("Load scenario").selectOption(list.scenarios[0].id);
  await expect(page.getByLabel(/Sample amount/)).toHaveValue("2001");
  const workspaceAfter = await (await page.request.get(`${root}/workspace`)).json();
  for (const key of ["purchases", "approvals", "notifications"]) expect(workspaceAfter[key]).toEqual(workspaceBefore[key]);
  const otherOrg = "14e41a68-86a8-4653-980c-07e1ec2b0562";
  expect((await page.request.get(path.replace(org, otherOrg))).status()).toBe(404);
  const employee = await browser.newPage();
  await login(employee, "employee");
  expect((await employee.request.get(`${path}/runs/${first}`)).status()).toBe(404);
  expect((await employee.request.post(path, { headers: { origin }, data: { input: { ...input, version: 1 } } })).status()).toBe(404);
  await employee.close();
  for (const width of [1440, 1024, 768, 390]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.querySelector("main")!.scrollWidth)).toBeLessThanOrEqual(width);
  }
  expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Switch color theme" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Language" }).click();
  await expect(page.getByRole("heading", { name: "İş akışı simülatörü" })).toBeVisible();
});
