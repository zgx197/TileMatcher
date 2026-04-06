import {
  escapeHtml,
  formatCount,
  formatDateTime,
  formatPercent,
  presentDifficulty,
  presentMark,
  presentToneForMark
} from "../presenters.js";
import { renderLayoutPreview } from "../preview.js";

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

function presentExportStatus(status, supersededBy = "") {
  if (status === "superseded" && supersededBy) {
    return `已被 ${supersededBy} 替代`;
  }

  if (status === "committed") {
    return "已提交正式目录";
  }

  if (status === "committing") {
    return "提交中";
  }

  if (status === "commit_failed") {
    return "提交失败";
  }

  return "草案";
}

function presentExportTone(status) {
  if (status === "committed") {
    return "accept";
  }

  if (status === "superseded") {
    return "review";
  }

  if (status === "commit_failed") {
    return "reject";
  }

  return "neutral";
}

function renderExportHistorySection(state, runtimeSelection) {
  const exportHistory = runtimeSelection.exportHistory || [];

  if (state.isLoadingExportHistory) {
    return `
      <section class="detail-section">
        <h4 class="section-title">最近导出历史</h4>
        <div class="empty-state runtime-preview-empty">正在加载最近导出历史...</div>
      </section>
    `;
  }

  if (!exportHistory.length) {
    return `
      <section class="detail-section">
        <h4 class="section-title">最近导出历史</h4>
        <div class="empty-state runtime-preview-empty">当前批次还没有导出记录，先从编排池生成一份草案。</div>
      </section>
    `;
  }

  return `
    <section class="detail-section">
      <div class="panel-header runtime-preview-header">
        <div>
          <h4 class="section-title">最近导出历史</h4>
          <p class="detail-copy">这一块单独拆出来，后面继续扩成提交历史页或导出对比页时不需要再挤回主渲染文件。</p>
        </div>
      </div>

      <div class="runtime-draft-list">
        ${exportHistory.slice(0, 6).map((item) => `
          <article class="runtime-draft-item ${item.exportId === state.runtimeSelection.latestExportDraft?.exportId ? "is-selected" : ""}">
            <div class="runtime-item-main">
              <div class="runtime-order">#</div>
              <div>
                <h3 class="candidate-title">${escapeHtml(item.exportId)}</h3>
                <div class="candidate-subtitle">生成于 ${formatDateTime(item.generatedAt)} · ${formatCount(item.itemCount)} 关</div>
              </div>
            </div>

            <div class="chip-wrap">
              <span class="tone-pill" data-tone="${presentExportTone(item.status)}">${escapeHtml(presentExportStatus(item.status, item.supersededBy))}</span>
              ${item.committedAt ? `<span class="chip is-warm">提交 ${formatDateTime(item.committedAt)}</span>` : ""}
            </div>

            <div class="detail-meta-grid">
              <div class="meta-row">
                <span class="meta-key">草案文件</span>
                <strong class="meta-value runtime-path">${escapeHtml(item.filePath || "-")}</strong>
              </div>
              ${item.committedRuntimeDir ? `
                <div class="meta-row">
                  <span class="meta-key">正式目录</span>
                  <strong class="meta-value runtime-path">${escapeHtml(item.committedRuntimeDir)}</strong>
                </div>
              ` : ""}
            </div>

            <div class="card-actions">
              <button type="button" class="ghost-button" data-action="preview-runtime-history-draft" data-export-id="${escapeHtml(item.exportId)}">查看这份草案</button>
            </div>
          </article>
        `).join("")}
      </div>
    </section>
  `;
}

export function renderRuntimeSelectionPanel(dom, state) {
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
        这一版先把“编排区”拆成独立模块，后面继续做诊断页和历史页时会更容易接。
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

    ${renderExportHistorySection(state, runtimeSelection)}

    ${latestDraft ? `
      <section class="detail-section">
        <h4 class="section-title">当前查看的导出草案</h4>
        <div class="detail-meta-grid">
          <div class="meta-row">
            <span class="meta-key">草案编号</span>
            <strong class="meta-value">${escapeHtml(latestDraft.exportId)}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-key">当前状态</span>
            <strong class="meta-value">${escapeHtml(presentExportStatus(latestDraft.status, latestDraft.supersededBy))}</strong>
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

        ${(latestDraft.commit || latestDraft.committedAt) ? `
          <div class="detail-meta-grid">
            <div class="meta-row">
              <span class="meta-key">最近提交</span>
              <strong class="meta-value">${formatDateTime(latestDraft.commit?.committedAt || latestDraft.committedAt)}</strong>
            </div>
            <div class="meta-row">
              <span class="meta-key">正式目录</span>
              <strong class="meta-value runtime-path">${escapeHtml(latestDraft.commit?.runtimeDir || latestDraft.committedRuntimeDir || "-")}</strong>
            </div>
            ${latestDraft.commit?.levelCatalogPath ? `
              <div class="meta-row">
                <span class="meta-key">目录索引</span>
                <strong class="meta-value runtime-path">${escapeHtml(latestDraft.commit.levelCatalogPath)}</strong>
              </div>
            ` : ""}
            ${latestDraft.commit?.backupDir ? `
              <div class="meta-row">
                <span class="meta-key">备份目录</span>
                <strong class="meta-value runtime-path">${escapeHtml(latestDraft.commit.backupDir)}</strong>
              </div>
            ` : ""}
          </div>
        ` : ""}
      </section>

      ${renderRuntimeDraftPreview(state, latestDraft)}
    ` : ""}
  `;
}
