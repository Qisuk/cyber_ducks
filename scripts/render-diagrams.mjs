// Renders every docs/diagrams/*.mmd to an .svg next to it, using mermaid-cli.
// Uses a browser already installed on the machine instead of puppeteer's bundled Chromium
// (npm blocks puppeteer's download script by default). Override with PUPPETEER_EXECUTABLE_PATH.
import { existsSync, readdirSync, writeFileSync, mkdtempSync, rmSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { spawnSync } from "node:child_process";

const candidates = [
  process.env.PUPPETEER_EXECUTABLE_PATH,
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
  "C:/Program Files/Google/Chrome/Application/chrome.exe",
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
  "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
  "/usr/bin/google-chrome",
  "/usr/bin/chromium",
  "/usr/bin/chromium-browser",
].filter(Boolean);

const browser = candidates.find((p) => existsSync(p));
if (!browser) {
  console.error("No Chrome/Edge/Chromium found. Set PUPPETEER_EXECUTABLE_PATH to a Chromium-based browser.");
  process.exit(1);
}

const tmp = mkdtempSync(join(tmpdir(), "cyber-ducks-diagrams-"));
const puppeteerConfig = join(tmp, "puppeteer.json");
writeFileSync(puppeteerConfig, JSON.stringify({ executablePath: browser, headless: "shell" }));

const dir = "docs/diagrams";
const sources = readdirSync(dir).filter((f) => f.endsWith(".mmd"));
let failed = false;

for (const src of sources) {
  const input = join(dir, src);
  const output = input.replace(/\.mmd$/, ".svg");
  // White background so the SVG stays readable in dark-themed viewers.
  const result = spawnSync(
    "npx",
    ["--no-install", "mmdc", "-i", input, "-o", output, "-b", "white", "-p", puppeteerConfig],
    { stdio: "inherit", shell: true },
  );
  if (result.status !== 0) {
    console.error(`Failed: ${input}`);
    failed = true;
  } else {
    console.log(`Rendered ${output}`);
  }
}

rmSync(tmp, { recursive: true, force: true });
process.exit(failed ? 1 : 0);
