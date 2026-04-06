import { sendJson } from "../lib/http.js";
import { getBatchIntegrity, getHealthStatus, getWorkbenchDiagnostics } from "../services/diagnostics-service.js";
import { appendOperationLog } from "../services/operation-log-service.js";

export async function handleDiagnosticRoutes(req, res, url) {
  if (req.method === "GET" && url.pathname === "/api/health") {
    sendJson(res, 200, await getHealthStatus());
    return true;
  }

  if (req.method === "GET" && url.pathname === "/api/diagnostics") {
    const diagnostics = await getWorkbenchDiagnostics();
    await appendOperationLog({
      eventType: "diagnostics_loaded",
      status: diagnostics.health.status,
      batchCount: diagnostics.batches.totalCount
    });
    sendJson(res, 200, diagnostics);
    return true;
  }

  const batchIntegrityMatch = url.pathname.match(/^\/api\/batches\/([^/]+)\/integrity$/);
  if (batchIntegrityMatch && req.method === "GET") {
    const integrity = await getBatchIntegrity(batchIntegrityMatch[1]);
    await appendOperationLog({
      eventType: "batch_integrity_checked",
      batchId: integrity.batchId,
      status: integrity.status
    });
    sendJson(res, 200, integrity);
    return true;
  }

  return false;
}
