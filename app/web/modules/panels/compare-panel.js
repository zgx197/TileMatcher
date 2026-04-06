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
import { renderLayoutPreview } from "../preview.js";

export function renderComparePanel(dom, state) {
  const comparedItems = state.compareCandidateIds
    .map((candidateId) => state.candidateDetailsById[candidateId] || null)
    .filter(Boolean);

  dom.compareCountBadge.textContent = `${comparedItems.length} / 4`;

  if (!comparedItems.length) {
    dom.comparePanel.className = "compare-panel empty-state";
    dom.comparePanel.innerHTML = "从候选列表或详情区加入 2 到 4 局牌，即可开始并排对比。";
    return;
  }

  dom.comparePanel.className = "compare-panel";
  dom.comparePanel.innerHTML = `
    <div class="compare-grid">
      ${comparedItems.map((item) => `
        <article class="compare-card ${item.candidateId === state.selectedCandidateId ? "is-selected" : ""}">
          <div class="compare-card-head">
            <div>
              <h3 class="candidate-title">${escapeHtml(item.candidateId)}</h3>
              <div class="candidate-subtitle">批次序号 ${formatCount(item.batchIndex)} · Seed ${formatCount(item.seed)}</div>
            </div>
            <span class="badge">${formatPercent(item.filterResult?.recommendationScore, 1)}</span>
          </div>

          <div class="chip-wrap">
            <span class="tone-pill" data-tone="${presentToneForDecision(item.filterResult?.decision)}">${presentDecision(item.filterResult?.decision)}</span>
            <span class="chip is-muted">${presentDifficulty(item.filterResult?.difficultyBucket)}</span>
            <span class="tone-pill" data-tone="${presentToneForMark(item.review?.mark || "")}">${presentMark(item.review?.mark || "")}</span>
          </div>

          <div class="compare-metric-grid">
            <div class="detail-stat">
              <span>随机存活率</span>
              <strong>${formatPercent(item.evaluation?.randomPlaySurvivalRate, 1)}</strong>
            </div>
            <div class="detail-stat">
              <span>死局率</span>
              <strong>${formatPercent(item.evaluation?.deadEndRate, 1)}</strong>
            </div>
            <div class="detail-stat">
              <span>分支数</span>
              <strong>${formatCount(item.evaluation?.initialBranchCount)} / ${Number(item.evaluation?.averageBranchCount || 0).toFixed(2)}</strong>
            </div>
            <div class="detail-stat">
              <span>牌数 / 层数</span>
              <strong>${formatCount(item.evaluation?.tileCount)} / ${formatCount(item.evaluation?.layerCount)}</strong>
            </div>
          </div>

          ${renderLayoutPreview(item.layout)}

          <div class="chip-wrap">
            ${(item.filterResult?.tags || []).slice(0, 4).map((tag) => `<span class="chip is-muted">${escapeHtml(tag)}</span>`).join("") || '<span class="chip is-muted">暂无标签</span>'}
          </div>

          <div class="card-actions">
            <button type="button" class="ghost-button" data-action="select-compare" data-candidate-id="${escapeHtml(item.candidateId)}">
              ${item.candidateId === state.selectedCandidateId ? "当前查看中" : "切到详情"}
            </button>
            <button type="button" class="${state.runtimeSelection.candidateIds.includes(item.candidateId) ? "secondary-button" : "ghost-button"}" data-action="toggle-runtime-selection" data-candidate-id="${escapeHtml(item.candidateId)}">
              ${state.runtimeSelection.candidateIds.includes(item.candidateId) ? "移出编排" : "加入编排"}
            </button>
            <button type="button" class="secondary-button" data-action="remove-compare" data-candidate-id="${escapeHtml(item.candidateId)}">
              移出对比
            </button>
          </div>
        </article>
      `).join("")}
    </div>
  `;
}
