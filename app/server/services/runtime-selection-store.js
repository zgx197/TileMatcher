import { config } from "../config.js";
import { ensureDir, readJsonFile, writeJsonFile } from "../lib/fs-utils.js";

function normalizeCandidateIds(candidateIds) {
  const normalized = [];
  const seen = new Set();

  for (const rawId of Array.isArray(candidateIds) ? candidateIds : []) {
    const candidateId = String(rawId || "").trim();
    if (!candidateId || seen.has(candidateId)) {
      continue;
    }

    seen.add(candidateId);
    normalized.push(candidateId);
  }

  return normalized;
}

async function loadAllSelections() {
  await ensureDir(config.workbenchDir);
  return readJsonFile(config.runtimeSelectionsFile, {});
}

async function saveAllSelections(payload) {
  await writeJsonFile(config.runtimeSelectionsFile, payload);
}

export async function getBatchRuntimeSelection(batchId) {
  const allSelections = await loadAllSelections();
  const selection = allSelections[batchId] || {};

  return {
    candidateIds: normalizeCandidateIds(selection.candidateIds),
    updatedAt: String(selection.updatedAt || "")
  };
}

export async function saveBatchRuntimeSelection(batchId, candidateIds) {
  const allSelections = await loadAllSelections();
  const normalizedCandidateIds = normalizeCandidateIds(candidateIds);

  allSelections[batchId] = {
    candidateIds: normalizedCandidateIds,
    updatedAt: new Date().toISOString()
  };

  await saveAllSelections(allSelections);
  return allSelections[batchId];
}
