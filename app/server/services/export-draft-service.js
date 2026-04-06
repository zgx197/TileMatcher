import fs from "node:fs/promises";
import path from "node:path";
import { config } from "../config.js";
import { ensureDir, pathExists, readJsonFile, writeJsonFile } from "../lib/fs-utils.js";
import { buildCandidateSummary, loadBatchCandidates, loadBatchOrThrow } from "./batch-loader.js";
import { appendOperationLog } from "./operation-log-service.js";
import { getBatchReviews } from "./review-store.js";
import { getBatchRuntimeSelection } from "./runtime-selection-store.js";

const COMMIT_MANIFEST_VERSION = 1;
const EXPORT_SOURCE = "workbench_local";

const EXPORT_STATUSES = {
  DRAFT: "draft",
  COMMITTING: "committing",
  COMMITTED: "committed",
  COMMIT_FAILED: "commit_failed",
  SUPERSEDED: "superseded"
};

function buildExportId(batchId) {
  const compactTimestamp = new Date().toISOString().replaceAll(":", "").replaceAll("-", "").replaceAll(".", "");
  return `runtime_selection_${batchId}_${compactTimestamp}`;
}

function buildCommitPaths(batch, exportId) {
  const commitRootDir = path.join(config.exportsDir, "commits", exportId);
  const stageRootDir = path.join(commitRootDir, "stage");
  const stagedRuntimeDir = path.join(stageRootDir, "runtime-levels");
  const stagedLevelsDir = path.join(stagedRuntimeDir, "levels");
  const backupDir = path.join(commitRootDir, "backup");
  const manifestPath = path.join(commitRootDir, "commit-manifest.json");
  const rollbackRuntimeDir = path.join(path.dirname(batch.runtimeDir), `runtime-levels.__rollback__${exportId}`);

  return {
    commitRootDir,
    stageRootDir,
    stagedRuntimeDir,
    stagedLevelsDir,
    backupDir,
    manifestPath,
    rollbackRuntimeDir
  };
}

function createStatusError(message, statusCode, details = null) {
  const error = new Error(message);
  error.statusCode = statusCode;
  error.details = details;
  return error;
}

function isWithinPath(targetPath, rootPath) {
  const resolvedTarget = path.resolve(targetPath);
  const resolvedRoot = path.resolve(rootPath);
  return resolvedTarget === resolvedRoot || resolvedTarget.startsWith(`${resolvedRoot}${path.sep}`);
}

function formatLevelFileName(levelNumber) {
  return `level_${String(levelNumber).padStart(3, "0")}.json`;
}

function normalizeRuntimeTile(tile) {
  return {
    Id: Number(tile.id) || 0,
    Type: String(tile.type || ""),
    GX: Number(tile.gx) || 0,
    GY: Number(tile.gy) || 0,
    GZ: Number(tile.gz) || 0,
    Shape: {
      WidthUnits: Number(tile.shape?.widthUnits) || 4,
      HeightUnits: Number(tile.shape?.heightUnits) || 6
    },
    Removed: Boolean(tile.removed),
    face_hidden_initial: Boolean(tile.faceHiddenInitial)
  };
}

function normalizeRuntimeLayout(layout) {
  return {
    CandidateId: String(layout?.candidateId || ""),
    LevelId: Number(layout?.levelId) || 0,
    Tiles: (layout?.tiles || []).map(normalizeRuntimeTile)
  };
}

function buildRuntimeCatalogItem(item) {
  return {
    LevelNumber: Number(item.levelNumber) || 0,
    CandidateId: String(item.candidateId || ""),
    DifficultyBucket: String(item.difficultyBucket || ""),
    RecommendationScore: Number(item.recommendationScore) || 0,
    FileName: formatLevelFileName(item.levelNumber)
  };
}

function buildRuntimeLevelPayload(item) {
  return {
    LevelNumber: Number(item.levelNumber) || 0,
    CandidateId: String(item.candidateId || ""),
    DifficultyBucket: String(item.difficultyBucket || ""),
    RecommendationScore: Number(item.recommendationScore) || 0,
    Layout: normalizeRuntimeLayout(item.layout)
  };
}

