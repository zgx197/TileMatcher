import {
  escapeHtml,
  formatCount,
  presentBatchSubtitle,
  presentDecision,
  presentDifficulty,
  presentMark
} from "./presenters.js";
import { renderCandidateList } from "./panels/candidate-list-panel.js";
import { renderComparePanel } from "./panels/compare-panel.js";
import { renderDiagnosticsPanel } from "./panels/diagnostics-panel.js";
import { renderRuntimeSelectionPanel } from "./panels/runtime-selection-panel.js";
import { renderDetailPanel } from "./panels/candidate-detail-panel.js";

function setSelectOptions(select, items, currentValue, presentLabel) {
  const placeholderOption = select.querySelector("option");

  while (select.options.length > 1) {
    select.remove(1);
  }

  for (const item of items || []) {
    const option = document.createElement("option");
    option.value = item;
    option.textContent = presentLabel(item);
    select.appendChild(option);
  }

  if (placeholderOption) {
    placeholderOption.selected = false;
  }

  select.value = currentValue || "";
}

function renderFlashBanner(dom, state) {
  const notice = state.notice;
  if (!notice?.message) {
    dom.flashBanner.classList.remove("is-visible");
    dom.flashBanner.textContent = "";
    dom.flashBanner.dataset.tone = "";
    return;
  }

  dom.flashBanner.textContent = notice.message;
  dom.flashBanner.dataset.tone = notice.tone || "info";
  dom.flashBanner.classList.add("is-visible");
}

function renderHeader(dom, state) {
  const currentBatch = state.bootstrap?.batches?.find((item) => item.batchId === state.currentBatchId) || state.overview?.batch || null;
  dom.heroBatchName.textContent = currentBatch?.batchName || "未发现批次";
  dom.heroGeneratedAt.textContent = presentBatchSubtitle(currentBatch);
}

function renderBatchControls(dom, state) {
  const batches = state.bootstrap?.batches || [];
  const currentBatchId = state.currentBatchId || state.bootstrap?.defaultBatchId || "";

  dom.batchSelect.innerHTML = "";
  for (const batch of batches) {
    const option = document.createElement("option");
    option.value = batch.batchId;
    option.textContent = `${batch.batchName} · ${formatCount(batch.candidateCount)}`;
    dom.batchSelect.appendChild(option);
  }

  dom.batchSelect.value = currentBatchId;

  const filters = state.filters;
  const overviewFilters = state.overview?.filters || { decisions: [], difficulties: [], tags: [] };
  setSelectOptions(dom.decisionSelect, overviewFilters.decisions, filters.decision, presentDecision);
  setSelectOptions(dom.difficultySelect, overviewFilters.difficulties, filters.difficultyBucket, presentDifficulty);
  setSelectOptions(dom.tagSelect, overviewFilters.tags, filters.tag, (item) => item);
  setSelectOptions(dom.markSelect, state.reviewMarks, filters.mark, presentMark);

  dom.searchInput.value = filters.q || "";
  dom.minScoreInput.value = filters.minScore || "";
  dom.sortSelect.value = filters.sort || state.defaultSort;
}

function renderOverview(dom, state) {
  const summary = state.overview?.summary;
  if (!summary) {
    dom.summaryCandidateCount.textContent = "-";
    dom.summaryAcceptedCount.textContent = "-";
    dom.summaryReviewCount.textContent = "-";
    dom.summaryRuntimeCount.textContent = "-";
    dom.topTags.innerHTML = '<span class="empty-state">等待批次摘要。</span>';
    return;
  }

  dom.summaryCandidateCount.textContent = formatCount(summary.candidateCount);
  dom.summaryAcceptedCount.textContent = formatCount(summary.acceptedCount);
  dom.summaryReviewCount.textContent = formatCount(summary.needsReviewCount);
  dom.summaryRuntimeCount.textContent = formatCount(summary.runtimeLevelCount);

  const topTags = state.overview?.distributions?.topTags || [];
  dom.topTags.innerHTML = topTags.length
    ? topTags.map((item, index) => (
      `<span class="chip ${index < 4 ? "" : "is-muted"}">${escapeHtml(item.key)} · ${formatCount(item.count)}</span>`
    )).join("")
    : '<span class="empty-state">当前批次暂无高频标签。</span>';
}

export function renderApp(dom, state) {
  renderFlashBanner(dom, state);
  renderHeader(dom, state);
  renderBatchControls(dom, state);
  renderOverview(dom, state);
  renderDiagnosticsPanel(dom, state);
  renderCandidateList(dom, state);
  renderComparePanel(dom, state);
  renderRuntimeSelectionPanel(dom, state);
  renderDetailPanel(dom, state);
}
