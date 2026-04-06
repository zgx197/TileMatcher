import { readRequestJson, sendJson } from "../lib/http.js";
import { createRuntimeSelectionDraft, getExportDraft } from "../services/export-draft-service.js";
import { buildCandidateSummary, loadBatchCandidates } from "../services/batch-loader.js";
import { getBatchReviews } from "../services/review-store.js";
import { getBatchRuntimeSelection, saveBatchRuntimeSelection } from "../services/runtime-selection-store.js";

async function buildRuntimeSelectionResponse(batchId, selection) {
  const candidates = await loadBatchCandidates(batchId);
  const reviewsByCandidateId = await getBatchReviews(batchId);
  const candidateById = new Map(candidates.map((candidate) => [candidate.candidateId, candidate]));
  const items = selection.candidateIds
    .map((candidateId) => candidateById.get(candidateId) || null)
    .filter(Boolean)
    .map((candidate, index) => ({
      levelNumber: index + 1,
      ...buildCandidateSummary(candidate, reviewsByCandidateId[candidate.candidateId] || null)
    }));

  return {
    batchId,
    candidateIds: items.map((item) => item.candidateId),
    updatedAt: selection.updatedAt,
    items
  };
}

export async function handleExportRoutes(req, res, url) {
  const selectionMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/runtime-selection$/);
  if (selectionMatch && req.method === "GET") {
    const batchId = selectionMatch[1];
    const selection = await getBatchRuntimeSelection(batchId);
    sendJson(res, 200, {
      item: await buildRuntimeSelectionResponse(batchId, selection)
    });
    return true;
  }

  if (selectionMatch && req.method === "PUT") {
    const batchId = selectionMatch[1];
    const body = await readRequestJson(req);
    const selection = await saveBatchRuntimeSelection(batchId, body.candidateIds);
    sendJson(res, 200, {
      item: await buildRuntimeSelectionResponse(batchId, selection)
    });
    return true;
  }

  const draftMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/exports\/runtime-selection$/);
  if (draftMatch && req.method === "POST") {
    sendJson(res, 200, {
      item: await createRuntimeSelectionDraft(draftMatch[1])
    });
    return true;
  }

  const exportMatch = url.pathname.match(/^\/api\/exports\/([^/]+)$/);
  if (exportMatch && req.method === "GET") {
    sendJson(res, 200, {
      item: await getExportDraft(exportMatch[1])
    });
    return true;
  }

  return false;
}
