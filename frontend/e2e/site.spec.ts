import { expect, test, type Page } from "@playwright/test";

// E7 public website: pages, navigation, the theme toggle, WhatsApp, the contact form and phone layout.
// The contact test sends one real message to the console inbox, named "E2E <n>".
const API = process.env.E2E_API_URL ?? "http://localhost:5178";
test.setTimeout(240_000);

const pages = ["/", "/about", "/admissions-info", "/parents", "/news", "/events", "/gallery", "/staff", "/contact"];

async function noSideways(page: Page, path: string) {
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth), { message: `${path} scrolls sideways`, timeout: 15_000 }).toBeLessThanOrEqual(0);
}

test("visitors browse the site; Portal leads to sign-in", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("strong, joyful start.");
  await expect(page.getByRole("link", { name: /Apply for admission|How admission works/ }).first()).toBeVisible();

  // Contact details and WhatsApp come from Settings → School.
  const profile = await (await request.get(`${API}/api/public/school/profile`)).json();
  if (profile.phone) await expect(page.getByRole("contentinfo").getByRole("link", { name: profile.phone })).toBeVisible();
  if (profile.whatsAppUrl) await expect(page.getByRole("link", { name: /Chat with the school on WhatsApp/ })).toHaveAttribute("href", profile.whatsAppUrl);

  for (const path of pages.slice(1)) {
    await page.goto(path);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  }
  await page.goto("/parents");
  await page.getByRole("navigation", { name: "On this page" }).getByRole("link", { name: "School bus" }).click();
  await expect(page).toHaveURL(/#transport$/);

  await page.getByRole("banner").getByRole("link", { name: "Portal", exact: true }).click();
  await page.waitForURL(/\/login/);
});

test("the theme toggle works without signing in", async ({ page }) => {
  await page.goto("/login");
  await page.getByRole("button", { name: "Theme: light, dark or system" }).click();
  await page.getByRole("menuitemradio", { name: "Dark" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("button", { name: "Theme: light, dark or system" }).click();
  await page.getByRole("menuitemradio", { name: "System" }).click();
  await expect(page.locator("html")).not.toHaveAttribute("data-theme", /.+/);
});

test("the contact form checks and sends a message", async ({ page }) => {
  await page.goto("/contact");
  const name = page.getByLabel("Your name");
  await expect(async () => {
    await name.fill("x");
    await expect(name).toHaveValue("x", { timeout: 2_000 });
  }).toPass({ timeout: 60_000 });
  await page.getByRole("button", { name: "Send message" }).click();
  await expect(page.getByText("Enter your email so we can reply.")).toBeVisible();
  await expect(page.getByText("Write your message.")).toBeVisible();
  await name.fill(`E2E ${Date.now() % 1_000_000}`);
  await page.getByLabel("Email").fill("e2e+contact@maydel.test");
  await page.getByLabel("Subject").selectOption("General question");
  await page.getByLabel("Message").fill("An automated test message from the website e2e. Please ignore.");
  await page.getByRole("button", { name: "Send message" }).click();
  await expect(page.getByText("Thank you, your message is on its way")).toBeVisible({ timeout: 30_000 });
});

test("signed-in users can still open the website", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email or admission number").fill("parent@maydel.test");
  await page.getByLabel("Password", { exact: true }).fill("Demo#Pass2026");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(/\/portal/);
  await page.goto("/");
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("strong, joyful start.");
});

test("every site page fits a phone", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  for (const path of pages) {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
    await noSideways(page, path);
  }
  await page.goto("/");
  await page.getByRole("button", { name: "Open menu" }).click();
  await expect(page.locator("#site-menu").getByRole("link", { name: "Gallery" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator("#site-menu")).toHaveCount(0);
});
