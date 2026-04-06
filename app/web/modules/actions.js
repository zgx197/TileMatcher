import * as api from "./api.js";
import { renderApp } from "./renderers.js";
import { createDefaultFilters, createEmptyDiagnosticsState } from "./state.js";
import {
  commitRuntimeDraft,
  createRuntimeDraft,
  loadBatchExportHistory,
  loadRuntimeDraft,
  loadRuntimeSelection,
  moveRuntimeSelection,
  resetRuntimeSelectionState,
  toggleRuntimeSelection
} from "./runtime-selection-actions.js";

let noticeTimer = null;

function setNotice(state, message, tone = "info") {
  state.notice = message ? { message, tone } : null;
}

function flashNotice(dom, state, message, tone = "info") {
  setNotice(state, message, tone);
  renderApp(dom, state);

  if (noticeTimer) {
    clearTimeout(noticeTimer);
  }

  noticeTimer = window.setTimeout(() => {
    state.notice = null;
    renderApp(dom, state);
  }, 2400);
}

function clearNotice(state) {
  state.notice = null;
}

function applyOverviewFilterGuards(state) {
  const filters = state.overview?.filters || { decisions: [], difficulties: [], tags: [] };

  if (state.filters.decision && !filters.decisions.includes(state.filters.decision)) {
    state.filters.decision = "";
  }

  if (state.filters.difficultyBucket && !filters.difficulties.includes(state.filters.difficultyBucket)) {
    state.filters.difficultyBucket = "";
  }

  if (state.filters.tag && !filters.tags.includes(state.filters.tag)) {
    state.filters.tag = "";
  }

  if (state.filters.mark && !state.reviewMarks.includes(state.filters.mark)) {
    state.filters.mark = "";
  }
}

function readFiltersFromDom(dom, state) {
  return {
    q: dom.searchInput.value.trim(),
    decision: dom.decisionSelect.value,
    difficultyBucket: dom.difficultySelect.value,
    tag: dom.tagSelect.value,
    mark: dom.markSelect.value,
    minScore: dom.minScoreInput.value.trim(),
    sort: dom.sortSelect.value || state.defaultSort
  };
}

function cacheCandidateDetail(state, detail) {
  state.candidateDetailsById[detail.candidateId] = detail;
  return detail;
}

function setDiagnosticsState(state, payload) {
  state.diagnostics = {
    health: payload?.health || null,
    workbench: payload?.workbench || null,
    batchIntegrity: payload?.batchIntegrity || null,
    recentOperations: payload?.recentOperations || []
  };
}

async function fetchCandidateDetail(state, candidateId, force = false) {
  if (!force && state.candidateDetailsById[candidateId]) {
    return state.candidateDetailsById[candidateId];
  }

  const detailResponse = await api.getCandidateDetail(state.currentBatchId, candidateId);
  return cacheCandidateDetail(state, detailResponse.item);
}

async function loadCandidateDetail(dom, state, candidateId, force = false) {
  if (!candidateId) {
    state.selectedCandidateId = "";
    state.selectedCandidateDetail = null;
    state.isLoadingDetail = false;
    renderApp(dom, state);
    return;
  }

  state.selectedCandidateId = candidateId;
  state.isLoadingDetail = true;
  renderApp(dom, state);

  try {
    state.selectedCandidateDetail = await fetchCandidateDetail(state, candidateId, force);
    state.isLoadingDetail = false;
    clearNotice(state);
    renderApp(dom, state);
  } catch (error) {
    state.isLoadingDetail = false;
    state.selectedCandidateDetail = null;
    flashNotice(dom, state, error.message || "读取候选详情失败。", "error");
  }
}

async function refreshBatchOverview(state) {
  state.overview = await api.getBatchOverview(state.currentBatchId);
  applyOverviewFilterGuards(state);
}

