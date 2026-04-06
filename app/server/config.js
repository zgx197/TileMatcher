import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, "..", "..");

function resolveRepoPath(value, fallbackRelativePath) {
  const targetPath = value || fallbackRelativePath;
  return path.isAbsolute(targetPath) ? targetPath : path.join(repoRoot, targetPath);
}

export const config = {
  repoRoot,
  get port() {
    return Number(process.env.PORT || 3100);
  },
  get webDir() {
    return path.join(repoRoot, "app", "web");
  },
  get batchRootDir() {
    return resolveRepoPath(process.env.ANALYSIS_BATCH_ROOT, path.join("artifacts", "batches"));
  },
  get legacyBatchRoot() {
    return resolveRepoPath(process.env.ANALYSIS_LEGACY_BATCH_ROOT, path.join("artifacts", "mahjong-mvp"));
  },
  get workbenchDir() {
    return resolveRepoPath(process.env.ANALYSIS_WORKBENCH_DIR, path.join("storage", "workbench"));
  },
  get reviewsFile() {
    return path.join(this.workbenchDir, "reviews.json");
  },
  get presetsFile() {
    return path.join(this.workbenchDir, "presets.json");
  },
  get runtimeSelectionsFile() {
    return path.join(this.workbenchDir, "runtime-selections.json");
  },
  get exportsDir() {
    return path.join(this.workbenchDir, "exports");
  },
  get commitLocksDir() {
    return path.join(this.workbenchDir, "locks", "commits");
  },
  get operationsLogFile() {
    return path.join(this.workbenchDir, "operations.log.jsonl");
  }
};
