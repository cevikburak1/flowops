import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  timeout: 90000,
  workers: 1,
  use: { baseURL: "http://localhost:3000", trace: "retain-on-failure" },
});
