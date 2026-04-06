import http from "node:http";
import { config } from "./config.js";
import { sendError, sendStaticFile } from "./lib/http.js";
import { handleBootstrapRoutes } from "./routes/bootstrap-routes.js";
import { handleBatchRoutes } from "./routes/batch-routes.js";
import { handleCandidateRoutes } from "./routes/candidate-routes.js";
import { handleDiagnosticRoutes } from "./routes/diagnostic-routes.js";
import { handleExportRoutes } from "./routes/export-routes.js";
import { handleReviewRoutes } from "./routes/review-routes.js";
import { appendOperationLog } from "./services/operation-log-service.js";

async function handleApi(req, res, url) {
  if (await handleDiagnosticRoutes(req, res, url)) {
    return true;
  }

  if (await handleBootstrapRoutes(req, res, url)) {
    return true;
  }

  if (await handleBatchRoutes(req, res, url)) {
    return true;
  }

  if (await handleCandidateRoutes(req, res, url)) {
    return true;
  }

  if (await handleReviewRoutes(req, res, url)) {
    return true;
  }

  if (await handleExportRoutes(req, res, url)) {
    return true;
  }

  return false;
}

async function requestHandler(req, res) {
  const url = new URL(req.url || "/", `http://${req.headers.host}`);

  try {
    if (url.pathname.startsWith("/api/")) {
      const handled = await handleApi(req, res, url);
      if (!handled) {
        sendError(res, 404, "API route not found.");
      }
      return;
    }

    await sendStaticFile(res, config.webDir, url.pathname === "/" ? "/index.html" : url.pathname);
  } catch (error) {
    try {
      await appendOperationLog({
        eventType: "request_failed",
        method: req.method || "",
        path: url.pathname,
        statusCode: error.statusCode || 500,
        message: error.message
      });
    } catch {
      // Keep the original request failure visible even if log persistence fails.
    }

    if (error.code === "ENOENT") {
      sendError(res, 404, "File not found.");
      return;
    }

    sendError(res, error.statusCode || 500, error.message, error.details || null);
  }
}

const server = http.createServer((req, res) => {
  requestHandler(req, res);
});

server.listen(config.port, () => {
  appendOperationLog({
    eventType: "server_started",
    port: config.port,
    webDir: config.webDir
  }).catch(() => {});
  console.log(`[analysis] Workbench server ready at http://127.0.0.1:${config.port}`);
});
