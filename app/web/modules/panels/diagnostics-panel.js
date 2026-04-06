import { escapeHtml, formatCount, formatDateTime } from "../presenters.js";

function presentStatus(status) {
  if (status === "ok") {
    return "正常";
  }

  if (status === "warning") {
    return "警告";
  }

  if (status === "error") {
    return "异常";
  }

  return "未知";
}

function presentTone(status) {
  if (status === "ok") {
    return "accept";
  }

  if (status === "warning") {
    return "review";
  }

  if (status === "error") {
    return "reject";
  }

  return "neutral";
}

function renderCheckList(checks) {
  if (!checks?.length) {
    return '<div class="empty-state">当前没有可展示的诊断检查项。</div>';
  }

  return `
    <div class="detail-meta-grid">
      ${checks.map((check) => `
        <div class="meta-row">
          <span class="meta-key">${escapeHtml(check.label)}</span>
          <strong class="meta-value">
            <span class="tone-pill" data-tone="${presentTone(check.status)}">${presentStatus(check.status)}</span>
          </strong>
        </div>
      `).join("")}
    </div>
  `;
}

export function renderDiagnosticsPanel(dom, state) {
  const health = state.diagnostics.health;
  const integrity = state.diagnostics.batchIntegrity;
  const recentOperations = state.diagnostics.recentOperations || [];

  dom.diagnosticsStatusBadge.textContent = health ? presentStatus(health.status) : "-";
  dom.diagnosticsStatusBadge.dataset.tone = health ? presentTone(health.status) : "neutral";

  if (state.isLoadingDiagnostics) {
    dom.diagnosticsPanel.className = "detail-panel empty-state";
    dom.diagnosticsPanel.innerHTML = "正在加载诊断信息...";
    return;
  }

  if (!health && !integrity) {
    dom.diagnosticsPanel.className = "detail-panel empty-state";
    dom.diagnosticsPanel.innerHTML = "诊断信息尚未加载。";
    return;
  }

  dom.diagnosticsPanel.className = "detail-panel";
  dom.diagnosticsPanel.innerHTML = `
    <section class="detail-section">
      <div class="panel-header">
        <div>
          <h4 class="section-title">服务状态</h4>
          <p class="detail-copy">这一版先提供最小可观测性，帮助我们快速判断是服务本身、批次目录，还是导出链路出了问题。</p>
        </div>
        <button type="button" class="ghost-button" data-action="refresh-diagnostics">刷新诊断</button>
      </div>

      <div class="detail-meta-grid">
        <div class="meta-row">
          <span class="meta-key">服务健康</span>
          <strong class="meta-value">
            <span class="tone-pill" data-tone="${presentTone(health?.status)}">${presentStatus(health?.status)}</span>
          </strong>
        </div>
        <div class="meta-row">
          <span class="meta-key">批次数量</span>
          <strong class="meta-value">${formatCount(health?.batchCount)}</strong>
        </div>
        <div class="meta-row">
          <span class="meta-key">运行时长</span>
          <strong class="meta-value">${formatCount(health?.uptimeSeconds)} 秒</strong>
        </div>
        <div class="meta-row">
          <span class="meta-key">最近检查</span>
          <strong class="meta-value">${formatDateTime(health?.checkedAt)}</strong>
        </div>
      </div>

      ${renderCheckList((health?.checks || []).slice(0, 4))}
    </section>

    <section class="detail-section">
      <h4 class="section-title">当前批次完整性</h4>
      ${integrity ? `
        <div class="detail-meta-grid">
          <div class="meta-row">
            <span class="meta-key">批次</span>
            <strong class="meta-value">${escapeHtml(integrity.batchId)}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-key">完整性状态</span>
            <strong class="meta-value">
              <span class="tone-pill" data-tone="${presentTone(integrity.status)}">${presentStatus(integrity.status)}</span>
            </strong>
          </div>
          <div class="meta-row">
            <span class="meta-key">检查时间</span>
            <strong class="meta-value">${formatDateTime(integrity.checkedAt)}</strong>
          </div>
        </div>
      ` : '<div class="empty-state">当前还没有批次完整性结果。</div>'}

      ${renderCheckList((integrity?.checks || []).slice(0, 6))}
    </section>

    <section class="detail-section">
      <h4 class="section-title">最近操作</h4>
      ${recentOperations.length ? `
        <div class="runtime-draft-list">
          ${recentOperations.slice(0, 6).map((item) => `
            <article class="runtime-draft-item">
              <div class="runtime-item-main">
                <div class="runtime-order">#</div>
                <div>
                  <h3 class="candidate-title">${escapeHtml(item.eventType || "unknown_event")}</h3>
                  <div class="candidate-subtitle">${formatDateTime(item.loggedAt)}</div>
                </div>
              </div>

              <div class="chip-wrap">
                ${item.batchId ? `<span class="chip is-muted">${escapeHtml(item.batchId)}</span>` : ""}
                ${item.status ? `<span class="tone-pill" data-tone="${presentTone(item.status)}">${presentStatus(item.status)}</span>` : ""}
              </div>
            </article>
          `).join("")}
        </div>
      ` : '<div class="empty-state">当前还没有最近操作记录。</div>'}
    </section>
  `;
}
