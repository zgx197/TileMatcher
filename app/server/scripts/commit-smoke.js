import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const smokeRoot = path.join(repoRoot, "storage", "smoke-tests", "commit-smoke");
const legacyBatchRoot = path.join(smokeRoot, "mahjong-mvp");
const workbenchRoot = path.join(smokeRoot, "workbench");

async function removePathIfExists(targetPath) {
  await fs.rm(targetPath, { recursive: true, force: true });
}

async function copyDirectory(sourceDir, targetDir) {
  await fs.mkdir(targetDir, { recursive: true });
  const entries = await fs.readdir(sourceDir, { withFileTypes: true });

  for (const entry of entries) {
    const sourcePath = path.join(sourceDir, entry.name);
    const targetPath = path.join(targetDir, entry.name);

    if (entry.isDirectory()) {
      await copyDirectory(sourcePath, targetPath);
      continue;
    }

    if (entry.isFile()) {
      await fs.mkdir(path.dirname(targetPath), { recursive: true });
      await fs.copyFile(sourcePath, targetPath);
    }
  }
}

async function prepareSmokeBatch() {
  await removePathIfExists(smokeRoot);
  await fs.mkdir(legacyBatchRoot, { recursive: true });

  await copyDirectory(path.join(repoRoot, "artifacts", "mahjong-mvp", "analysis"), path.join(legacyBatchRoot, "analysis"));
  await copyDirectory(path.join(repoRoot, "artifacts", "mahjong-mvp", "runtime-levels"), path.join(legacyBatchRoot, "runtime-levels"));
}

try {
  await prepareSmokeBatch();

  process.env.ANALYSIS_LEGACY_BATCH_ROOT = legacyBatchRoot;
  process.env.ANALYSIS_WORKBENCH_DIR = workbenchRoot;

  const [{ queryCandidates }, { saveBatchRuntimeSelection }, { createRuntimeSelectionDraft, getExportDraft, commitExportDraft }, { loadRuntimeCatalog }] = await Promise.all([
    import("../services/candidate-query.js"),
    import("../services/runtime-selection-store.js"),
    import("../services/export-draft-service.js"),
    import("../services/batch-loader.js")
  ]);

  const candidatePage = await queryCandidates("mahjong-mvp", {
    page: 1,
    pageSize: 3,
    sort: "score_desc"
  });

  if (!candidatePage.items.length) {
    throw new Error("Commit smoke test did not find any candidates.");
  }

  const candidateIds = candidatePage.items.map((item) => item.candidateId);
  await saveBatchRuntimeSelection("mahjong-mvp", candidateIds);

  const draft = await createRuntimeSelectionDraft("mahjong-mvp");
  const preview = await getExportDraft(draft.exportId);
  const commit = await commitExportDraft(draft.exportId);
  const runtimeCatalog = await loadRuntimeCatalog("mahjong-mvp");

  if (preview.itemCount !== candidateIds.length) {
    throw new Error(`Draft preview count mismatch: expected ${candidateIds.length}, got ${preview.itemCount}.`);
  }

  if (commit.itemCount !== candidateIds.length) {
    throw new Error(`Commit count mismatch: expected ${candidateIds.length}, got ${commit.itemCount}.`);
  }

  if (runtimeCatalog.length !== candidateIds.length) {
    throw new Error(`Runtime catalog count mismatch: expected ${candidateIds.length}, got ${runtimeCatalog.length}.`);
  }

  if (!runtimeCatalog.length || runtimeCatalog[0].candidateId !== candidateIds[0]) {
    throw new Error("Runtime catalog first candidate does not match the committed draft.");
  }

  await fs.access(commit.manifestPath);
  await fs.access(commit.backupDir);

  console.log(`[analysis:commit-smoke] Draft: ${draft.exportId}`);
  console.log(`[analysis:commit-smoke] Items: ${commit.itemCount}`);
  console.log(`[analysis:commit-smoke] Manifest: ${commit.manifestPath}`);
  console.log(`[analysis:commit-smoke] Backup: ${commit.backupDir}`);
  console.log("[analysis:commit-smoke] Runtime commit pipeline is ready.");

  await removePathIfExists(smokeRoot);
} catch (error) {
  console.error(`[analysis:commit-smoke] Failed: ${error.message}`);
  console.error(`[analysis:commit-smoke] Smoke root: ${smokeRoot}`);
  process.exitCode = 1;
}
