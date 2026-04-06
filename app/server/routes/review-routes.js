import { readRequestJson, sendJson, sendNoContent } from "../lib/http.js";
import { deleteBatchReview, getBatchReviews, upsertBatchReview } from "../services/review-store.js";

export async function handleReviewRoutes(req, res, url) {
  const batchMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/reviews$/);
  if (batchMatch && req.method === "GET") {
    sendJson(res, 200, {
      items: await getBatchReviews(batchMatch[1])
    });
    return true;
  }

  const reviewMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/reviews\/([^/]+)$/);
  if (!reviewMatch) {
    return false;
  }

  const batchId = reviewMatch[1];
  const candidateId = decodeURIComponent(reviewMatch[2]);

  if (req.method === "PUT") {
    const body = await readRequestJson(req);
    sendJson(res, 200, {
      item: await upsertBatchReview(batchId, candidateId, body)
    });
    return true;
  }

  if (req.method === "DELETE") {
    await deleteBatchReview(batchId, candidateId);
    sendNoContent(res);
    return true;
  }

  return false;
}
