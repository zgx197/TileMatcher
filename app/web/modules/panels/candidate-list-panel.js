import {
  escapeHtml,
  formatCount,
  formatPercent,
  presentDecision,
  presentDifficulty,
  presentMark,
  presentToneForDecision,
  presentToneForMark
} from "../presenters.js";

export function renderCandidateList(dom, state) {
  const totalCount = Number(state.candidatePage?.totalCount) || 0;
  dom.candidateCountBadge.textContent = `${formatCount(totalCount)} 条`;

  if (state.isLoadingList) {
    dom.candidateList.className = "candidate-list empty-state";
    dom.candidateList.innerHTML = "正在加载候选列表...";
    return;
  }

  const items = state.candidatePage?.items || [];
  if (!items.length) {
    dom.candidateList.className = "candidate-list empty-state";
    dom.candidateList.innerHTML = "当前筛选条件下没有候选结果。";
    return;
  }

  dom.candidateList.className = "candidate-list";
  dom.candidateList.innerHTML = items.map((item) => {
    const isSelected = item.candidateId === state.selectedCandidateId;
    const reviewMark = item.review?.mark || "";
    const tags = item.tags?.slice(0, 5) || [];
    const isCompared = state.compareCandidateIds.includes(item.candidateId);
    const isInRuntimeSelection = state.runtimeSelection.candidateIds.includes(item.candidateId);

    return `
      <article class="candidate-card ${isSelected ? "is-selected" : ""}" data-candidate-id="${escapeHtml(item.candidateId)}">
        <div class="candidate-head">
          <div>
            <h3 class="candidate-title">${escapeHtml(item.candidateId)}</h3>
            <div class="candidate-subtitle">批次序号 ${formatCount(item.batchIndex)} · Seed ${formatCount(item.seed)}</div>
          </div>
          <span class="badge">评分 ${formatPercent(item.recommendationScore, 1)}</span>
        </div>

        <div class="candidate-metrics">
          <div class="metric-card">
            <span class="metric-label">随机存活率</span>
            <strong class="metric-value">${formatPercent(item.randomPlaySurvivalRate, 1)}</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">死局率</span>
            <strong class="metric-value">${formatPercent(item.deadEndRate, 1)}</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">牌数 / 层数</span>
            <strong class="metric-value">${formatCount(item.tileCount)} / ${formatCount(item.layerCount)}</strong>
          </div>
        </div>

        <div class="candidate-footer">
          <span class="tone-pill" data-tone="${presentToneForDecision(item.decision)}">${presentDecision(item.decision)}</span>
          <span class="tone-pill" data-tone="${presentToneForMark(reviewMark)}">${presentMark(reviewMark)}</span>
          <span class="chip is-muted">${presentDifficulty(item.difficultyBucket)}</span>
          ${item.runtimeLevelNumber ? `<span class="chip is-warm">正式关卡 ${formatCount(item.runtimeLevelNumber)}</span>` : ""}
          ${item.hiddenFaceCount ? `<span class="chip">遮面 ${formatCount(item.hiddenFaceCount)}</span>` : ""}
          ${tags.map((tag) => `<span class="chip is-muted">${escapeHtml(tag)}</span>`).join("")}
        </div>

        <div class="card-actions">
          <button type="button" class="ghost-button" data-action="select-candidate" data-candidate-id="${escapeHtml(item.candidateId)}">
            ${isSelected ? "当前查看中" : "查看详情"}
          </button>
          <button type="button" class="${isInRuntimeSelection ? "secondary-button" : "ghost-button"}" data-action="toggle-runtime-selection" data-candidate-id="${escapeHtml(item.candidateId)}">
            ${isInRuntimeSelection ? "移出编排" : "加入编排"}
          </button>
          <button type="button" class="${isCompared ? "secondary-button" : "primary-button"}" data-action="toggle-compare" data-candidate-id="${escapeHtml(item.candidateId)}">
            ${isCompared ? "移出对比" : "加入对比"}
          </button>
        </div>
      </article>
    `;
  }).join("");
}
