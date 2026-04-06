import fs from "node:fs/promises";
import path from "node:path";
import { config } from "../config.js";
import { listBatches, getBatchById } from "./batch-registry.js";
import { readRecentOperationLogs } from "./operation-log-service.js";
import { pathExists, readJsonFile } from "../lib/fs-utils.js";

function buildCheck(key, label, status, details = {}, affectsOverallStatus = true) {
  return {
    key,
    label,
    status,
    details,
    affectsOverallStatus
  };
}

function mergeStatus(currentStatus, nextStatus) {
  if (currentStatus === "error" || nextStatus === "error") {
    return "error";
  }

  if (currentStatus === "warning" || nextStatus === "warning") {
    return "warning";
  }

  return "ok";
}

function summarizeChecks(checks) {
  return checks.reduce((status, check) => {
    if (check.affectsOverallStatus === false) {
      return status;
    }

    return mergeStatus(status, check.status);
  }, "ok");
}

async function inspectPathCheck(key, label, targetPath, missingStatus = "warning", affectsOverallStatus = true) {
  const exists = await pathExists(targetPath);
  return buildCheck(key, label, exists ? "ok" : missingStatus, {
    path: targetPath,
    exists
  }, affectsOverallStatus);
}

async function inspectRuntimeFiles(batch) {
  const checks = [];
  const runtimeCatalogCheck = await inspectPathCheck("runtime_catalog", "运行时目录索引", batch.runtimeCatalogPath, "error");
  checks.push(runtimeCatalogCheck);

  const runtimeLevelsDir = path.join(batch.runtimeDir, "levels");
  const runtimeLevelsDirCheck = await inspectPathCheck("runtime_levels_dir", "运行时关卡目录", runtimeLevelsDir, "warning");
  checks.push(runtimeLevelsDirCheck);

  let runtimeLevelFileCount = 0;
  if (runtimeLevelsDirCheck.details.exists) {
    const entries = await fs.readdir(runtimeLevelsDir, { withFileTypes: true });
    runtimeLevelFileCount = entries.filter((entry) => entry.isFile() && /\.json$/iu.test(entry.name)).length;
  }

  checks.push(buildCheck("runtime_levels_file_count", "运行时关卡文件数", runtimeLevelFileCount > 0 ? "ok" : "warning", {
    path: runtimeLevelsDir,
    runtimeLevelFileCount
  }));

  return checks;
}

async function inspectCandidateStorage(batch) {
  const checks = [];
  const hasIndex = await pathExists(batch.candidateIndexPath);
  const hasDetailDir = await pathExists(batch.candidateDetailDir);
  const hasLegacyCandidates = await pathExists(batch.candidatesPath);

  checks.push(buildCheck("candidate_index", "候选索引文件", hasIndex ? "ok" : "warning", {
    path: batch.candidateIndexPath,
    exists: hasIndex
  }, false));
  checks.push(buildCheck("candidate_detail_dir", "候选详情目录", hasDetailDir ? "ok" : "warning", {
    path: batch.candidateDetailDir,
    exists: hasDetailDir
  }, false));
  checks.push(buildCheck("legacy_candidates", "兼容候选文件", hasLegacyCandidates ? "ok" : "warning", {
    path: batch.candidatesPath,
    exists: hasLegacyCandidates
  }, false));

  if (hasIndex && hasDetailDir) {
    const candidateIndex = await readJsonFile(batch.candidateIndexPath, []);
    const missingDetailFiles = [];

    for (const item of candidateIndex || []) {
      const detailFile = String(item.DetailFile || item.detailFile || "");
      if (!detailFile) {
        continue;
      }

      const detailPath = path.join(batch.candidateDetailDir, detailFile);
      if (!await pathExists(detailPath)) {
        missingDetailFiles.push(detailFile);
      }
    }

    checks.push(buildCheck("candidate_detail_integrity", "候选详情索引完整性", missingDetailFiles.length ? "error" : "ok", {
      indexedCandidateCount: Array.isArray(candidateIndex) ? candidateIndex.length : 0,
      missingDetailFileCount: missingDetailFiles.length,
      sampleMissingDetailFiles: missingDetailFiles.slice(0, 5)
    }));
  } else if (!hasLegacyCandidates) {
    checks.push(buildCheck("candidate_data_available", "候选数据可读取性", "error", {
      message: "缺少候选索引与兼容 candidates.json，当前批次无法正常读取候选。"
    }));
  }

  return checks;
}

export async function getHealthStatus() {
  const batches = await listBatches();
  const checks = [
    await inspectPathCheck("batch_root", "批次根目录", config.batchRootDir, "warning", false),
    await inspectPathCheck("legacy_batch_root", "兼容批次目录", config.legacyBatchRoot, "warning", false),
    await inspectPathCheck("workbench_dir", "工作台存储目录", config.workbenchDir, "warning"),
    await inspectPathCheck("operations_log", "操作日志文件", config.operationsLogFile, "warning", false)
  ];

  const status = summarizeChecks(checks);
  return {
    ok: status !== "error",
    status,
    checkedAt: new Date().toISOString(),
    uptimeSeconds: Math.round(process.uptime()),
    port: config.port,
    batchCount: batches.length,
    checks
  };
}

export async function getWorkbenchDiagnostics() {
  const [health, batches, recentOperations] = await Promise.all([
    getHealthStatus(),
    listBatches(),
    readRecentOperationLogs(12)
  ]);

  return {
    checkedAt: new Date().toISOString(),
    health,
    workbench: {
      repoRoot: config.repoRoot,
      workbenchDir: config.workbenchDir,
      exportsDir: config.exportsDir,
      operationsLogFile: config.operationsLogFile
    },
    batches: {
      totalCount: batches.length,
      latestBatchId: batches[0]?.batchId || "",
      latestGeneratedAtUtc: batches[0]?.generatedAtUtc || ""
    },
    recentOperations
  };
}

export async function getBatchIntegrity(batchId) {
  const batch = await getBatchById(batchId);
  if (!batch) {
    const error = new Error(`Unknown batch: ${batchId}`);
    error.statusCode = 404;
    throw error;
  }

  const checks = [
    await inspectPathCheck("batch_root", "批次目录", batch.rootDir, "error"),
    await inspectPathCheck("analysis_dir", "分析目录", batch.analysisDir, "error"),
    await inspectPathCheck("summary_file", "批次摘要文件", batch.summaryPath, "error")
  ];

  checks.push(...await inspectRuntimeFiles(batch));
  checks.push(...await inspectCandidateStorage(batch));

  return {
    batchId: batch.batchId,
    batchName: batch.batchName,
    checkedAt: new Date().toISOString(),
    status: summarizeChecks(checks),
    checks
  };
}
