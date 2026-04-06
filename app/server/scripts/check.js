import { listBatches } from "../services/batch-registry.js";
import { loadBatchOverview, loadRuntimeCatalog } from "../services/batch-loader.js";
import { queryCandidates, getCandidateDetail } from "../services/candidate-query.js";

const batches = await listBatches();
if (!batches.length) {
  throw new Error("未找到可用离线批次，请先运行离线导出。");
}

const batch = batches[0];
const overview = await loadBatchOverview(batch.batchId);
const runtimeLevels = await loadRuntimeCatalog(batch.batchId);
const candidatePage = await queryCandidates(batch.batchId, {
  page: 1,
  pageSize: 10,
  sort: "score_desc"
});

if (!candidatePage.items.length) {
  throw new Error(`批次 ${batch.batchId} 没有可读取的候选。`);
}

const detail = await getCandidateDetail(batch.batchId, candidatePage.items[0].candidateId);

console.log(`[analysis:check] Batch: ${batch.batchId}`);
console.log(`[analysis:check] Candidates: ${overview.summary.candidateCount}`);
console.log(`[analysis:check] Runtime levels: ${runtimeLevels.length}`);
console.log(`[analysis:check] First candidate: ${detail.candidateId}`);
console.log("[analysis:check] API data services are ready.");