function buildCommitManifest(base = {}, patch = {}) {
  return {
    manifestVersion: COMMIT_MANIFEST_VERSION,
    ...base,
    ...patch,
    updatedAt: new Date().toISOString()
  };
}

function buildDraftStatus(payload) {
  if (payload?.status) {
    return String(payload.status);
  }

  if (payload?.supersededBy) {
    return EXPORT_STATUSES.SUPERSEDED;
  }

  if (payload?.committedAt || payload?.commit?.committedAt) {
    return EXPORT_STATUSES.COMMITTED;
  }

  return EXPORT_STATUSES.DRAFT;
}

function normalizeDraftPayload(payload) {
  const generatedAt = String(payload?.generatedAt || payload?.createdAt || "");
  const createdAt = String(payload?.createdAt || generatedAt);
  const committedAt = String(payload?.committedAt || payload?.commit?.committedAt || "");
  const committedRuntimeDir = String(payload?.committedRuntimeDir || payload?.commit?.runtimeDir || "");
  const itemCount = Number(payload?.itemCount) || (Array.isArray(payload?.items) ? payload.items.length : 0);

  return {
    ...payload,
    generatedAt,
    createdAt,
    itemCount,
    source: String(payload?.source || EXPORT_SOURCE),
    status: buildDraftStatus(payload),
    committedAt,
    committedRuntimeDir,
    supersededBy: String(payload?.supersededBy || "")
  };
}

function buildExportSummary(payload, filePath) {
  const normalizedPayload = normalizeDraftPayload(payload);

  return {
    exportId: String(normalizedPayload.exportId || ""),
    batchId: String(normalizedPayload.batchId || ""),
    exportType: String(normalizedPayload.exportType || ""),
    status: normalizedPayload.status,
    source: normalizedPayload.source,
    generatedAt: normalizedPayload.generatedAt,
    createdAt: normalizedPayload.createdAt,
    selectionUpdatedAt: String(normalizedPayload.selectionUpdatedAt || ""),
    itemCount: normalizedPayload.itemCount,
    committedAt: normalizedPayload.committedAt,
    committedRuntimeDir: normalizedPayload.committedRuntimeDir,
    supersededBy: normalizedPayload.supersededBy,
    filePath
  };
}

function compareExportsByGeneratedAt(left, right) {
  const leftTime = Date.parse(left.generatedAt || left.createdAt || "") || 0;
  const rightTime = Date.parse(right.generatedAt || right.createdAt || "") || 0;

  if (leftTime !== rightTime) {
    return rightTime - leftTime;
  }

  return String(right.exportId || "").localeCompare(String(left.exportId || ""), "en");
}

async function listExportDraftFiles() {
  if (!await pathExists(config.exportsDir)) {
    return [];
  }

  const entries = await fs.readdir(config.exportsDir, { withFileTypes: true });
  return entries
    .filter((entry) => entry.isFile() && entry.name.endsWith(".json"))
    .map((entry) => path.join(config.exportsDir, entry.name));
}

async function markBatchCommittedExportsSuperseded(batchId, currentExportId) {
  const exportFiles = await listExportDraftFiles();

  for (const filePath of exportFiles) {
    const payload = await readJsonFile(filePath, null);
    if (!payload || String(payload.batchId || "") !== batchId || String(payload.exportId || "") === currentExportId) {
      continue;
    }

    const normalizedPayload = normalizeDraftPayload(payload);
    if (normalizedPayload.status !== EXPORT_STATUSES.COMMITTED) {
      continue;
    }

    await writeJsonFile(filePath, {
      ...normalizedPayload,
      status: EXPORT_STATUSES.SUPERSEDED,
      supersededBy: currentExportId,
      supersededAt: new Date().toISOString()
    });
  }
}

