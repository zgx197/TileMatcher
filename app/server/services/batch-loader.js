import path from "node:path";
import { getBatchById } from "./batch-registry.js";
import { pathExists, readJsonFile } from "../lib/fs-utils.js";

const FILTER_DECISION_LABELS = {
  0: "AutoReject",
  1: "NeedsReview",
  2: "AutoAccept"
};

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

function normalizeDecision(rawValue) {
  if (typeof rawValue === "string" && rawValue.trim()) {
    return rawValue;
  }

  return FILTER_DECISION_LABELS[Number(rawValue)] || "AutoReject";
}

function normalizeTile(rawTile) {
  const shapeSource = pickFirst(rawTile, ["Shape", "shape"], {});
  return {
    id: Number(pickFirst(rawTile, ["Id", "id"], 0)) || 0,
    type: String(pickFirst(rawTile, ["Type", "type"], "")),
    gx: Number(pickFirst(rawTile, ["GX", "gx"], 0)) || 0,
    gy: Number(pickFirst(rawTile, ["GY", "gy"], 0)) || 0,
    gz: Number(pickFirst(rawTile, ["GZ", "gz"], 0)) || 0,
    shape: {
      widthUnits: Number(pickFirst(shapeSource, ["WidthUnits", "widthUnits"], 4)) || 4,
      heightUnits: Number(pickFirst(shapeSource, ["HeightUnits", "heightUnits"], 6)) || 6
    },
    removed: Boolean(pickFirst(rawTile, ["Removed", "removed"], false)),
    faceHiddenInitial: Boolean(pickFirst(rawTile, ["face_hidden_initial", "FaceHiddenInitial", "faceHiddenInitial"], false))
  };
}

function computeBounds(tiles) {
  if (!tiles.length) {
    return {
      minX: 0,
      minY: 0,
      maxX: 0,
      maxY: 0,
      maxZ: 0
    };
  }

  return {
    minX: Math.min(...tiles.map((tile) => tile.gx)),
    minY: Math.min(...tiles.map((tile) => tile.gy)),
    maxX: Math.max(...tiles.map((tile) => tile.gx + tile.shape.widthUnits)),
    maxY: Math.max(...tiles.map((tile) => tile.gy + tile.shape.heightUnits)),
    maxZ: Math.max(...tiles.map((tile) => tile.gz))
  };
}