async function loadDiagnostics(dom, state, options = {}) {
  const { silent = false } = options;

  if (!silent) {
    state.isLoadingDiagnostics = true;
    renderApp(dom, state);
  }

  try {
    const [health, diagnostics, batchIntegrity] = await Promise.all([
      api.getHealth(),
      api.getDiagnostics(),
      state.currentBatchId ? api.getBatchIntegrity(state.currentBatchId) : Promise.resolve(null)
    ]);

    setDiagnosticsState(state, {
      health,
      workbench: diagnostics?.workbench || null,
      batchIntegrity,
      recentOperations: diagnostics?.recentOperations || []
    });
    state.isLoadingDiagnostics = false;

    if (!silent) {
      clearNotice(state);
      renderApp(dom, state);
    }
  } catch (error) {
    state.isLoadingDiagnostics = false;
    if (!silent) {
      flashNotice(dom, state, error.message || "读取诊断信息失败。", "error");
    }
  }
}

async function toggleCompareCandidate(dom, state, candidateId) {
  if (!candidateId) {
    return;
  }

  if (state.compareCandidateIds.includes(candidateId)) {
    state.compareCandidateIds = state.compareCandidateIds.filter((item) => item !== candidateId);
    clearNotice(state);
    renderApp(dom, state);
    return;
  }

  if (state.compareCandidateIds.length >= 4) {
    flashNotice(dom, state, "候选对比页最多同时放 4 局牌。", "error");
    return;
  }

  try {
    await fetchCandidateDetail(state, candidateId);
    state.compareCandidateIds = [...state.compareCandidateIds, candidateId];
    flashNotice(dom, state, "已加入候选对比页。", "success");
  } catch (error) {
    flashNotice(dom, state, error.message || "加入候选对比页失败。", "error");
  }
}

async function refreshCandidateList(dom, state, preserveSelection = true) {
  state.isLoadingList = true;

  if (!preserveSelection) {
    state.selectedCandidateId = "";
    state.selectedCandidateDetail = null;
  }

  renderApp(dom, state);

  try {
    const page = await api.getCandidates(state.currentBatchId, state.filters);
    state.candidatePage = page;
    state.isLoadingList = false;

    const stillExists = preserveSelection
      ? page.items.some((item) => item.candidateId === state.selectedCandidateId)
      : false;
    const nextCandidateId = stillExists
      ? state.selectedCandidateId
      : (page.items[0]?.candidateId || "");

    if (!nextCandidateId) {
      state.selectedCandidateId = "";
      state.selectedCandidateDetail = null;
      clearNotice(state);
      renderApp(dom, state);
      return;
    }

    renderApp(dom, state);
    await loadCandidateDetail(dom, state, nextCandidateId);
  } catch (error) {
    state.isLoadingList = false;
    state.candidatePage = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 200
    };
    state.selectedCandidateId = "";
    state.selectedCandidateDetail = null;
    flashNotice(dom, state, error.message || "读取候选列表失败。", "error");
  }
}

function getRuntimeHelpers(dom, state) {
  return {
    clearNotice,
    flashNotice,
    fetchCandidateDetail,
    refreshBatchOverview,
    refreshCandidateList: (targetDom, targetState, preserveSelection) => refreshCandidateList(targetDom, targetState, preserveSelection)
  };
}

async function loadBatch(dom, state, batchId) {
  state.currentBatchId = batchId;
  state.overview = null;
  state.candidateDetailsById = {};
  state.compareCandidateIds = [];
  resetRuntimeSelectionState(state, batchId);
  state.diagnostics = createEmptyDiagnosticsState();
  state.selectedCandidateId = "";
  state.selectedCandidateDetail = null;
  state.isLoadingOverview = true;
  state.isLoadingList = true;
  state.isLoadingDetail = false;
  state.isLoadingRuntimeSelection = true;
  state.isLoadingExportHistory = true;
  state.isLoadingDiagnostics = true;
  state.isLoadingRuntimeDraft = false;
  state.isCommittingRuntimeDraft = false;
  renderApp(dom, state);

  try {
    const overview = await api.getBatchOverview(batchId);
    state.overview = overview;
    state.isLoadingOverview = false;
    applyOverviewFilterGuards(state);
    renderApp(dom, state);

    const runtimeHelpers = getRuntimeHelpers(dom, state);
    await loadRuntimeSelection(dom, state, runtimeHelpers);
    await loadBatchExportHistory(dom, state, runtimeHelpers, { silent: true });
    await loadDiagnostics(dom, state, { silent: true });
    await refreshCandidateList(dom, state, false);
  } catch (error) {
    state.isLoadingOverview = false;
    state.isLoadingList = false;
    state.isLoadingExportHistory = false;
    state.isLoadingDiagnostics = false;
    flashNotice(dom, state, error.message || "读取批次摘要失败。", "error");
  }
}

