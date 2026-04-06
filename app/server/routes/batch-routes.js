import { sendJson } from "../lib/http.js";
import { listBatches } from "../services/batch-registry.js";
import { loadBatchOverview, loadRuntimeCatalog } from "../services/batch-loader.js";

export async function handleBatchRoutes(req, res, url) {
  if (req.method === "GET" && url.pathname === "/api/batches") {
    sendJson(res, 200, {
      items: await listBatches()
    });
    return true;
  }

  const overviewMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/overview$/);
  if (overviewMatch && req.method === "GET") {
    sendJson(res, 200, await loadBatchOverview(overviewMatch[1]));
    return true;
  }

  const runtimeLevelsMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/runtime-levels$/);
  if (runtimeLevelsMatch && req.method === "GET") {
    sendJson(res, 200, {
      items: await loadRuntimeCatalog(runtimeLevelsMatch[1])
    });
    return true;
  }

  return false;
}