function validateDraftItems(draft, candidates) {
  const errors = [];
  const candidateIds = new Set(candidates.map((candidate) => candidate.candidateId));
  const seenCandidateIds = new Set();
  const seenLevelNumbers = new Set();

  for (const [index, item] of (draft.items || []).entries()) {
    const expectedLevelNumber = index + 1;

    if (item.levelNumber !== expectedLevelNumber) {
      errors.push({
        code: "invalid_level_number",
        candidateId: item.candidateId,
        expectedLevelNumber,
        actualLevelNumber: item.levelNumber
      });
    }

    if (seenLevelNumbers.has(item.levelNumber)) {
      errors.push({
        code: "duplicate_level_number",
        candidateId: item.candidateId,
        levelNumber: item.levelNumber
      });
    }
    seenLevelNumbers.add(item.levelNumber);

    if (seenCandidateIds.has(item.candidateId)) {
      errors.push({
        code: "duplicate_candidate_id",
        candidateId: item.candidateId
      });
    }
    seenCandidateIds.add(item.candidateId);

    if (!candidateIds.has(item.candidateId)) {
      errors.push({
        code: "candidate_not_found",
        candidateId: item.candidateId
      });
    }

    if (!item.layout || !Array.isArray(item.layout.tiles) || !item.layout.tiles.length) {
      errors.push({
        code: "missing_layout_tiles",
        candidateId: item.candidateId
      });
    }

    if (item.layout?.candidateId && item.layout.candidateId !== item.candidateId) {
      errors.push({
        code: "layout_candidate_mismatch",
        candidateId: item.candidateId,
        layoutCandidateId: item.layout.candidateId
      });
    }
  }

  if (errors.length) {
    throw createStatusError("Export draft validation failed.", 400, errors);
  }

  return {
    itemCount: draft.items.length,
    candidateCount: seenCandidateIds.size
  };
}

function assertSafeRuntimePaths(batch) {
  const unsafePaths = [];
  const requiredPaths = [
    batch.rootDir,
    batch.runtimeDir,
    batch.runtimeCatalogPath
  ];

  for (const targetPath of requiredPaths) {
    if (!isWithinPath(targetPath, config.repoRoot)) {
      unsafePaths.push(targetPath);
    }
  }

  if (!isWithinPath(batch.runtimeCatalogPath, batch.runtimeDir)) {
    unsafePaths.push(batch.runtimeCatalogPath);
  }

  if (unsafePaths.length) {
    throw createStatusError("Runtime commit target is outside the allowed workspace.", 500, unsafePaths);
  }
}

async function removePathIfExists(targetPath) {
  if (!await pathExists(targetPath)) {
    return;
  }

  await fs.rm(targetPath, { recursive: true, force: true });
}

async function copyDirectoryFiles(sourceDir, targetDir) {
  if (!await pathExists(sourceDir)) {
    return;
  }

  await ensureDir(targetDir);
  const entries = await fs.readdir(sourceDir, { withFileTypes: true });
  for (const entry of entries) {
    const sourcePath = path.join(sourceDir, entry.name);
    const targetPath = path.join(targetDir, entry.name);

    if (entry.isDirectory()) {
      await copyDirectoryFiles(sourcePath, targetPath);
      continue;
    }

    if (entry.isFile()) {
      await ensureDir(path.dirname(targetPath));
      await fs.copyFile(sourcePath, targetPath);
    }
  }
}

async function backupExistingRuntimeDir(batch, paths) {
  const sourceLevelsDir = path.join(batch.runtimeDir, "levels");
  const backupLevelsDir = path.join(paths.backupDir, "levels");

  await removePathIfExists(paths.backupDir);
  await ensureDir(paths.backupDir);

  if (await pathExists(batch.runtimeCatalogPath)) {
    await fs.copyFile(batch.runtimeCatalogPath, path.join(paths.backupDir, "level-catalog.json"));
  }

  await copyDirectoryFiles(sourceLevelsDir, backupLevelsDir);
}

async function writeStagedRuntimeDir(paths, draftItems) {
  await removePathIfExists(paths.stageRootDir);
  await ensureDir(paths.stagedLevelsDir);

  const catalogItems = draftItems.map(buildRuntimeCatalogItem);
  await writeJsonFile(path.join(paths.stagedRuntimeDir, "level-catalog.json"), catalogItems);

  for (const item of draftItems) {
    const levelFilePath = path.join(paths.stagedLevelsDir, formatLevelFileName(item.levelNumber));
    await writeJsonFile(levelFilePath, buildRuntimeLevelPayload(item));
  }
}

