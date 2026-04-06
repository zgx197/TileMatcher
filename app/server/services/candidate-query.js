import { buildCandidateSummary, loadBatchCandidates } from "./batch-loader.js";

function normalizePageSize(rawValue, fallbackValue = 200) {
  const parsed = Number(rawValue);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallbackValue;
  }

  return Math.min(500, Math.floor(parsed));
}

function normalizePage(rawValue, fallbackValue = 1) {
  const parsed = Number(rawValue);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallbackValue;
  }

  return Math.floor(parsed);
}

function compareCandidates(sortKey, left, right) {
  switch (sortKey) {
    case "score_asc":
      return left.filterResult.recommendationScore - right.filterResult.recommendationScore;
    case "survival_desc":
      return right.evaluation.randomPlaySurvivalRate - left.evaluation.randomPlaySurvivalRate
        || right.filterResult.recommendationScore - left.filterResult.recommendationScore;
    case "dead_end_desc":
      return right.evaluation.deadEndRate - left.evaluation.deadEndRate
        || right.filterResult.recommendationScore - left.filterResult.recommendationScore;
    case "tile_count_desc":
      return right.evaluation.tileCount - left.evaluation.tileCount
        || right.filterResult.recommendationScore - left.filterResult.recommendationScore;
    case "batch_index_asc":
      return left.batchIndex - right.batchIndex;
    case "score_desc":
    default:
      return right.filterResult.recommendationScore - left.filterResult.recommendationScore
        || left.batchIndex - right.batchIndex;
  }
}

function matchesSearch(candidate, searchText, review) {
  if (!searchText) {
    return true;
  }

  const searchIndex = [
    candidate.candidateId,
    candidate.filterResult.decision,
    candidate.filterResult.difficultyBucket,
    candidate.filterResult.tags.join(" "),
    review?.mark || "",
    review?.comment || ""
  ].join(" ").toLowerCase();

  return searchIndex.includes(searchText);
}

export async function queryCandidates(batchId, filters = {}, reviewsByCandidateId = {}) {
  const candidates = await loadBatchCandidates(batchId);
  const searchText = String(filters.q || "").trim().toLowerCase();
  const decision = String(filters.decision || "").trim();
  const difficultyBucket = String(filters.difficultyBucket || "").trim();
  const tag = String(filters.tag || "").trim();
  const mark = String(filters.mark || "").trim();
  const minScore = filters.minScore === undefined || filters.minScore === null || filters.minScore === ""
    ? null
    : Number(filters.minScore);
  const maxScore = filters.maxScore === undefined || filters.maxScore === null || filters.maxScore === ""
    ? null
    : Number(filters.maxScore);
  const sort = String(filters.sort || "score_desc");
  const page = normalizePage(filters.page);
  const pageSize = normalizePageSize(filters.pageSize);

  const filteredCandidates = candidates
    .filter((candidate) => {
      const review = reviewsByCandidateId[candidate.candidateId] || null;

      if (decision && candidate.filterResult.decision !== decision) {
        return false;
      }

      if (difficultyBucket && candidate.filterResult.difficultyBucket !== difficultyBucket) {
        return false;
      }

      if (tag && !candidate.filterResult.tags.includes(tag)) {
        return false;
      }

      if (mark && review?.mark !== mark) {
        return false;
      }

      if (Number.isFinite(minScore) && candidate.filterResult.recommendationScore < minScore) {
        return false;
      }

      if (Number.isFinite(maxScore) && candidate.filterResult.recommendationScore > maxScore) {
        return false;
      }

      return matchesSearch(candidate, searchText, review);
    })
    .sort((left, right) => compareCandidates(sort, left, right));

  const totalCount = filteredCandidates.length;
  const startIndex = (page - 1) * pageSize;
  const pagedCandidates = filteredCandidates.slice(startIndex, startIndex + pageSize);

  return {
    items: pagedCandidates.map((candidate) => buildCandidateSummary(candidate, reviewsByCandidateId[candidate.candidateId] || null)),
    totalCount,
    page,
    pageSize
  };
}

export async function getCandidateDetail(batchId, candidateId, reviewsByCandidateId = {}) {
  const candidates = await loadBatchCandidates(batchId);
  const candidate = candidates.find((item) => item.candidateId === candidateId);
  if (!candidate) {
    const error = new Error(`Unknown candidate: ${candidateId}`);
    error.statusCode = 404;
    throw error;
  }

  return {
    ...candidate,
    review: reviewsByCandidateId[candidate.candidateId] || null
  };
}
