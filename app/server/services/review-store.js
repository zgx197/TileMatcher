import { config } from "../config.js";
import { ensureDir, readJsonFile, writeJsonFile } from "../lib/fs-utils.js";

const ALLOWED_MARKS = new Set(["keep", "review", "reject", "intro", "cover"]);

function normalizeReviewPayload(payload = {}) {
  const mark = String(payload.mark || "").trim();
  const labels = Array.isArray(payload.labels)
    ? payload.labels.map((item) => String(item || "").trim()).filter(Boolean)
    : [];
  const comment = String(payload.comment || "").trim();

  return {
    mark: ALLOWED_MARKS.has(mark) ? mark : "",
    labels,
    comment
  };
}

async function loadAllReviews() {
  await ensureDir(config.workbenchDir);
  return readJsonFile(config.reviewsFile, {});
}

async function saveAllReviews(payload) {
  await writeJsonFile(config.reviewsFile, payload);
}

export async function getBatchReviews(batchId) {
  const allReviews = await loadAllReviews();
  return allReviews[batchId] || {};
}

export async function upsertBatchReview(batchId, candidateId, payload) {
  const allReviews = await loadAllReviews();
  const batchReviews = { ...(allReviews[batchId] || {}) };
  const normalizedReview = normalizeReviewPayload(payload);

  batchReviews[candidateId] = {
    ...normalizedReview,
    updatedAt: new Date().toISOString()
  };

  allReviews[batchId] = batchReviews;
  await saveAllReviews(allReviews);
  return batchReviews[candidateId];
}

export async function deleteBatchReview(batchId, candidateId) {
  const allReviews = await loadAllReviews();
  const batchReviews = { ...(allReviews[batchId] || {}) };
  delete batchReviews[candidateId];
  allReviews[batchId] = batchReviews;
  await saveAllReviews(allReviews);
}