async function validateStagedRuntimeDir(paths, draftItems) {
  const stagedCatalogPath = path.join(paths.stagedRuntimeDir, "level-catalog.json");
  const stagedCatalog = await readJsonFile(stagedCatalogPath, null);
  if (!Array.isArray(stagedCatalog) || stagedCatalog.length !== draftItems.length) {
    throw createStatusError("Staged runtime catalog validation failed.", 500, {
      stagedCatalogPath,
      expectedCount: draftItems.length,
      actualCount: Array.isArray(stagedCatalog) ? stagedCatalog.length : null
    });
  }

  for (const item of draftItems) {
    const stagedLevelPath = path.join(paths.stagedLevelsDir, formatLevelFileName(item.levelNumber));
    const stagedLevel = await readJsonFile(stagedLevelPath, null);
    if (!stagedLevel) {
      throw createStatusError("Staged runtime level file is missing.", 500, {
        candidateId: item.candidateId,
        levelFilePath: stagedLevelPath
      });
    }

    if (Number(stagedLevel.LevelNumber) !== item.levelNumber || String(stagedLevel.CandidateId || "") !== item.candidateId) {
      throw createStatusError("Staged runtime level file validation failed.", 500, {
        candidateId: item.candidateId,
        expectedLevelNumber: item.levelNumber,
        actualLevelNumber: stagedLevel.LevelNumber,
        actualCandidateId: stagedLevel.CandidateId,
        levelFilePath: stagedLevelPath
      });
    }
  }

  return {
    stagedCatalogPath,
    stagedLevelCount: draftItems.length
  };
}

async function acquireCommitLock(batchId, exportId) {
  await ensureDir(config.commitLocksDir);
  const lockPath = path.join(config.commitLocksDir, `${batchId}.lock.json`);
  const payload = {
    batchId,
    exportId,
    createdAt: new Date().toISOString(),
    pid: process.pid
  };

  try {
    const handle = await fs.open(lockPath, "wx");
    try {
      await handle.writeFile(`${JSON.stringify(payload, null, 2)}\n`, "utf8");
    } finally {
      await handle.close();
    }
  } catch (error) {
    if (error.code === "EEXIST") {
      const existingLock = await readJsonFile(lockPath, null);
      throw createStatusError("Another runtime export commit is already in progress for this batch.", 409, {
        lockPath,
        activeLock: existingLock
      });
    }

    throw error;
  }

  return {
    ...payload,
    lockPath
  };
}

async function releaseCommitLock(lock) {
  if (!lock?.lockPath) {
    return;
  }

  try {
    await fs.unlink(lock.lockPath);
  } catch (error) {
    if (error.code !== "ENOENT") {
      throw error;
    }
  }
}

async function swapRuntimeDir(batch, paths) {
  await removePathIfExists(paths.rollbackRuntimeDir);
  await ensureDir(path.dirname(batch.runtimeDir));

  const liveRuntimeExists = await pathExists(batch.runtimeDir);
  if (liveRuntimeExists) {
    await fs.rename(batch.runtimeDir, paths.rollbackRuntimeDir);
  }

  try {
    await fs.rename(paths.stagedRuntimeDir, batch.runtimeDir);
  } catch (error) {
    if (await pathExists(batch.runtimeDir)) {
      await removePathIfExists(batch.runtimeDir);
    }

    if (await pathExists(paths.rollbackRuntimeDir)) {
      await fs.rename(paths.rollbackRuntimeDir, batch.runtimeDir);
    }

    throw error;
  }
}

async function rollbackRuntimeDir(batch, paths) {
  const rollbackExists = await pathExists(paths.rollbackRuntimeDir);
  if (!rollbackExists) {
    return false;
  }

  if (await pathExists(batch.runtimeDir)) {
    await removePathIfExists(batch.runtimeDir);
  }

  await fs.rename(paths.rollbackRuntimeDir, batch.runtimeDir);
  return true;
}

async function finalizeRollbackDir(paths) {
  await removePathIfExists(paths.rollbackRuntimeDir);
}

async function writeCommitManifest(manifestPath, baseManifest, patch = {}) {
  const nextManifest = buildCommitManifest(baseManifest, patch);
  await writeJsonFile(manifestPath, nextManifest);
  return nextManifest;
}

