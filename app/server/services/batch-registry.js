import path from "node:path";
import { config } from "../config.js";
import { listDirectories, pathExists, readJsonFile } from "../lib/fs-utils.js";

function pickFirst(source, keys, fallbackValue = null) {
  if (!source || typeof source !== "object") {
    return fallbackValue;
  }

  for (const key of keys) {
    if (source[key] !== undefined && source[key] !== null) {
      return source[key];
    }
  }

  return fallbackValue;
}

function normalizeBatchRecord(baseRecord, summary) {
  return {
    ...baseRecord,
    batchName: String(pickFirst(summary, ["BatchName", "batchName"], baseRecord.batchId)),
    candidateCount: Number(pickFirst(summary, ["CandidateCount", "candidateCount"], 0)) || 0,
    acceptedCount: Number(pickFirst(summary, ["AcceptedCount", "acceptedCount"], 0)) || 0,
    needsReviewCount: Number(pickFirst(summary, ["NeedsReviewCount", "needsReviewCount"], 0)) || 0,
    rejectedCount: Number(pickFirst(summary, ["RejectedCount", "rejectedCount"], 0)) || 0,
    generatedAtUtc: String(pickFirst(summary, ["GeneratedAtUtc", "generatedAtUtc"], ""))
  };
}

async function buildManifestBatchRecord(batchDirName) {
  const rootDir = path.join(config.batchRootDir, batchDirName);
  const manifestPath = path.join(rootDir, "manifest.json");
  if (!await pathExists(manifestPath)) {
    return null;
  }

  const manifest = await readJsonFile(manifestPath, {});
  const analysisDir = path.join(rootDir, "analysis");
  const runtimeDir = path.join(rootDir, "runtime-levels");
  const summaryPath = path.join(analysisDir, "summary.json");
  const summary = await readJsonFile(summaryPath, manifest);

  return normalizeBatchRecord({
    batchId: String(pickFirst(manifest, ["BatchId", "batchId"], batchDirName)),
    sourceKind: "manifest",
    rootDir,
    analysisDir,
    runtimeDir,
    summaryPath,
    candidatesPath: path.join(analysisDir, "candidates.json"),
    candidateIndexPath: path.join(analysisDir, "candidates.index.json"),
    candidateDetailDir: path.join(analysisDir, "candidates"),
    runtimeCatalogPath: path.join(runtimeDir, "level-catalog.json")
  }, summary);
}

async function buildLegacyBatchRecord() {
  const rootDir = config.legacyBatchRoot;
  const summaryPath = path.join(rootDir, "analysis", "summary.json");
  if (!await pathExists(summaryPath)) {
    return null;
  }

  const summary = await readJsonFile(summaryPath, {});
  return normalizeBatchRecord({
    batchId: "mahjong-mvp",
    sourceKind: "legacy",
    rootDir,
    analysisDir: path.join(rootDir, "analysis"),
    runtimeDir: path.join(rootDir, "runtime-levels"),
    summaryPath,
    candidatesPath: path.join(rootDir, "analysis", "candidates.json"),
    candidateIndexPath: path.join(rootDir, "analysis", "candidates.index.json"),
    candidateDetailDir: path.join(rootDir, "analysis", "candidates"),
    runtimeCatalogPath: path.join(rootDir, "runtime-levels", "level-catalog.json")
  }, summary);
}

export async function listBatches() {
  const manifestBatchDirs = await listDirectories(config.batchRootDir);
  const manifestBatches = (await Promise.all(manifestBatchDirs.map(buildManifestBatchRecord)))
    .filter(Boolean);
  const legacyBatch = await buildLegacyBatchRecord();
  const allBatches = legacyBatch
    ? [...manifestBatches, legacyBatch]
    : manifestBatches;

  return allBatches.sort((left, right) => (
    String(right.generatedAtUtc || "").localeCompare(String(left.generatedAtUtc || ""))
    || left.batchName.localeCompare(right.batchName, "zh-CN")
  ));
}

export async function getBatchById(batchId) {
  const batches = await listBatches();
  return batches.find((batch) => batch.batchId === batchId) || null;
}