function normalizeCandidateRecord(rawCandidate, runtimeLevelLookup) {
  const layoutSource = pickFirst(rawCandidate, ["Layout", "layout"], {});
  const candidateId = String(pickFirst(rawCandidate, ["CandidateId", "candidateId"], pickFirst(layoutSource, ["CandidateId", "candidateId"], "")));
  const tiles = (pickFirst(layoutSource, ["Tiles", "tiles"], []) || []).map(normalizeTile);
  const evaluationSource = pickFirst(rawCandidate, ["Evaluation", "evaluation"], {});
  const filterSource = pickFirst(rawCandidate, ["FilterResult", "filterResult"], {});

  return {
    candidateId,
    batchIndex: Number(pickFirst(rawCandidate, ["BatchIndex", "batchIndex"], 0)) || 0,
    seed: Number(pickFirst(rawCandidate, ["Seed", "seed"], 0)) || 0,
    runtimeLevelNumber: runtimeLevelLookup.get(candidateId) || null,
    layout: {
      candidateId,
      levelId: Number(pickFirst(layoutSource, ["LevelId", "levelId"], 0)) || 0,
      tiles
    },
    evaluation: {
      hasSolution: Boolean(pickFirst(evaluationSource, ["HasSolution", "hasSolution"], false)),
      solutionCountEstimate: Number(pickFirst(evaluationSource, ["SolutionCountEstimate", "solutionCountEstimate"], 0)) || 0,
      initialBranchCount: Number(pickFirst(evaluationSource, ["InitialBranchCount", "initialBranchCount"], 0)) || 0,
      averageBranchCount: Number(pickFirst(evaluationSource, ["AverageBranchCount", "averageBranchCount"], 0)) || 0,
      deadEndRate: Number(pickFirst(evaluationSource, ["DeadEndRate", "deadEndRate"], 0)) || 0,
      randomPlaySurvivalRate: Number(pickFirst(evaluationSource, ["RandomPlaySurvivalRate", "randomPlaySurvivalRate"], 0)) || 0,
      tileCount: Number(pickFirst(evaluationSource, ["TileCount", "tileCount"], tiles.length)) || tiles.length,
      layerCount: Number(pickFirst(evaluationSource, ["LayerCount", "layerCount"], 0)) || 0,
      searchVisitedStateCount: Number(pickFirst(evaluationSource, ["SearchVisitedStateCount", "searchVisitedStateCount"], 0)) || 0,
      searchDeadEndStateCount: Number(pickFirst(evaluationSource, ["SearchDeadEndStateCount", "searchDeadEndStateCount"], 0)) || 0
    },
    filterResult: {
      decision: normalizeDecision(pickFirst(filterSource, ["Decision", "decision"], "AutoReject")),
      difficultyBucket: String(pickFirst(filterSource, ["DifficultyBucket", "difficultyBucket"], "rejected")),
      recommendationScore: Number(pickFirst(filterSource, ["RecommendationScore", "recommendationScore"], 0)) || 0,
      rejectReasons: pickFirst(filterSource, ["RejectReasons", "rejectReasons"], []) || [],
      tags: pickFirst(filterSource, ["Tags", "tags"], []) || [],
      needsManualReview: Boolean(pickFirst(filterSource, ["NeedsManualReview", "needsManualReview"], false))
    },
    hiddenFaceCount: tiles.filter((tile) => tile.faceHiddenInitial).length,
    bounds: computeBounds(tiles)
  };
}

async function loadRuntimeLevelLookup(batch) {
  const catalog = await readJsonFile(batch.runtimeCatalogPath, []);
  return new Map((catalog || []).map((item) => [
    String(pickFirst(item, ["CandidateId", "candidateId"], "")),
    Number(pickFirst(item, ["LevelNumber", "levelNumber"], 0)) || null
  ]));
}

async function loadCandidatesFromLegacyFile(batch, runtimeLevelLookup) {
  const rawCandidates = await readJsonFile(batch.candidatesPath, []);
  return (rawCandidates || []).map((rawCandidate) => normalizeCandidateRecord(rawCandidate, runtimeLevelLookup));
}

async function loadCandidatesFromIndexFiles(batch, runtimeLevelLookup) {
  const rawIndexItems = await readJsonFile(batch.candidateIndexPath, []);
  if (!rawIndexItems?.length) {
    return [];
  }

  const detailItems = [];
  for (const rawItem of rawIndexItems) {
    const detailFile = pickFirst(rawItem, ["DetailFile", "detailFile"], "");
    if (!detailFile) {
      continue;
    }

    const detailPath = path.join(batch.candidateDetailDir, detailFile);
    const detailPayload = await readJsonFile(detailPath, null);
    if (detailPayload) {
      detailItems.push(normalizeCandidateRecord(detailPayload, runtimeLevelLookup));
    }
  }

  return detailItems;
}

export async function loadBatchOrThrow(batchId) {
  const batch = await getBatchById(batchId);
  if (!batch) {
    const error = new Error(`Unknown batch: ${batchId}`);
    error.statusCode = 404;
    throw error;
  }

  return batch;
}

export async function loadRuntimeCatalog(batchId) {
  const batch = await loadBatchOrThrow(batchId);
  const catalog = await readJsonFile(batch.runtimeCatalogPath, []);
  return (catalog || []).map((item) => ({
    levelNumber: Number(pickFirst(item, ["LevelNumber", "levelNumber"], 0)) || 0,
    candidateId: String(pickFirst(item, ["CandidateId", "candidateId"], "")),
    difficultyBucket: String(pickFirst(item, ["DifficultyBucket", "difficultyBucket"], "")),
    recommendationScore: Number(pickFirst(item, ["RecommendationScore", "recommendationScore"], 0)) || 0,
    fileName: String(pickFirst(item, ["FileName", "fileName"], ""))
  }));
}