export async function createRuntimeSelectionDraft(batchId) {
  const selection = await getBatchRuntimeSelection(batchId);
  if (!selection.candidateIds.length) {
    throw createStatusError("Runtime selection is empty.", 400);
  }

  const candidates = await loadBatchCandidates(batchId);
  const reviewsByCandidateId = await getBatchReviews(batchId);
  const candidateById = new Map(candidates.map((candidate) => [candidate.candidateId, candidate]));
  const selectedCandidates = selection.candidateIds
    .map((candidateId) => candidateById.get(candidateId) || null)
    .filter(Boolean);

  if (!selectedCandidates.length) {
    throw createStatusError("Runtime selection does not contain valid candidates.", 400);
  }

  const exportId = buildExportId(batchId);
  const generatedAt = new Date().toISOString();
  const items = selectedCandidates.map((candidate, index) => ({
    levelNumber: index + 1,
    ...buildCandidateSummary(candidate, reviewsByCandidateId[candidate.candidateId] || null),
    layout: candidate.layout
  }));

  const payload = {
    exportId,
    exportType: "runtime-selection-draft",
    batchId,
    status: EXPORT_STATUSES.DRAFT,
    source: EXPORT_SOURCE,
    createdAt: generatedAt,
    generatedAt,
    selectionUpdatedAt: selection.updatedAt,
    committedAt: "",
    committedRuntimeDir: "",
    supersededBy: "",
    itemCount: items.length,
    items
  };

  await ensureDir(config.exportsDir);
  const filePath = path.join(config.exportsDir, `${exportId}.json`);
  await writeJsonFile(filePath, payload);
  await appendOperationLog({
    eventType: "runtime_selection_draft_created",
    batchId,
    exportId,
    itemCount: items.length
  });

  return {
    exportId,
    exportType: payload.exportType,
    batchId,
    status: payload.status,
    source: payload.source,
    createdAt: payload.createdAt,
    generatedAt,
    selectionUpdatedAt: selection.updatedAt,
    itemCount: items.length,
    committedAt: payload.committedAt,
    committedRuntimeDir: payload.committedRuntimeDir,
    supersededBy: payload.supersededBy,
    filePath,
    items: items.map((item) => ({
      levelNumber: item.levelNumber,
      candidateId: item.candidateId,
      difficultyBucket: item.difficultyBucket,
      recommendationScore: item.recommendationScore,
      runtimeLevelNumber: item.runtimeLevelNumber,
      review: item.review
    }))
  };
}

export async function listBatchExports(batchId) {
  const exportFiles = await listExportDraftFiles();
  const items = [];

  for (const filePath of exportFiles) {
    const payload = await readJsonFile(filePath, null);
    if (!payload || String(payload.batchId || "") !== batchId) {
      continue;
    }

    items.push(buildExportSummary(payload, filePath));
  }

  items.sort(compareExportsByGeneratedAt);
  return {
    batchId,
    items
  };
}

export async function getExportDraft(exportId) {
  const filePath = path.join(config.exportsDir, `${exportId}.json`);
  if (!await pathExists(filePath)) {
    throw createStatusError(`Unknown export: ${exportId}`, 404);
  }

  const payload = await readJsonFile(filePath, null);
  if (!payload) {
    throw createStatusError(`Failed to read export: ${exportId}`, 500);
  }

  return {
    ...normalizeDraftPayload(payload),
    filePath
  };
}

