import * as api from "./api.js";
import { renderApp } from "./renderers.js";
import { createEmptyRuntimeSelectionState } from "./state.js";

export function setRuntimeSelection(state, payload) {
  state.runtimeSelection = {
    batchId: payload?.batchId || state.currentBatchId,
    candidateIds: payload?.candidateIds || [],
    items: payload?.items || [],
    updatedAt: payload?.updatedAt || "",
    exportHistory: state.runtimeSelection?.exportHistory || [],
    latestExportDraft: state.runtimeSelection?.latestExportDraft || null
  };
}

export function setExportHistory(state, payload) {
  const exportHistory = payload?.items || [];
  const latestDraft = state.runtimeSelection?.latestExportDraft || null;
  const latestSummary = latestDraft?.exportId
    ? exportHistory.find((item) => item.exportId === latestDraft.exportId) || null
    : null;
  const fallbackDraft = latestSummary || latestDraft || exportHistory[0] || null;

  state.runtimeSelection = {
    ...state.runtimeSelection,
    exportHistory,
    latestExportDraft: latestSummary && latestDraft
      ? {
        ...latestDraft,
        ...latestSummary
      }
      : fallbackDraft
  };
}

export function setLatestRuntimeDraft(state, payload) {
  if (!payload) {
    state.runtimeSelection = {
      ...state.runtimeSelection,
      latestExportDraft: null
    };
    return;
  }

  state.runtimeSelection = {
    ...state.runtimeSelection,
    latestExportDraft: payload
  };
}

export function resetRuntimeSelectionState(state, batchId = "") {
  state.runtimeSelection = createEmptyRuntimeSelectionState(batchId);
}

export async function loadBatchExportHistory(dom, state, helpers, options = {}) {
  const { silent = false } = options;
  if (!silent) {
    state.isLoadingExportHistory = true;
    renderApp(dom, state);
  }

  try {
    const response = await api.getBatchExports(state.currentBatchId);
    setExportHistory(state, response.item);
    state.isLoadingExportHistory = false;

    if (!silent) {
      helpers.clearNotice(state);
      renderApp(dom, state);
    }
  } catch (error) {
    state.isLoadingExportHistory = false;
    if (!silent) {
      helpers.flashNotice(dom, state, error.message || "读取导出历史失败。", "error");
    }
  }
}

export async function loadRuntimeSelection(dom, state, helpers) {
  state.isLoadingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const response = await api.getRuntimeSelection(state.currentBatchId);
    setRuntimeSelection(state, response.item);
    state.isLoadingRuntimeSelection = false;
    helpers.clearNotice(state);
    renderApp(dom, state);
  } catch (error) {
    state.isLoadingRuntimeSelection = false;
    helpers.flashNotice(dom, state, error.message || "读取正式关卡编排池失败。", "error");
  }
}

export async function loadRuntimeDraft(dom, state, helpers, exportId, options = {}) {
  if (!exportId) {
    return null;
  }

  const { silent = false } = options;
  if (!silent) {
    state.isLoadingRuntimeDraft = true;
    renderApp(dom, state);
  }

  try {
    const response = await api.getExportDraft(exportId);
    setLatestRuntimeDraft(state, response.item);

    if (!silent) {
      state.isLoadingRuntimeDraft = false;
      helpers.clearNotice(state);
      renderApp(dom, state);
    }

    return response.item;
  } catch (error) {
    if (!silent) {
      state.isLoadingRuntimeDraft = false;
      helpers.flashNotice(dom, state, error.message || "读取导出草案失败。", "error");
    }
    return null;
  }
}

export async function saveRuntimeSelection(dom, state, helpers, candidateIds) {
  state.isLoadingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const response = await api.saveRuntimeSelection(state.currentBatchId, candidateIds);
    setRuntimeSelection(state, response.item);
    state.isLoadingRuntimeSelection = false;
    helpers.clearNotice(state);
    renderApp(dom, state);
    return response.item;
  } catch (error) {
    state.isLoadingRuntimeSelection = false;
    helpers.flashNotice(dom, state, error.message || "保存正式关卡编排池失败。", "error");
    return null;
  }
}

export async function toggleRuntimeSelection(dom, state, helpers, candidateId) {
  if (!candidateId) {
    return;
  }

  const currentIds = state.runtimeSelection.candidateIds || [];
  if (currentIds.includes(candidateId)) {
    const nextIds = currentIds.filter((item) => item !== candidateId);
    const saved = await saveRuntimeSelection(dom, state, helpers, nextIds);
    if (saved) {
      helpers.flashNotice(dom, state, "已从正式关卡编排池移出。", "success");
    }
    return;
  }

  try {
    await helpers.fetchCandidateDetail(state, candidateId);
    const saved = await saveRuntimeSelection(dom, state, helpers, [...currentIds, candidateId]);
    if (saved) {
      helpers.flashNotice(dom, state, "已加入正式关卡编排池。", "success");
    }
  } catch (error) {
    helpers.flashNotice(dom, state, error.message || "加入正式关卡编排池失败。", "error");
  }
}

export async function moveRuntimeSelection(dom, state, helpers, candidateId, direction) {
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
  const saved = await saveRuntimeSelection(dom, state, helpers, currentIds);
  if (saved) {
    helpers.flashNotice(dom, state, "正式关卡顺序已更新。", "success");
  }
}

export async function createRuntimeDraft(dom, state, helpers) {
  if (!state.runtimeSelection.candidateIds.length) {
    helpers.flashNotice(dom, state, "正式关卡编排池还是空的，先加入候选再生成草案。", "error");
    return;
  }

  state.isExportingRuntimeSelection = true;
  renderApp(dom, state);

  try {
    const response = await api.createRuntimeSelectionDraft(state.currentBatchId);
    await loadRuntimeDraft(dom, state, helpers, response.item.exportId, { silent: true });
    await loadBatchExportHistory(dom, state, helpers, { silent: true });
    state.isExportingRuntimeSelection = false;
    state.isLoadingRuntimeDraft = false;
    helpers.flashNotice(dom, state, "导出草案已生成。", "success");
  } catch (error) {
    state.isExportingRuntimeSelection = false;
    state.isLoadingRuntimeDraft = false;
    helpers.flashNotice(dom, state, error.message || "生成导出草案失败。", "error");
  }
}

export async function commitRuntimeDraft(dom, state, helpers, exportId) {
  if (!exportId) {
    helpers.flashNotice(dom, state, "还没有可提交的导出草案。", "error");
    return;
  }

  state.isCommittingRuntimeDraft = true;
  renderApp(dom, state);

  try {
    const response = await api.commitExportDraft(exportId);
    const latestDraft = state.runtimeSelection.latestExportDraft || null;
    if (latestDraft) {
      setLatestRuntimeDraft(state, {
        ...latestDraft,
        commit: response.item
      });
    }

    state.candidateDetailsById = {};
    await helpers.refreshBatchOverview(state);
    await loadRuntimeSelection(dom, state, helpers);
    await helpers.refreshCandidateList(dom, state, true);
    await loadRuntimeDraft(dom, state, helpers, exportId, { silent: true });
    await loadBatchExportHistory(dom, state, helpers, { silent: true });
    state.isCommittingRuntimeDraft = false;
    state.isLoadingRuntimeDraft = false;
    helpers.flashNotice(dom, state, "导出草案已提交到正式目录。", "success");
  } catch (error) {
    state.isCommittingRuntimeDraft = false;
    helpers.flashNotice(dom, state, error.message || "提交导出草案失败。", "error");
  }
}