export async function loadBatchCandidates(batchId) {
  const batch = await loadBatchOrThrow(batchId);
  const runtimeLevelLookup = await loadRuntimeLevelLookup(batch);

  if (await pathExists(batch.candidateIndexPath) && await pathExists(batch.candidateDetailDir)) {
    const indexCandidates = await loadCandidatesFromIndexFiles(batch, runtimeLevelLookup);
    if (indexCandidates.length) {
      return indexCandidates;
    }
  }

  return loadCandidatesFromLegacyFile(batch, runtimeLevelLookup);
}

export async function loadBatchOverview(batchId) {
  const batch = await loadBatchOrThrow(batchId);
  const candidates = await loadBatchCandidates(batchId);
  const runtimeCatalog = await loadRuntimeCatalog(batchId);
  const decisionCounts = new Map();
  const bucketCounts = new Map();
  const tagCounts = new Map();

  for (const candidate of candidates) {
    decisionCounts.set(candidate.filterResult.decision, (decisionCounts.get(candidate.filterResult.decision) || 0) + 1);
    bucketCounts.set(candidate.filterResult.difficultyBucket, (bucketCounts.get(candidate.filterResult.difficultyBucket) || 0) + 1);
    for (const tag of candidate.filterResult.tags) {
      tagCounts.set(tag, (tagCounts.get(tag) || 0) + 1);
    }
  }

  return {
    batch: {
      batchId: batch.batchId,
      batchName: batch.batchName,
      generatedAtUtc: batch.generatedAtUtc,
      candidateCount: batch.candidateCount,
      acceptedCount: batch.acceptedCount,
      needsReviewCount: batch.needsReviewCount,
      rejectedCount: batch.rejectedCount,
      sourceKind: batch.sourceKind
    },
    summary: {
      candidateCount: candidates.length,
      acceptedCount: batch.acceptedCount,
      needsReviewCount: batch.needsReviewCount,
      rejectedCount: batch.rejectedCount,
      runtimeLevelCount: runtimeCatalog.length
    },
    filters: {
      decisions: [...decisionCounts.keys()].sort(),
      difficulties: [...bucketCounts.keys()].sort(),
      tags: [...tagCounts.keys()].sort()
    },
    distributions: {
      decisions: [...decisionCounts.entries()].map(([key, count]) => ({ key, count })),
      buckets: [...bucketCounts.entries()].map(([key, count]) => ({ key, count })),
      topTags: [...tagCounts.entries()]
        .sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0], "zh-CN"))
        .slice(0, 12)
        .map(([key, count]) => ({ key, count }))
    }
  };
}

export function buildCandidateSummary(candidate, review = null) {
  return {
    candidateId: candidate.candidateId,
    batchIndex: candidate.batchIndex,
    seed: candidate.seed,
    runtimeLevelNumber: candidate.runtimeLevelNumber,
    decision: candidate.filterResult.decision,
    difficultyBucket: candidate.filterResult.difficultyBucket,
    recommendationScore: candidate.filterResult.recommendationScore,
    tileCount: candidate.evaluation.tileCount,
    layerCount: candidate.evaluation.layerCount,
    initialBranchCount: candidate.evaluation.initialBranchCount,
    averageBranchCount: candidate.evaluation.averageBranchCount,
    deadEndRate: candidate.evaluation.deadEndRate,
    randomPlaySurvivalRate: candidate.evaluation.randomPlaySurvivalRate,
    tags: candidate.filterResult.tags,
    hiddenFaceCount: candidate.hiddenFaceCount,
    bounds: candidate.bounds,
    review
  };
}
