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

async function loadCandidateDetail(dom, state, candidateId) {
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
    const detailResponse = await api.getCandidateDetail(state.currentBatchId, candidateId);
    state.selectedCandidateDetail = detailResponse.item;
    state.isLoadingDetail = false;
    clearNotice(state);
    renderApp(dom, state);
  } catch (error) {
    state.isLoadingDetail = false;
    state.selectedCandidateDetail = null;
    flashNotice(dom, state, error.message || "读取候选详情失败。", "error");
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
  state.selectedCandidateId = "";
  state.selectedCandidateDetail = null;
  state.isLoadingOverview = true;
  state.isLoadingList = true;
  state.isLoadingDetail = false;
  renderApp(dom, state);

  try {
    const overview = await api.getBatchOverview(batchId);
    state.overview = overview;
    state.isLoadingOverview = false;
    applyOverviewFilterGuards(state);
    renderApp(dom, state);
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
    await refreshCandidateList(dom, state, true);
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
    await refreshCandidateList(dom, state, true);
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
    const button = event.target.closest("[data-candidate-id]");
    if (!button) {
      return;
    }

    const candidateId = button.getAttribute("data-candidate-id");
    if (!candidateId || candidateId === state.selectedCandidateId) {
      return;
    }

    await loadCandidateDetail(dom, state, candidateId);
  });

  dom.detailPanel.addEventListener("click", async (event) => {
    const actionButton = event.target.closest("[data-action]");
    if (!actionButton) {
      return;
    }

    const action = actionButton.getAttribute("data-action");
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

    await refreshCandidateList(dom, state, false);
  } catch (error) {
    state.isBootstrapping = false;
    state.isLoadingList = false;
    flashNotice(dom, state, error.message || "初始化工作台失败。", "error");
  }
}
