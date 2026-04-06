import {
  escapeHtml,
  formatCount,
  formatDateTime,
  formatPercent,
  presentBatchSubtitle,
  presentDecision,
  presentDifficulty,
  presentMark,
  presentToneForDecision,
  presentToneForMark
} from "./presenters.js";
import { renderLayoutPreview } from "./preview.js";

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

function renderCandidateList(dom, state) {
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
          <button type="button" class="${state.runtimeSelection.candidateIds.includes(item.candidateId) ? "secondary-button" : "ghost-button"}" data-action="toggle-runtime-selection" data-candidate-id="${escapeHtml(item.candidateId)}">
            ${state.runtimeSelection.candidateIds.includes(item.candidateId) ? "移出编排" : "加入编排"}
          </button>
          <button type="button" class="${isCompared ? "secondary-button" : "primary-button"}" data-action="toggle-compare" data-candidate-id="${escapeHtml(item.candidateId)}">
            ${isCompared ? "移出对比" : "加入对比"}
          </button>
        </div>
      </article>
    `;
  }).join("");
}

function renderComparePanel(dom, state) {
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

function renderRuntimeDraftPreview(state, latestDraft) {
  const draftItems = latestDraft?.items || [];
  if (!draftItems.length) {
    return `
      <section class="detail-section">
        <h4 class="section-title">草案预览</h4>
        <div class="empty-state runtime-preview-empty">点击“查看草案预览”后，这里会展示本次草案的关卡顺序与候选摘要。</div>
      </section>
    `;
  }

  const previewLayoutItem = draftItems.find((item) => item.layout?.tiles?.length) || null;

  return `
    <section class="detail-section">
      <div class="panel-header runtime-preview-header">
        <div>
          <h4 class="section-title">草案预览</h4>
          <p class="detail-copy">按当前编排顺序预览即将写入正式目录的关卡草案。</p>
        </div>
      </div>

      <div class="runtime-draft-list">
        ${draftItems.map((item) => `
          <article class="runtime-draft-item ${item.candidateId === state.selectedCandidateId ? "is-selected" : ""}">
            <div class="runtime-item-main">
              <div class="runtime-order">#${formatCount(item.levelNumber)}</div>
              <div>
                <h3 class="candidate-title">${escapeHtml(item.candidateId)}</h3>
                <div class="candidate-subtitle">${presentDifficulty(item.difficultyBucket)} · 推荐分 ${formatPercent(item.recommendationScore, 1)}</div>
              </div>
            </div>

            <div class="runtime-metrics">
              <div class="detail-stat">
                <span>随机存活率</span>
                <strong>${formatPercent(item.randomPlaySurvivalRate, 1)}</strong>
              </div>
              <div class="detail-stat">
                <span>死局率</span>
                <strong>${formatPercent(item.deadEndRate, 1)}</strong>
              </div>
              <div class="detail-stat">
                <span>牌数 / 层数</span>
                <strong>${formatCount(item.tileCount)} / ${formatCount(item.layerCount)}</strong>
              </div>
            </div>

            <div class="chip-wrap">
              <span class="tone-pill" data-tone="${presentToneForMark(item.review?.mark || "")}">${presentMark(item.review?.mark || "")}</span>
              ${item.hiddenFaceCount ? `<span class="chip is-warm">遮面 ${formatCount(item.hiddenFaceCount)}</span>` : ""}
            </div>

            <div class="card-actions">
              <button type="button" class="ghost-button" data-action="select-runtime-item" data-candidate-id="${escapeHtml(item.candidateId)}">切到详情</button>
            </div>
          </article>
        `).join("")}
      </div>

      ${previewLayoutItem ? `
        <div class="runtime-preview-layout">
          <div class="panel-header runtime-preview-header">
            <div>
              <h4 class="section-title">首关结构预览</h4>
              <p class="detail-copy">${escapeHtml(previewLayoutItem.candidateId)} 将作为第 ${formatCount(previewLayoutItem.levelNumber)} 关写入正式目录。</p>
            </div>
          </div>
          ${renderLayoutPreview(previewLayoutItem.layout)}
        </div>
      ` : ""}
    </section>
  `;
}

function renderRuntimeSelectionPanel(dom, state) {
  const runtimeSelection = state.runtimeSelection || { items: [] };
  const items = runtimeSelection.items || [];
  dom.runtimeSelectionCountBadge.textContent = `${formatCount(items.length)} 关`;

  if (state.isLoadingRuntimeSelection) {
    dom.runtimeSelectionPanel.className = "runtime-selection-panel empty-state";
    dom.runtimeSelectionPanel.innerHTML = "正在加载正式关卡编排池...";
    return;
  }

  if (!items.length) {
    dom.runtimeSelectionPanel.className = "runtime-selection-panel empty-state";
    dom.runtimeSelectionPanel.innerHTML = "把候选加入正式关卡池后，就可以在这里调整顺序并生成导出草案。";
    return;
  }

  const latestDraft = runtimeSelection.latestExportDraft;
  dom.runtimeSelectionPanel.className = "runtime-selection-panel";
  dom.runtimeSelectionPanel.innerHTML = `
    <div class="runtime-toolbar">
      <div class="detail-copy">
        当前编排池最后更新于 ${formatDateTime(runtimeSelection.updatedAt)}。
        这一步只生成本地导出草案，不会直接覆写正式运行时目录。
      </div>
      <div class="card-actions">
        <button type="button" class="ghost-button" data-action="preview-runtime-draft" ${latestDraft?.exportId ? "" : "disabled"}>
          ${state.isLoadingRuntimeDraft ? "正在读取草案" : "查看草案预览"}
        </button>
        <button type="button" class="primary-button" data-action="create-runtime-draft" ${state.isExportingRuntimeSelection ? "disabled" : ""}>
          ${state.isExportingRuntimeSelection ? "正在生成草案" : "生成导出草案"}
        </button>
        <button type="button" class="secondary-button" data-action="commit-runtime-draft" ${latestDraft?.exportId && !state.isCommittingRuntimeDraft ? "" : "disabled"}>
          ${state.isCommittingRuntimeDraft ? "正在提交正式目录" : "提交到正式目录"}
        </button>
      </div>
    </div>

    <div class="runtime-list">
      ${items.map((item, index) => `
        <article class="runtime-item">
          <div class="runtime-item-main">
            <div class="runtime-order">#${formatCount(item.levelNumber)}</div>
            <div>
              <h3 class="candidate-title">${escapeHtml(item.candidateId)}</h3>
              <div class="candidate-subtitle">批次序号 ${formatCount(item.batchIndex)} · Seed ${formatCount(item.seed)}</div>
            </div>
          </div>

          <div class="chip-wrap">
            <span class="badge">${formatPercent(item.recommendationScore, 1)}</span>
            <span class="chip is-muted">${presentDifficulty(item.difficultyBucket)}</span>
            <span class="tone-pill" data-tone="${presentToneForMark(item.review?.mark || "")}">${presentMark(item.review?.mark || "")}</span>
          </div>

          <div class="runtime-metrics">
            <div class="detail-stat">
              <span>随机存活率</span>
              <strong>${formatPercent(item.randomPlaySurvivalRate, 1)}</strong>
            </div>
            <div class="detail-stat">
              <span>死局率</span>
              <strong>${formatPercent(item.deadEndRate, 1)}</strong>
            </div>
            <div class="detail-stat">
              <span>牌数 / 层数</span>
              <strong>${formatCount(item.tileCount)} / ${formatCount(item.layerCount)}</strong>
            </div>
          </div>

          <div class="card-actions">
            <button type="button" class="ghost-button" data-action="select-runtime-item" data-candidate-id="${escapeHtml(item.candidateId)}">切到详情</button>
            <button type="button" class="ghost-button" data-action="move-runtime-up" data-candidate-id="${escapeHtml(item.candidateId)}" ${index === 0 ? "disabled" : ""}>上移</button>
            <button type="button" class="ghost-button" data-action="move-runtime-down" data-candidate-id="${escapeHtml(item.candidateId)}" ${index === items.length - 1 ? "disabled" : ""}>下移</button>
            <button type="button" class="secondary-button" data-action="remove-runtime-item" data-candidate-id="${escapeHtml(item.candidateId)}">移出编排</button>
          </div>
        </article>
      `).join("")}
    </div>

    ${latestDraft ? `
      <section class="detail-section">
        <h4 class="section-title">最近一次导出草案</h4>
        <div class="detail-meta-grid">
          <div class="meta-row">
            <span class="meta-key">草案编号</span>
            <strong class="meta-value">${escapeHtml(latestDraft.exportId)}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-key">导出条目</span>
            <strong class="meta-value">${formatCount(latestDraft.itemCount)} 关</strong>
          </div>
          <div class="meta-row">
            <span class="meta-key">生成时间</span>
            <strong class="meta-value">${formatDateTime(latestDraft.generatedAt)}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-key">草案文件</span>
            <strong class="meta-value runtime-path">${escapeHtml(latestDraft.filePath)}</strong>
          </div>
        </div>

        ${latestDraft.commit ? `
          <div class="detail-meta-grid">
            <div class="meta-row">
              <span class="meta-key">最近提交</span>
              <strong class="meta-value">${formatDateTime(latestDraft.commit.committedAt)}</strong>
            </div>
            <div class="meta-row">
              <span class="meta-key">正式目录</span>
              <strong class="meta-value runtime-path">${escapeHtml(latestDraft.commit.runtimeDir)}</strong>
            </div>
            <div class="meta-row">
              <span class="meta-key">目录索引</span>
              <strong class="meta-value runtime-path">${escapeHtml(latestDraft.commit.levelCatalogPath)}</strong>
            </div>
            <div class="meta-row">
              <span class="meta-key">备份目录</span>
              <strong class="meta-value runtime-path">${escapeHtml(latestDraft.commit.backupDir)}</strong>
            </div>
          </div>
        ` : ""}
      </section>

      ${renderRuntimeDraftPreview(state, latestDraft)}
    ` : ""}
  `;
}

function renderDetailPanel(dom, state) {
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
        <strong>${presentDecision(detail.filterResult?.decision)}</strong>，
        推荐分 ${formatPercent(detail.filterResult?.recommendationScore, 1)}。
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
        预览采用轻量 SVG 投影，只用于浏览结构层次、遮面情况和总体轮廓，
        目前不承担玩法级别的精确碰撞与可移动分析。
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

export function renderApp(dom, state) {
  renderFlashBanner(dom, state);
  renderHeader(dom, state);
  renderBatchControls(dom, state);
  renderOverview(dom, state);
  renderCandidateList(dom, state);
  renderComparePanel(dom, state);
  renderRuntimeSelectionPanel(dom, state);
  renderDetailPanel(dom, state);
}
