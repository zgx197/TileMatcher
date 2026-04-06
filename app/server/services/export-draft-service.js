import fs from "node:fs/promises";
import path from "node:path";
import { config } from "../config.js";
import { ensureDir, pathExists, readJsonFile, writeJsonFile } from "../lib/fs-utils.js";
import { buildCandidateSummary, loadBatchCandidates } from "./batch-loader.js";
import { loadBatchOrThrow } from "./batch-loader.js";
import { getBatchReviews } from "./review-store.js";
import { getBatchRuntimeSelection } from "./runtime-selection-store.js";

function buildExportId(batchId) {
  const compactTimestamp = new Date().toISOString().replaceAll(":", "").replaceAll("-", "").replaceAll(".", "");
  return `runtime_selection_${batchId}_${compactTimestamp}`;
}

function formatLevelFileName(levelNumber) {
  return `level_${String(levelNumber).padStart(3, "0")}.json`;
}

function toRuntimeTile(tile) {
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

function toRuntimeLayout(layout) {
  return {
    CandidateId: String(layout?.candidateId || ""),
    LevelId: Number(layout?.levelId) || 0,
    Tiles: (layout?.tiles || []).map(toRuntimeTile)
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
    Layout: toRuntimeLayout(item.layout)
  };
}

async function backupExistingRuntimeDir(batch, exportId) {
  const commitRootDir = path.join(config.exportsDir, "commits", exportId);
  const backupDir = path.join(commitRootDir, "backup");
  const backupLevelsDir = path.join(backupDir, "levels");
  const sourceLevelsDir = path.join(batch.runtimeDir, "levels");

  await ensureDir(backupDir);

  if (await pathExists(batch.runtimeCatalogPath)) {
    await fs.copyFile(batch.runtimeCatalogPath, path.join(backupDir, "level-catalog.json"));
  }

  if (await pathExists(sourceLevelsDir)) {
    await ensureDir(backupLevelsDir);
    const entries = await fs.readdir(sourceLevelsDir, { withFileTypes: true });
    for (const entry of entries) {
      if (!entry.isFile()) {
        continue;
      }

      await fs.copyFile(
        path.join(sourceLevelsDir, entry.name),
        path.join(backupLevelsDir, entry.name)
      );
    }
  }

  return {
    commitRootDir,
    backupDir
  };
}

async function clearGeneratedRuntimeLevels(levelsDir) {
  if (!await pathExists(levelsDir)) {
    return;
  }

  const entries = await fs.readdir(levelsDir, { withFileTypes: true });
  for (const entry of entries) {
    if (!entry.isFile()) {
      continue;
    }

    if (!/^level_\d{3}\.json$/i.test(entry.name)) {
      continue;
    }

    await fs.unlink(path.join(levelsDir, entry.name));
  }
}

export async function createRuntimeSelectionDraft(batchId) {
  const selection = await getBatchRuntimeSelection(batchId);
  if (!selection.candidateIds.length) {
    const error = new Error("Runtime selection is empty.");
    error.statusCode = 400;
    throw error;
  }

  const candidates = await loadBatchCandidates(batchId);
  const reviewsByCandidateId = await getBatchReviews(batchId);
  const candidateById = new Map(candidates.map((candidate) => [candidate.candidateId, candidate]));
  const selectedCandidates = selection.candidateIds
    .map((candidateId) => candidateById.get(candidateId) || null)
    .filter(Boolean);

  if (!selectedCandidates.length) {
    const error = new Error("Runtime selection does not contain valid candidates.");
    error.statusCode = 400;
    throw error;
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
    generatedAt,
    selectionUpdatedAt: selection.updatedAt,
    itemCount: items.length,
    items
  };

  await ensureDir(config.exportsDir);
  const filePath = path.join(config.exportsDir, `${exportId}.json`);
  await writeJsonFile(filePath, payload);

  return {
    exportId,
    exportType: payload.exportType,
    batchId,
    generatedAt,
    selectionUpdatedAt: selection.updatedAt,
    itemCount: items.length,
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

export async function getExportDraft(exportId) {
  const filePath = path.join(config.exportsDir, `${exportId}.json`);
  if (!await pathExists(filePath)) {
    const error = new Error(`Unknown export: ${exportId}`);
    error.statusCode = 404;
    throw error;
  }

  const payload = await readJsonFile(filePath, null);
  if (!payload) {
    const error = new Error(`Failed to read export: ${exportId}`);
    error.statusCode = 500;
    throw error;
  }

  return {
    ...payload,
    filePath
  };
}

export async function commitExportDraft(exportId) {
  const draft = await getExportDraft(exportId);
  if (draft.exportType !== "runtime-selection-draft") {
    const error = new Error(`Unsupported export type: ${draft.exportType}`);
    error.statusCode = 400;
    throw error;
  }

  if (!Array.isArray(draft.items) || !draft.items.length) {
    const error = new Error("Export draft does not contain any items.");
    error.statusCode = 400;
    throw error;
  }

  const batch = await loadBatchOrThrow(draft.batchId);
  const { backupDir } = await backupExistingRuntimeDir(batch, exportId);
  const levelsDir = path.join(batch.runtimeDir, "levels");
  const catalogItems = draft.items.map(buildRuntimeCatalogItem);

  await ensureDir(batch.runtimeDir);
  await ensureDir(levelsDir);
  await clearGeneratedRuntimeLevels(levelsDir);
  await writeJsonFile(batch.runtimeCatalogPath, catalogItems);

  for (const item of draft.items) {
    const levelFileName = formatLevelFileName(item.levelNumber);
    const levelFilePath = path.join(levelsDir, levelFileName);
    await writeJsonFile(levelFilePath, buildRuntimeLevelPayload(item));
  }

  const committedAt = new Date().toISOString();
  const commit = {
    exportId,
    batchId: draft.batchId,
    committedAt,
    runtimeDir: batch.runtimeDir,
    levelCatalogPath: batch.runtimeCatalogPath,
    levelsDir,
    backupDir,
    itemCount: draft.items.length
  };

  await writeJsonFile(path.join(config.exportsDir, `${exportId}.json`), {
    ...draft,
    commit
  });

  return commit;
}
