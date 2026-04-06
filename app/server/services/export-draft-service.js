import path from "node:path";
import { config } from "../config.js";
import { ensureDir, pathExists, readJsonFile, writeJsonFile } from "../lib/fs-utils.js";
import { buildCandidateSummary, loadBatchCandidates } from "./batch-loader.js";
import { getBatchReviews } from "./review-store.js";
import { getBatchRuntimeSelection } from "./runtime-selection-store.js";

function buildExportId(batchId) {
  const compactTimestamp = new Date().toISOString().replaceAll(":", "").replaceAll("-", "").replaceAll(".", "");
  return `runtime_selection_${batchId}_${compactTimestamp}`;
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
