// Still screenshots for README: dark desktop + mobile light/dark. Usage: node stills.mjs <outDir> <demoJson>
import { chromium } from "playwright";
import { readFileSync } from "node:fs";
import path from "node:path";
const [outDir, demoPath] = process.argv.slice(2);
const demo = JSON.parse(readFileSync(demoPath, "utf8"));
const BASE = "http://localhost:5173";
const browser = await chromium.launch();

async function run(name, { width, height, scheme, mobile }, steps) {
  const ctx = await browser.newContext({ viewport: { width, height }, colorScheme: scheme, isMobile: !!mobile, hasTouch: !!mobile, deviceScaleFactor: mobile ? 2 : 1, locale: "pl-PL", timezoneId: "Europe/Warsaw" });
  const page = await ctx.newPage();
  await page.goto(BASE + "/");
  await page.evaluate((s) => { try { localStorage.setItem("theme", s); } catch {} }, scheme);
  await steps(page, async (label, opts = {}) => {
    await page.waitForTimeout(400);
    const file = path.join(outDir, `${name}-${label}.png`);
    await page.screenshot({ path: file, fullPage: !!opts.full });
    console.log("still", file);
  });
  await ctx.close();
}

async function login(page) {
  await page.evaluate(async (d) => {
    await fetch("/api/auth/login?useCookies=true", { method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include", body: JSON.stringify({ email: d.e, password: d.p }) });
  }, demo);
}

await run("desktop-dark", { width: 1280, height: 800, scheme: "dark" }, async (page, shot) => {
  await page.reload(); await page.getByRole("heading", { level: 1 }).first().waitFor(); await shot("home");
  await login(page); await page.goto(BASE + "/sprawy"); await page.getByRole("heading", { level: 1, name: /Moje sprawy/ }).waitFor(); await shot("cases");
});
await run("desktop-light", { width: 1280, height: 800, scheme: "light" }, async (page, shot) => {
  await page.reload(); await page.getByRole("heading", { level: 1 }).first().waitFor(); await shot("home-full", { full: true });
  await login(page); await page.goto(BASE + "/sprawy"); await page.getByRole("heading", { level: 1, name: /Moje sprawy/ }).waitFor(); await shot("cases");
  await page.goto(BASE + "/konto"); await page.getByRole("heading", { level: 1 }).first().waitFor(); await shot("profile");
});
for (const scheme of ["light", "dark"]) {
  await run(`mobile-${scheme}`, { width: 390, height: 844, scheme, mobile: true }, async (page, shot) => {
    await page.reload(); await page.getByRole("heading", { level: 1 }).first().waitFor(); await shot("home");
    await page.getByRole("button", { name: /Wpisz ręcznie/ }).first().click(); await page.waitForTimeout(500); await shot("manual");
  });
}
await browser.close();
