import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const org = "17213e02-c104-49cf-93b5-a625628e5906";
const workspace = `/workspace/${org}`;
const root = `/api/organizations/${org}`;
const headers = { origin: "http://localhost:3000" };

async function login(page: Page, user: string) {
  await page.goto("/auth/login");
  await page.locator("#username").fill(user);
  await page.locator("#password").fill(process.env.FLOWOPS_DEV_PASSWORD!);
  await page.locator("#kc-login").click();
  await expect(page).toHaveURL(/\/workspace\//, { timeout: 30000 });
}

test("studio form revisions and conditional submissions start the pinned Elsa process", async ({ page, browser }) => {
  await login(page, "finance");
  await page.goto(`${workspace}/forms`);
  await page.getByRole("link", { name: "New form" }).click();
  const name = `Supplier intake ${Date.now()}`;
  await page.getByLabel("Form name").fill(name);
  await page.getByRole("button", { name: "Add field" }).click();
  await page.getByRole("textbox", { name: "Field label" }).fill("Supplier type");
  await page.getByLabel("Field type").selectOption("select");
  await page.getByLabel("Options, one per line").fill("local\nforeign");
  await page.getByRole("button", { name: "Add field" }).click();
  const field = page.locator(".studio-field").last();
  await field.getByRole("textbox", { name: "Field label" }).fill("Foreign reference");
  await field.getByLabel("Required").check();
  await field.getByLabel("Show when field").selectOption({ label: "Supplier type" });
  await field.getByLabel("Matches value").fill("foreign");
  await field.getByLabel("Form step").selectOption("2");
  await page.getByRole("button", { name: "Add field" }).click();
  const attachment = page.locator(".studio-field").last();
  await attachment.getByRole("textbox", { name: "Field label" }).fill("Supplier document");
  await attachment.getByLabel("Field type").selectOption("file");
  await attachment.getByLabel("Form step").selectOption("2");
  await attachment.getByLabel("File limit (KiB)").fill("1");
  await attachment.getByLabel("File extensions, separated by commas").fill("txt");
  const preview = page.getByRole("heading", { name: "Preview" }).locator("../..");
  await expect(preview.getByLabel("Foreign reference")).toHaveCount(0);
  await preview.getByLabel("Supplier type").selectOption("foreign");
  await preview.getByLabel("Form step").selectOption("2");
  await expect(preview.getByLabel("Foreign reference")).toBeVisible();
  await page.getByRole("button", { name: "Narrow preview" }).click();
  await expect(page.locator(".studio-narrow")).toBeVisible();
  await page.getByRole("button", { name: "Save draft" }).click();
  await expect(page.getByRole("heading", { name })).toBeVisible();
  const id = new URL(page.url()).pathname.split("/").at(-1)!;
  const formApi = `${root}/forms/${id}`;
  await page.getByRole("button", { name: "Publish form" }).click();
  await expect(page.getByText("Version 1", { exact: true })).toBeVisible();
  const form = await (await page.request.get(formApi)).json();
  const [supplier, reference] = form.draft.fields;
  const directorySchema = { name: `Directory ${Date.now()}`, fields: [
    { key: "reviewer", label: "Reviewer", type: "user", required: true },
    { key: "department", label: "Department", type: "department", required: false },
  ] };
  const post = (url: string, data: object) => page.request.post(url, { headers, data });
  const directoryForm = await post(`${root}/forms`, directorySchema);
  expect(directoryForm.status()).toBe(201);
  const directoryId = (await directoryForm.json()).id;
  expect((await post(`${root}/forms/${directoryId}/publish`, { revision: 1 })).status()).toBe(204);
  const directoryProcess = await post(`${root}/processes`, {
    name: directorySchema.name, trigger: "manual", fields: [], attachment: "none", form: { id: directoryId, version: 1 },
    steps: [{ kind: "timer", seconds: 1 }],
  });
  expect(directoryProcess.status()).toBe(201);
  const directoryApi = `${root}/processes/${(await directoryProcess.json()).id}`;
  const departmentName = `Directory department ${Date.now()}`;
  expect((await post(`${root}/groups`, { name: departmentName, kind: "department" })).ok()).toBe(true);
  const settings = await (await page.request.get(`${root}/settings`)).json();
  const department = settings.groups.find((group: { name: string }) => group.name === departmentName);
  expect((await post(`${directoryApi}/publish`, { revision: 1 })).status()).toBe(204);
  const directory = await (await page.request.get(directoryApi)).json();
  expect(directory.formChoices.users).toContainEqual(expect.objectContaining({ value: "b4fc7d75-09a6-46fb-8fb2-fcd4dbc74af4" }));
  expect(directory.formChoices.users).not.toContainEqual(expect.objectContaining({ value: "92d58048-838b-4fc3-8ff8-aa8c48b86dac" }));
  expect(directory.formChoices.departments).toContainEqual({ value: department.id, label: departmentName });
  expect((await post(`${directoryApi}/submission-draft`, {
    version: 1, revision: 0, answers: { reviewer: "b4fc7d75-09a6-46fb-8fb2-fcd4dbc74af4", department: crypto.randomUUID() },
  })).status()).toBe(400);
  expect((await post(`${directoryApi}/submission-draft`, {
    version: 1, revision: 0, answers: { reviewer: "b4fc7d75-09a6-46fb-8fb2-fcd4dbc74af4", department: department.id },
  })).status()).toBe(200);
  expect((await post(`${directoryApi}/submission-draft`, {
    version: 1, revision: 0, answers: { reviewer: "92d58048-838b-4fc3-8ff8-aa8c48b86dac" },
  })).status()).toBe(400);
  const spec = { name, trigger: "manual", fields: [], attachment: "none",
    form: { id, version: 1, inputMap: { [reference.key]: "external_reference" } }, steps: [{ kind: "timer", seconds: 1 }] };
  const collision = { ...spec, form: { id, version: 1, inputMap: { [reference.key]: supplier.key } } };
  expect((await post(`${root}/processes`, collision)).status()).toBe(400);
  const process = await post(`${root}/processes`, spec);
  expect(process.status()).toBe(201);
  const processId = (await process.json()).id;
  const api = `${root}/processes/${processId}`;
  expect((await post(`${api}/publish`, { revision: 1 })).status()).toBe(204);

  // Autosave advances the revision and a stale editor cannot overwrite it.
  await page.locator(".studio-field").nth(1).getByRole("textbox", { name: "Field label" }).fill("International reference");
  await expect.poll(async () => (await (await page.request.get(formApi)).json()).revision).toBe(form.revision + 1);
  expect((await post(`${formApi}/draft`, { schema: form.draft, revision: form.revision })).status()).toBe(409);
  await expect(page.getByRole("status")).toHaveText("Draft saved");
  await page.getByRole("button", { name: "Publish form" }).click();
  await expect(page.getByText("Version 2", { exact: true })).toBeVisible();
  for (const width of [1440, 1024, 768, 390]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.querySelector("main")!.scrollWidth)).toBeLessThanOrEqual(width);
  }
  expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Switch color theme" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  expect((await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze()).violations).toEqual([]);

  const rejected = { id: crypto.randomUUID(), version: 1, answers: { [supplier.key]: "foreign" } };
  expect((await post(`${api}/run`, rejected)).status()).toBe(400);
  expect((await post(`${api}/run`, { ...rejected, answers: { [supplier.key]: "local", [reference.key]: "hidden" } })).status()).toBe(400);
  expect((await post(`${api}/run`, { ...rejected, answers: { [supplier.key]: "local" },
    attachment: { name: "supplier.pdf", content: "UkVG" } })).status()).toBe(400);
  await page.goto(`${workspace}/processes/${processId}`);
  await page.getByLabel("Supplier type").selectOption("foreign");
  await page.getByRole("button", { name: "Save answers as draft" }).click();
  await expect(page.getByText("Draft answers saved")).toBeVisible();
  await page.reload();
  await expect(page.getByLabel("Supplier type")).toHaveValue("foreign");
  expect((await post(`${api}/submission-draft`, { version: 1, revision: 0, answers: {} })).status()).toBe(409);
  await page.getByRole("button", { name: "Next step" }).click();
  await page.getByLabel("Foreign reference").fill("EXT-42");
  await page.getByLabel("Supplier document").setInputFiles({ name: "supplier.txt", mimeType: "text/plain", buffer: Buffer.from("REF") });
  await page.getByRole("button", { name: "Start process" }).click();
  await expect(page.getByText("Completed", { exact: true })).toBeVisible({ timeout: 20000 });
  const result = await (await page.request.get(api)).json();
  expect(result.submissionDraft).toBeNull();
  expect(result.runs[0].attachment).toBe("supplier.txt");
  expect(result.form.fields[1].label).toBe("Foreign reference");
  expect(JSON.parse(result.runs[0].answers)).toContainEqual({
    key: reference.key, label: "Foreign reference", value: "EXT-42", inputName: "external_reference",
  });
  const employee = await browser.newPage();
  await login(employee, "employee");
  expect((await employee.request.get(formApi)).status()).toBe(404);
  expect((await employee.request.post(`${formApi}/publish`, { headers, data: { revision: form.revision + 1 } })).status()).toBe(404);
  await employee.close();
});
