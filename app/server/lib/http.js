import fs from "node:fs/promises";
import path from "node:path";

const CONTENT_TYPES = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml"
};

export async function readRequestJson(req) {
  const chunks = [];
  for await (const chunk of req) {
    chunks.push(chunk);
  }

  const body = Buffer.concat(chunks).toString("utf8");
  return body ? JSON.parse(body) : {};
}

export function sendJson(res, statusCode, payload) {
  res.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store"
  });
  res.end(JSON.stringify(payload));
}

export function sendError(res, statusCode, message, details = null) {
  sendJson(res, statusCode, {
    error: {
      message,
      details
    }
  });
}

export function sendNoContent(res) {
  res.writeHead(204, {
    "Cache-Control": "no-store"
  });
  res.end();
}

export async function sendStaticFile(res, rootDir, relativePath) {
  const safePath = relativePath === "/" ? "/index.html" : relativePath;
  const normalizedRelativePath = path.normalize(safePath).replace(/^(\.\.[/\\])+/, "");
  const filePath = path.join(rootDir, normalizedRelativePath);
  const resolvedRoot = path.resolve(rootDir);
  const resolvedFile = path.resolve(filePath);

  if (!resolvedFile.startsWith(resolvedRoot)) {
    const error = new Error("Invalid static asset path.");
    error.code = "EINVAL";
    throw error;
  }

  const extension = path.extname(resolvedFile);
  const raw = await fs.readFile(resolvedFile);
  res.writeHead(200, {
    "Content-Type": CONTENT_TYPES[extension] || "application/octet-stream",
    "Cache-Control": "no-store"
  });
  res.end(raw);
}
