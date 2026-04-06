import { spawn } from "node:child_process";
import process from "node:process";
import { setTimeout as delay } from "node:timers/promises";

function hasFlag(flag) {
  return process.argv.includes(flag);
}

function readPositiveInt(value, fallback) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : fallback;
}

const port = readPositiveInt(process.env.PORT, 3100);
const browserUrl = `http://127.0.0.1:${port}`;
const healthUrl = `${browserUrl}/api/health`;
const openBrowser = !hasFlag("--no-open") && !process.env.CI;
const watchMode = !hasFlag("--no-watch");

async function isHealthy(url) {
  try {
    const response = await fetch(url, {
      signal: AbortSignal.timeout(1500)
    });
    return response.ok;
  } catch {
    return false;
  }
}

async function waitForHealthy(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await isHealthy(url)) {
      return true;
    }

    await delay(500);
  }

  return false;
}

function openBrowserWindow(url) {
  if (!openBrowser) {
    return;
  }

  if (process.platform === "win32") {
    spawn("cmd", ["/c", "start", "", url], {
      detached: true,
      stdio: "ignore"
    }).unref();
    return;
  }

  if (process.platform === "darwin") {
    spawn("open", [url], {
      detached: true,
      stdio: "ignore"
    }).unref();
    return;
  }

  spawn("xdg-open", [url], {
    detached: true,
    stdio: "ignore"
  }).unref();
}

async function main() {
  if (await isHealthy(healthUrl)) {
    console.log(`[analysis:dev] Reusing running workbench at ${browserUrl}`);
    openBrowserWindow(browserUrl);
    return;
  }

  const serverArgs = [
    ...(watchMode ? ["--watch"] : []),
    "app/server/server.js"
  ];

  const child = spawn(process.execPath, serverArgs, {
    stdio: "inherit",
    env: {
      ...process.env,
      PORT: String(port)
    }
  });

  process.on("SIGINT", () => child.kill("SIGINT"));
  process.on("SIGTERM", () => child.kill("SIGTERM"));

  const healthy = await waitForHealthy(healthUrl, 30000);
  if (healthy) {
    console.log(`[analysis:dev] Server ready at ${browserUrl}`);
    openBrowserWindow(browserUrl);
    return;
  }

  console.log("[analysis:dev] Health check timed out.");
}

await main();
