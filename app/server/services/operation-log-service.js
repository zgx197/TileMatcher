import fs from "node:fs/promises";
import { config } from "../config.js";
import { ensureDir, pathExists } from "../lib/fs-utils.js";

function normalizeLimit(limit, fallbackValue = 20) {
  const parsed = Number(limit);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallbackValue;
  }

  return Math.min(Math.trunc(parsed), 200);
}

export async function appendOperationLog(entry) {
  const logEntry = {
    ...entry,
    loggedAt: new Date().toISOString()
  };

  await ensureDir(config.workbenchDir);
  await fs.appendFile(config.operationsLogFile, `${JSON.stringify(logEntry)}\n`, "utf8");
}

export async function readRecentOperationLogs(limit = 20) {
  if (!await pathExists(config.operationsLogFile)) {
    return [];
  }

  const normalizedLimit = normalizeLimit(limit);
  const raw = await fs.readFile(config.operationsLogFile, "utf8");
  const lines = raw
    .split(/\r?\n/u)
    .map((line) => line.trim())
    .filter(Boolean);

  return lines
    .slice(-normalizedLimit)
    .reverse()
    .map((line) => {
      try {
        return JSON.parse(line);
      } catch {
        return {
          eventType: "log_parse_failed",
          rawLine: line
        };
      }
    });
}
