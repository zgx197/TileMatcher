import http from "node:http";
import { config } from "./config.js";
import { sendError, sendJson, sendStaticFile } from "./lib/http.js";
import { handleBootstrapRoutes } from "./routes/bootstrap-routes.js";
import { handleBatchRoutes } from "./routes/batch-routes.js";
import { handleCandidateRoutes } from "./routes/candidate-routes.js";
import { handleExportRoutes } from "./routes/export-routes.js";
import { handleReviewRoutes } from "./routes/review-routes.js";

async function handleApi(req, res, url) {
  if (req.method === "GET" && url.pathname === "/api/health") {
    sendJson(res, 200, { ok: true });
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
    if (error.code === "ENOENT") {
      sendError(res, 404, "File not found.");
      return;
    }

    sendError(res, error.statusCode || 500, error.message);
  }
}

const server = http.createServer((req, res) => {
  requestHandler(req, res);
});

server.listen(config.port, () => {
  console.log(`[analysis] Workbench server ready at http://127.0.0.1:${config.port}`);
});
