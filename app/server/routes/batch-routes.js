import { sendJson } from "../lib/http.js";
import { listBatches } from "../services/batch-registry.js";
import { loadBatchOverview, loadRuntimeCatalog } from "../services/batch-loader.js";
import { appendOperationLog } from "../services/operation-log-service.js";

export async function handleBatchRoutes(req, res, url) {
  if (req.method === "GET" && url.pathname === "/api/batches") {
    sendJson(res, 200, {
      items: await listBatches()
    });
    return true;
  }

  const overviewMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/overview$/);
  if (overviewMatch && req.method === "GET") {
    const overview = await loadBatchOverview(overviewMatch[1]);
    await appendOperationLog({
      eventType: "batch_overview_loaded",
      batchId: overview.batch.batchId,
      candidateCount: overview.summary.candidateCount
    });
    sendJson(res, 200, overview);
    return true;
  }

  const runtimeLevelsMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/runtime-levels$/);
  if (runtimeLevelsMatch && req.method === "GET") {
    const items = await loadRuntimeCatalog(runtimeLevelsMatch[1]);
    await appendOperationLog({
      eventType: "runtime_catalog_loaded",
      batchId: runtimeLevelsMatch[1],
      runtimeLevelCount: items.length
    });
    sendJson(res, 200, { items });
    return true;
  }

  return false;
}
