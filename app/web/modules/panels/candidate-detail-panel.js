import {
  escapeHtml,
  formatCount,
  formatDateTime,
  formatPercent,
  presentDecision,
  presentDifficulty,
  presentMark,
  presentToneForDecision
} from "../presenters.js";
import { renderLayoutPreview } from "../preview.js";

export function renderDetailPanel(dom, state) {
  if (state.isLoadingDetail) {
    dom.detailPanel.className = "detail-panel empty-state";
    dom.detailPanel.innerHTML = "正在加载候选详情...";
    return;
  }

  const detail = state.selectedCandidateDetail;
  if (!detail) {
    dom.detailPanel.className = "detail-panel empty-state";
    dom.detailPanel.innerHTML = "先从候选浏览器中选择一局牌，再查看结构、指标和人工标记。";
    return;
  }

  const review = detail.review || {};
  const tags = detail.filterResult?.tags || [];
  const rejectReasons = detail.filterResult?.rejectReasons || [];

  dom.detailPanel.className = "detail-panel";
  dom.detailPanel.innerHTML = `
    <section class="detail-top">
      <div class="panel-header">
        <div>
          <p class="eyebrow">Candidate</p>
          <h3 class="detail-title">${escapeHtml(detail.candidateId)}</h3>
        </div>
        <span class="badge">${presentDifficulty(detail.filterResult?.difficultyBucket)}</span>
      </div>
      <p class="detail-copy">
        当前候选来自批次序号 ${formatCount(detail.batchIndex)}，自动决策为
        <strong>${presentDecision(detail.filterResult?.decision)}</strong>，推荐分 ${formatPercent(detail.filterResult?.recommendationScore, 1)}。
      </p>
    </section>

    <section class="detail-section">
      <h4 class="section-title">关键元数据</h4>
      <div class="detail-meta-grid">
        <div class="meta-row">
          <span class="meta-key">Seed</span>
          <strong class="meta-value">${formatCount(detail.seed)}</strong>
        </div>
        <div class="meta-row">
          <span class="meta-key">正式关卡</span>
          <strong class="meta-value">${detail.runtimeLevelNumber ? `第 ${formatCount(detail.runtimeLevelNumber)} 关` : "尚未编入"}</strong>
        </div>
        <div class="meta-row">
          <span class="meta-key">人工标记</span>
          <strong class="meta-value">${presentMark(review.mark)}</strong>
        </div>
        <div class="meta-row">
          <span class="meta-key">最近更新</span>
          <strong class="meta-value">${formatDateTime(review.updatedAt)}</strong>
        </div>
      </div>
    </section>

    <section class="detail-section">
      <h4 class="section-title">评估指标</h4>
      <div class="metric-grid">
        <div class="detail-stat">
          <span>初始分支数</span>
          <strong>${formatCount(detail.evaluation?.initialBranchCount)}</strong>
        </div>
        <div class="detail-stat">
          <span>平均分支数</span>
          <strong>${Number(detail.evaluation?.averageBranchCount || 0).toFixed(2)}</strong>
        </div>
        <div class="detail-stat">
          <span>解数量估计</span>
          <strong>${formatCount(detail.evaluation?.solutionCountEstimate)}</strong>
        </div>
        <div class="detail-stat">
          <span>死局率</span>
          <strong>${formatPercent(detail.evaluation?.deadEndRate, 1)}</strong>
        </div>
        <div class="detail-stat">
          <span>随机存活率</span>
          <strong>${formatPercent(detail.evaluation?.randomPlaySurvivalRate, 1)}</strong>
        </div>
        <div class="detail-stat">
          <span>牌数 / 层数</span>
          <strong>${formatCount(detail.evaluation?.tileCount)} / ${formatCount(detail.evaluation?.layerCount)}</strong>
        </div>
      </div>
    </section>

    <section class="preview-card">
      <h4 class="section-title">牌桌预览</h4>
      ${renderLayoutPreview(detail.layout)}
      <p class="detail-note">
        预览采用轻量 SVG 投影，只用于浏览结构层次、遮面情况和总体轮廓，目前不承担玩法级别的精确碰撞与可移动分析。
      </p>
    </section>

    <section class="detail-section">
      <h4 class="section-title">标签与自动判断</h4>
      <div class="chip-wrap">
        <span class="tone-pill" data-tone="${presentToneForDecision(detail.filterResult?.decision)}">${presentDecision(detail.filterResult?.decision)}</span>
        ${tags.length ? tags.map((tag) => `<span class="chip">${escapeHtml(tag)}</span>`).join("") : '<span class="chip is-muted">暂无标签</span>'}
      </div>
      ${rejectReasons.length ? `
        <div>
          <p class="mini-title">自动拒绝原因</p>
          <div class="chip-wrap">
            ${rejectReasons.map((reason) => `<span class="chip is-warm">${escapeHtml(reason)}</span>`).join("")}
          </div>
        </div>
      ` : ""}
    </section>

    <section class="review-card">
      <h4 class="section-title">人工标记</h4>
      <label class="field">
        <span>标记类型</span>
        <select id="detail-mark-select">
          <option value="">未标记</option>
          ${state.reviewMarks.map((mark) => `
            <option value="${escapeHtml(mark)}" ${review.mark === mark ? "selected" : ""}>${presentMark(mark)}</option>
          `).join("")}
        </select>
      </label>
      <label class="field">
        <span>备注</span>
        <textarea id="detail-comment-input" placeholder="记录这局牌为什么值得保留、复核或淘汰。">${escapeHtml(review.comment || "")}</textarea>
      </label>
      <div class="detail-actions">
        <button type="button" class="${state.compareCandidateIds.includes(detail.candidateId) ? "secondary-button" : "ghost-button"}" data-action="toggle-compare" data-candidate-id="${escapeHtml(detail.candidateId)}">
          ${state.compareCandidateIds.includes(detail.candidateId) ? "移出对比" : "加入对比"}
        </button>
        <button type="button" class="${state.runtimeSelection.candidateIds.includes(detail.candidateId) ? "secondary-button" : "ghost-button"}" data-action="toggle-runtime-selection" data-candidate-id="${escapeHtml(detail.candidateId)}">
          ${state.runtimeSelection.candidateIds.includes(detail.candidateId) ? "移出编排" : "加入编排"}
        </button>
        <button type="button" class="primary-button" data-action="save-review">保存标记</button>
        <button type="button" class="secondary-button" data-action="delete-review">清除标记</button>
      </div>
    </section>
  `;
}
