import { sendJson } from "../lib/http.js";
import { listBatches } from "../services/batch-registry.js";
import { loadBatchOverview } from "../services/batch-loader.js";
import { appendOperationLog } from "../services/operation-log-service.js";

export async function handleBootstrapRoutes(req, res, url) {
  if (req.method !== "GET" || url.pathname !== "/api/bootstrap") {
    return false;
  }

  const batches = await listBatches();
  const defaultBatch = batches[0] || null;
  const defaultOverview = defaultBatch ? await loadBatchOverview(defaultBatch.batchId) : null;
  await appendOperationLog({
    eventType: "bootstrap_loaded",
    batchCount: batches.length,
    defaultBatchId: defaultBatch?.batchId || ""
  });

  sendJson(res, 200, {
    batches,
    defaultBatchId: defaultBatch?.batchId || "",
    defaultOverview,
    workbench: {
      reviewMarks: ["keep", "review", "reject", "intro", "cover"],
      defaultSort: "score_desc"
    }
  });
  return true;
}
