import fs from "node:fs/promises";
import path from "node:path";

export async function pathExists(targetPath) {
  try {
    await fs.access(targetPath);
    return true;
  } catch {
    return false;
  }
}

export async function ensureDir(dirPath) {
  await fs.mkdir(dirPath, { recursive: true });
}

export async function readJsonFile(filePath, fallbackValue = null) {
  try {
    const raw = await fs.readFile(filePath, "utf8");
    if (!raw.trim()) {
      return fallbackValue;
    }

    return JSON.parse(raw);
  } catch (error) {
    if (error.code === "ENOENT") {
      return fallbackValue;
    }

    throw error;
  }
}

export async function writeJsonFile(filePath, value) {
  await ensureDir(path.dirname(filePath));
  const tempPath = `${filePath}.tmp`;
  await fs.writeFile(tempPath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  await fs.rename(tempPath, filePath);
}

export async function listDirectories(rootDir) {
  if (!await pathExists(rootDir)) {
    return [];
  }

  const entries = await fs.readdir(rootDir, { withFileTypes: true });
  return entries
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort((left, right) => left.localeCompare(right, "zh-CN"));
}