async function handleSaveReview(dom, state) {
  const candidateId = state.selectedCandidateId;
  if (!candidateId) {
    return;
  }

  const markSelect = dom.detailPanel.querySelector("#detail-mark-select");
  const commentInput = dom.detailPanel.querySelector("#detail-comment-input");
  const payload = {
    mark: markSelect?.value || "",
    comment: commentInput?.value || "",
    labels: []
  };

  try {
    await api.upsertReview(state.currentBatchId, candidateId, payload);
    delete state.candidateDetailsById[candidateId];
    await refreshCandidateList(dom, state, true);
    await loadRuntimeSelection(dom, state, getRuntimeHelpers(dom, state));
    flashNotice(dom, state, "人工标记已保存。", "success");
  } catch (error) {
    flashNotice(dom, state, error.message || "保存人工标记失败。", "error");
  }
}

async function handleDeleteReview(dom, state) {
  const candidateId = state.selectedCandidateId;
  if (!candidateId) {
    return;
  }

  try {
    await api.deleteReview(state.currentBatchId, candidateId);
    delete state.candidateDetailsById[candidateId];
    await refreshCandidateList(dom, state, true);
    await loadRuntimeSelection(dom, state, getRuntimeHelpers(dom, state));
    flashNotice(dom, state, "人工标记已清除。", "success");
  } catch (error) {
    flashNotice(dom, state, error.message || "清除人工标记失败。", "error");
  }
}

export function bindActions(dom, state) {
  dom.batchSelect.addEventListener("change", async () => {
    state.filters = createDefaultFilters(state.defaultSort);
    await loadBatch(dom, state, dom.batchSelect.value);
  });

  dom.applyFiltersButton.addEventListener("click", async () => {
    state.filters = readFiltersFromDom(dom, state);
    await refreshCandidateList(dom, state, true);
  });

  dom.resetFiltersButton.addEventListener("click", async () => {
    state.filters = createDefaultFilters(state.defaultSort);
    renderApp(dom, state);
    await refreshCandidateList(dom, state, true);
  });

  dom.searchInput.addEventListener("keydown", async (event) => {
    if (event.key !== "Enter") {
      return;
    }

    event.preventDefault();
    state.filters = readFiltersFromDom(dom, state);
    await refreshCandidateList(dom, state, true);
  });

  dom.candidateList.addEventListener("click", async (event) => {
    const actionButton = event.target.closest("[data-action][data-candidate-id]");
    if (!actionButton) {
      return;
    }

    const candidateId = actionButton.getAttribute("data-candidate-id");
    const action = actionButton.getAttribute("data-action");
    if (!candidateId) {
      return;
    }

    if (action === "select-candidate") {
      if (candidateId === state.selectedCandidateId) {
        return;
      }

      await loadCandidateDetail(dom, state, candidateId);
      return;
    }

    if (action === "toggle-compare") {
      await toggleCompareCandidate(dom, state, candidateId);
      return;
    }

    if (action === "toggle-runtime-selection") {
      await toggleRuntimeSelection(dom, state, getRuntimeHelpers(dom, state), candidateId);
    }
  });

  dom.comparePanel.addEventListener("click", async (event) => {
    const actionButton = event.target.closest("[data-action][data-candidate-id]");
    if (!actionButton) {
      return;
    }

    const candidateId = actionButton.getAttribute("data-candidate-id");
    const action = actionButton.getAttribute("data-action");
    if (!candidateId) {
      return;
    }

    if (action === "select-compare") {
      await loadCandidateDetail(dom, state, candidateId);
      return;
    }

    if (action === "toggle-runtime-selection") {
      await toggleRuntimeSelection(dom, state, getRuntimeHelpers(dom, state), candidateId);
      return;
    }

    if (action === "remove-compare") {
      state.compareCandidateIds = state.compareCandidateIds.filter((item) => item !== candidateId);
      clearNotice(state);
      renderApp(dom, state);
    }
  });

  dom.runtimeSelectionPanel.addEventListener("click", async (event) => {
    const actionButton = event.target.closest("[data-action]");
    if (!actionButton) {
      return;
    }

    const action = actionButton.getAttribute("data-action");
    const candidateId = actionButton.getAttribute("data-candidate-id");
    const runtimeHelpers = getRuntimeHelpers(dom, state);

    if (action === "create-runtime-draft") {
      await createRuntimeDraft(dom, state, runtimeHelpers);
      await loadDiagnostics(dom, state, { silent: true });
      return;
    }

    if (action === "preview-runtime-draft") {
      await loadRuntimeDraft(dom, state, runtimeHelpers, state.runtimeSelection.latestExportDraft?.exportId);
      return;
    }

    if (action === "preview-runtime-history-draft") {
      await loadRuntimeDraft(dom, state, runtimeHelpers, actionButton.getAttribute("data-export-id"));
      return;
    }

    if (action === "commit-runtime-draft") {
      await commitRuntimeDraft(dom, state, runtimeHelpers, state.runtimeSelection.latestExportDraft?.exportId);
      await loadDiagnostics(dom, state, { silent: true });
      return;
    }

    if (action === "select-runtime-item") {
      await loadCandidateDetail(dom, state, candidateId);
      return;
    }

    if (action === "move-runtime-up") {
      await moveRuntimeSelection(dom, state, runtimeHelpers, candidateId, "up");
      return;
    }

    if (action === "move-runtime-down") {
      await moveRuntimeSelection(dom, state, runtimeHelpers, candidateId, "down");
      return;
    }

    if (action === "remove-runtime-item") {
      await toggleRuntimeSelection(dom, state, runtimeHelpers, candidateId);
    }
  });

  dom.detailPanel.addEventListener("click", async (event) => {
    const actionButton = event.target.closest("[data-action]");
    if (!actionButton) {
      return;
    }

    const action = actionButton.getAttribute("data-action");
    const candidateId = actionButton.getAttribute("data-candidate-id");
    if (action === "toggle-compare") {
      await toggleCompareCandidate(dom, state, candidateId || state.selectedCandidateId);
      return;
    }

    if (action === "toggle-runtime-selection") {
      await toggleRuntimeSelection(dom, state, getRuntimeHelpers(dom, state), candidateId || state.selectedCandidateId);
      return;
    }

    if (action === "save-review") {
      await handleSaveReview(dom, state);
      return;
    }

    if (action === "delete-review") {
      await handleDeleteReview(dom, state);
    }
  });

  dom.diagnosticsPanel.addEventListener("click", async (event) => {
    const actionButton = event.target.closest("[data-action]");
    if (!actionButton) {
      return;
    }

    if (actionButton.getAttribute("data-action") === "refresh-diagnostics") {
      await loadDiagnostics(dom, state);
    }
  });
}

