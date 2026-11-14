// Records demo frames of Refund Pilot with headless Chromium. Fictional ticket only.
// Usage: node record-demo.mjs <outDir> <demoJsonPath>
import { chromium } from "playwright";
import { mkdirSync, readFileSync } from "node:fs";
import path from "node:path";

const [outDir, demoPath] = process.argv.slice(2);
mkdirSync(outDir, { recursive: true });
const demo = JSON.parse(readFileSync(demoPath, "utf8"));
const BASE = "http://localhost:5173";

function fictionalTicketPdf() {
  const lines = ["PKP INTERCITY - BILET NA PRZEJAZD", "Pociag: IC 3510 MALOPOLSKA  Kategoria: IC",
    "Od: Krakow Glowny   Do: Warszawa Centralna", "Data przejazdu: 2026-11-10  Odjazd 08:12  Przyjazd 10:50",
    "Nr biletu: IC-DEMO-0001-2026", "Klasa 2, miejsce 45, wagon 6", "Cena: 89,00 zl (PTU 8%)"];
  const text = lines.map((l, i) => `BT /F1 13 Tf 50 ${780 - i * 24} Td (${l}) Tj ET`).join("\n");
  const objs = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    `<< /Length ${text.length} >>\nstream\n${text}\nendstream`, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"];
  let pdf = "%PDF-1.4\n"; const off = [];
  objs.forEach((o, i) => { off.push(pdf.length); pdf += `${i + 1} 0 obj\n${o}\nendobj\n`; });
  const x = pdf.length;
  pdf += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n` + off.map((o) => `${String(o).padStart(10, "0")} 00000 n \n`).join("");
  pdf += `trailer\n<< /Size ${objs.length + 1} /Root 1 0 R >>\nstartxref\n${x}\n%%EOF\n`;
  return Buffer.from(pdf, "latin1");
}

const scheme = process.env.SCHEME || "light";
const width = Number(process.env.W || 1100), height = Number(process.env.H || 720);
const prefix = process.env.PREFIX || "f";
const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width, height }, colorScheme: scheme, locale: "pl-PL", timezoneId: "Europe/Warsaw" });
const page = await ctx.newPage();
let n = 0;
async function shot(name) {
  await page.waitForTimeout(400);
  const file = path.join(outDir, `${prefix}${String(++n).padStart(2, "0")}-${name}.jpg`);
  await page.screenshot({ path: file, type: "jpeg", quality: 85 });
  console.log("frame", file);
}

await page.goto(BASE + "/");
await page.evaluate((s) => { try { localStorage.setItem("theme", s); } catch {} }, scheme);
await page.reload();
await page.getByRole("heading", { level: 1 }).first().waitFor();
await shot("home");

await page.getByRole("button", { name: /Wgraj bilet/ }).first().click();
await page.locator('input[type=file]').first().setInputFiles({ name: "bilet-demo.pdf", mimeType: "application/pdf", buffer: fictionalTicketPdf() });
await shot("ticket-picked");
await page.getByRole("button", { name: /Odczytaj dane/ }).click();
await page.getByText(/Sprawdź dane z biletu/).first().waitFor({ timeout: 60000 });
await shot("ticket-read");

await page.getByRole("button", { name: /^Dalej/ }).click();
await page.getByRole("button", { name: /^120 min/ }).click();
await page.getByText(/Należy się/).first().waitFor({ timeout: 15000 });
await shot("delay");

await page.getByRole("button", { name: /Zobacz wynik/ }).click();
await page.getByText(/44,50/).first().waitFor({ timeout: 15000 });
await shot("result");

await page.getByRole("button", { name: /Zapisz sprawę/ }).click();
await page.getByLabel(/e-mail/i).first().fill(demo.e);
await page.getByLabel(/^Hasło/i).first().fill(demo.p);
await shot("inline-login");
await page.getByRole("button", { name: /^Zaloguj się$/ }).last().click();
await page.getByText(/Wybierz format|Tekst/).first().waitFor({ timeout: 60000 });
await shot("letter-choice");

const textBtn = page.getByRole("button", { name: /Tekst/ }).first();
if (await textBtn.count()) { await textBtn.click(); }
else { await page.getByRole("radio", { name: /Tekst/ }).first().check(); }
await page.waitForTimeout(800);
await shot("letter-text");

await page.goto(BASE + "/sprawy");
await page.getByRole("heading", { level: 1, name: /Moje sprawy/ }).waitFor();
await shot("cases");

await browser.close();