export async function commitExportDraft(exportId) {
  const draft = await getExportDraft(exportId);
  if (draft.exportType !== "runtime-selection-draft") {
    throw createStatusError(`Unsupported export type: ${draft.exportType}`, 400);
  }

  if (!Array.isArray(draft.items) || !draft.items.length) {
    throw createStatusError("Export draft does not contain any items.", 400);
  }

  const batch = await loadBatchOrThrow(draft.batchId);
  assertSafeRuntimePaths(batch);

  if (draft.status === EXPORT_STATUSES.COMMITTED && draft.commit && !draft.supersededBy) {
    return draft.commit;
  }

  const lock = await acquireCommitLock(draft.batchId, exportId);
  const paths = buildCommitPaths(batch, exportId);
  const draftFilePath = path.join(config.exportsDir, `${exportId}.json`);
  const baseManifest = {
    exportId,
    batchId: draft.batchId,
    draftFilePath,
    runtimeDir: batch.runtimeDir,
    runtimeCatalogPath: batch.runtimeCatalogPath,
    backupDir: paths.backupDir,
    lock,
    startedAt: new Date().toISOString(),
    status: "preparing"
  };

  await ensureDir(paths.commitRootDir);
  let manifest = await writeCommitManifest(paths.manifestPath, baseManifest);
  await writeJsonFile(draftFilePath, {
    ...draft,
    status: EXPORT_STATUSES.COMMITTING
  });

  try {
    const candidates = await loadBatchCandidates(draft.batchId);
    const draftValidation = validateDraftItems(draft, candidates);
    manifest = await writeCommitManifest(paths.manifestPath, manifest, {
      status: "validated",
      draftValidation
    });

    await writeStagedRuntimeDir(paths, draft.items);
    const stageValidation = await validateStagedRuntimeDir(paths, draft.items);
    manifest = await writeCommitManifest(paths.manifestPath, manifest, {
      status: "staged",
      stageValidation,
      stagedRuntimeDir: paths.stagedRuntimeDir
    });

    await backupExistingRuntimeDir(batch, paths);
    manifest = await writeCommitManifest(paths.manifestPath, manifest, {
      status: "backup_ready"
    });

    await swapRuntimeDir(batch, paths);
    await finalizeRollbackDir(paths);

    const committedAt = new Date().toISOString();
    const commit = {
      exportId,
      batchId: draft.batchId,
      status: "committed",
      committedAt,
      runtimeDir: batch.runtimeDir,
      levelCatalogPath: batch.runtimeCatalogPath,
      levelsDir: path.join(batch.runtimeDir, "levels"),
      backupDir: paths.backupDir,
      manifestPath: paths.manifestPath,
      itemCount: draft.items.length
    };

    manifest = await writeCommitManifest(paths.manifestPath, manifest, {
      status: "committed",
      committedAt,
      commit
    });

    await writeJsonFile(draftFilePath, {
      ...draft,
      status: EXPORT_STATUSES.COMMITTED,
      committedAt,
      committedRuntimeDir: batch.runtimeDir,
      supersededBy: "",
      commit
    });

    try {
      await markBatchCommittedExportsSuperseded(draft.batchId, exportId);
      await appendOperationLog({
        eventType: "runtime_selection_draft_committed",
        batchId: draft.batchId,
        exportId,
        itemCount: draft.items.length,
        committedAt,
        committedRuntimeDir: batch.runtimeDir,
        manifestPath: paths.manifestPath
      });
    } catch (metadataError) {
      await writeCommitManifest(paths.manifestPath, manifest, {
        historyWarning: {
          message: metadataError.message
        }
      });
    }

    return commit;
  } catch (error) {
    let rollbackSucceeded = false;
    try {
      rollbackSucceeded = await rollbackRuntimeDir(batch, paths);
    } catch (rollbackError) {
      error.details = {
        ...(error.details || {}),
        rollbackError: {
          message: rollbackError.message
        }
      };
    }

    await writeCommitManifest(paths.manifestPath, manifest, {
      status: rollbackSucceeded ? "failed_rolled_back" : "failed_needs_recovery",
      failedAt: new Date().toISOString(),
      rollbackSucceeded,
      error: {
        message: error.message,
        details: error.details || null
      }
    });

    try {
      await writeJsonFile(draftFilePath, {
        ...draft,
        status: EXPORT_STATUSES.COMMIT_FAILED,
        lastError: {
          message: error.message,
          failedAt: new Date().toISOString()
        }
      });
      await appendOperationLog({
        eventType: "runtime_selection_draft_commit_failed",
        batchId: draft.batchId,
        exportId,
        message: error.message,
        rollbackSucceeded
      });
    } catch {
      // Preserve the original failure; metadata persistence is best effort here.
    }

    if (!error.statusCode) {
      error.statusCode = 500;
    }

    error.details = {
      ...(error.details || {}),
      manifestPath: paths.manifestPath,
      rollbackSucceeded
    };
    throw error;
  } finally {
    await releaseCommitLock(lock);
  }
}