export async function initializeWorkbench(dom, state) {
  state.isBootstrapping = true;
  state.isLoadingList = true;
  state.isLoadingDiagnostics = true;
  renderApp(dom, state);

  try {
    const bootstrap = await api.getBootstrap();
    state.bootstrap = bootstrap;
    state.reviewMarks = bootstrap.workbench?.reviewMarks || [];
    state.defaultSort = bootstrap.workbench?.defaultSort || "score_desc";
    state.filters = createDefaultFilters(state.defaultSort);
    state.currentBatchId = bootstrap.defaultBatchId || bootstrap.batches?.[0]?.batchId || "";
    state.overview = bootstrap.defaultOverview || null;
    state.isBootstrapping = false;
    applyOverviewFilterGuards(state);
    renderApp(dom, state);

    if (!state.currentBatchId) {
      state.isLoadingList = false;
      state.isLoadingDiagnostics = false;
      flashNotice(dom, state, "当前没有可用的离线批次，请先生成分析产物。", "error");
      return;
    }

    const runtimeHelpers = getRuntimeHelpers(dom, state);
    await loadRuntimeSelection(dom, state, runtimeHelpers);
    await loadBatchExportHistory(dom, state, runtimeHelpers, { silent: true });
    await loadDiagnostics(dom, state, { silent: true });
    await refreshCandidateList(dom, state, false);
  } catch (error) {
    state.isBootstrapping = false;
    state.isLoadingList = false;
    state.isLoadingDiagnostics = false;
    flashNotice(dom, state, error.message || "初始化工作台失败。", "error");
  }
}
