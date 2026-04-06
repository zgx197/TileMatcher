import * as api from "./api.js";
import { renderApp } from "./renderers.js";
import { createDefaultFilters } from "./state.js";

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

function setRuntimeSelection(state, payload) {
  state.runtimeSelection = {
    batchId: payload?.batchId || state.currentBatchId,
    candidateIds: payload?.candidateIds || [],
    items: payload?.items || [],
    updatedAt: payload?.updatedAt || "",
    latestExportDraft: state.runtimeSelection?.latestExportDraft || null
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

async function loadRuntimeSelection(dom, state) {
  state.isLoadingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const response = await api.getRuntimeSelection(state.currentBatchId);
    setRuntimeSelection(state, response.item);
    state.isLoadingRuntimeSelection = false;
    clearNotice(state);
    renderApp(dom, state);
  } catch (error) {
    state.isLoadingRuntimeSelection = false;
    flashNotice(dom, state, error.message || "读取正式关卡编排池失败。", "error");
  }
}

async function saveRuntimeSelection(dom, state, candidateIds) {
  state.isLoadingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const response = await api.saveRuntimeSelection(state.currentBatchId, candidateIds);
    setRuntimeSelection(state, response.item);
    state.isLoadingRuntimeSelection = false;
    clearNotice(state);
    renderApp(dom, state);
    return response.item;
  } catch (error) {
    state.isLoadingRuntimeSelection = false;
    flashNotice(dom, state, error.message || "保存正式关卡编排池失败。", "error");
    return null;
  }
}

async function toggleRuntimeSelection(dom, state, candidateId) {
  if (!candidateId) {
    return;
  }

  const currentIds = state.runtimeSelection.candidateIds || [];
  if (currentIds.includes(candidateId)) {
    const nextIds = currentIds.filter((item) => item !== candidateId);
    const saved = await saveRuntimeSelection(dom, state, nextIds);
    if (saved) {
      flashNotice(dom, state, "已从正式关卡编排池移出。", "success");
    }
    return;
  }

  try {
    await fetchCandidateDetail(state, candidateId);
    const saved = await saveRuntimeSelection(dom, state, [...currentIds, candidateId]);
    if (saved) {
      flashNotice(dom, state, "已加入正式关卡编排池。", "success");
    }
  } catch (error) {
    flashNotice(dom, state, error.message || "加入正式关卡编排池失败。", "error");
  }
}

async function moveRuntimeSelection(dom, state, candidateId, direction) {
  const currentIds = [...(state.runtimeSelection.candidateIds || [])];
  const currentIndex = currentIds.indexOf(candidateId);
  if (currentIndex < 0) {
    return;
  }

  const targetIndex = direction === "up" ? currentIndex - 1 : currentIndex + 1;
  if (targetIndex < 0 || targetIndex >= currentIds.length) {
    return;
  }

  [currentIds[currentIndex], currentIds[targetIndex]] = [currentIds[targetIndex], currentIds[currentIndex]];
  const saved = await saveRuntimeSelection(dom, state, currentIds);
  if (saved) {
    flashNotice(dom, state, "正式关卡顺序已更新。", "success");
  }
}

async function createRuntimeDraft(dom, state) {
  if (!state.runtimeSelection.candidateIds.length) {
    flashNotice(dom, state, "正式关卡编排池还是空的，先加入候选再生成草案。", "error");
    return;
  }

  state.isExportingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const response = await api.createRuntimeSelectionDraft(state.currentBatchId);
    state.runtimeSelection = {
      ...state.runtimeSelection,
      latestExportDraft: response.item
    };
    state.isExportingRuntimeSelection = false;
    flashNotice(dom, state, "导出草案已生成。", "success");
  } catch (error) {
    state.isExportingRuntimeSelection = false;
    flashNotice(dom, state, error.message || "生成导出草案失败。", "error");
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

async function loadBatch(dom, state, batchId) {
  state.currentBatchId = batchId;
  state.overview = null;
  state.candidateDetailsById = {};
  state.compareCandidateIds = [];
  state.runtimeSelection = {
    batchId,
    candidateIds: [],
    items: [],
    updatedAt: "",
    latestExportDraft: null
  };
  state.selectedCandidateId = "";
  state.selectedCandidateDetail = null;
  state.isLoadingOverview = true;
  state.isLoadingList = true;
  state.isLoadingDetail = false;
  state.isLoadingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const overview = await api.getBatchOverview(batchId);
    state.overview = overview;
    state.isLoadingOverview = false;
    applyOverviewFilterGuards(state);
    renderApp(dom, state);
    await loadRuntimeSelection(dom, state);
    await refreshCandidateList(dom, state, false);
  } catch (error) {
    state.isLoadingOverview = false;
    state.isLoadingList = false;
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
    await loadRuntimeSelection(dom, state);
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
    await loadRuntimeSelection(dom, state);
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
      await toggleRuntimeSelection(dom, state, candidateId);
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
      await toggleRuntimeSelection(dom, state, candidateId);
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

    if (action === "create-runtime-draft") {
      await createRuntimeDraft(dom, state);
      return;
    }

    if (action === "select-runtime-item") {
      await loadCandidateDetail(dom, state, candidateId);
      return;
    }

    if (action === "move-runtime-up") {
      await moveRuntimeSelection(dom, state, candidateId, "up");
      return;
    }

    if (action === "move-runtime-down") {
      await moveRuntimeSelection(dom, state, candidateId, "down");
      return;
    }

    if (action === "remove-runtime-item") {
      await toggleRuntimeSelection(dom, state, candidateId);
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
      await toggleRuntimeSelection(dom, state, candidateId || state.selectedCandidateId);
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
}

export async function initializeWorkbench(dom, state) {
  state.isBootstrapping = true;
  state.isLoadingList = true;
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
      flashNotice(dom, state, "当前没有可用的离线批次，请先生成分析产物。", "error");
      return;
    }

    await loadRuntimeSelection(dom, state);
    await refreshCandidateList(dom, state, false);
  } catch (error) {
    state.isBootstrapping = false;
    state.isLoadingList = false;
    flashNotice(dom, state, error.message || "初始化工作台失败。", "error");
  }
}
