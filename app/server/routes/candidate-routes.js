import { sendJson } from "../lib/http.js";
import { getCandidateDetail, queryCandidates } from "../services/candidate-query.js";
import { getBatchReviews } from "../services/review-store.js";

export async function handleCandidateRoutes(req, res, url) {
  const listMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/candidates$/);
  if (listMatch && req.method === "GET") {
    const batchId = listMatch[1];
    const reviewsByCandidateId = await getBatchReviews(batchId);
    sendJson(res, 200, await queryCandidates(batchId, {
      q: url.searchParams.get("q"),
      decision: url.searchParams.get("decision"),
      difficultyBucket: url.searchParams.get("difficultyBucket"),
      tag: url.searchParams.get("tag"),
      mark: url.searchParams.get("mark"),
      minScore: url.searchParams.get("minScore"),
      maxScore: url.searchParams.get("maxScore"),
      sort: url.searchParams.get("sort"),
      page: url.searchParams.get("page"),
      pageSize: url.searchParams.get("pageSize")
    }, reviewsByCandidateId));
    return true;
  }

  const detailMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/candidates\/([^/]+)$/);
  if (detailMatch && req.method === "GET") {
    const batchId = detailMatch[1];
    const candidateId = decodeURIComponent(detailMatch[2]);
    const reviewsByCandidateId = await getBatchReviews(batchId);
    sendJson(res, 200, {
      item: await getCandidateDetail(batchId, candidateId, reviewsByCandidateId)
    });
    return true;
  }

  return false;
}
