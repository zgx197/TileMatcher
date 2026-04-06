const DECISION_LABELS = {
  AutoAccept: "自动通过",
  NeedsReview: "待复核",
  AutoReject: "自动淘汰"
};

const MARK_LABELS = {
  keep: "保留",
  review: "复核",
  reject: "淘汰",
  intro: "引导",
  cover: "封面"
};

const DIFFICULTY_LABELS = {
  easy_core: "轻度核心",
  normal_core: "标准核心",
  hard_core: "高压核心",
  rejected: "已淘汰"
};

function humanizeToken(value) {
  return String(value || "")
    .replace(/[_-]+/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

export function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}

export function formatCount(value) {
  const numeric = Number(value) || 0;
  return new Intl.NumberFormat("zh-CN").format(numeric);
}

export function formatDateTime(value) {
  if (!value) {
    return "未记录";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(date);
}

export function formatPercent(value, digits = 1) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) {
    return "-";
  }

  return `${(numeric * 100).toFixed(digits)}%`;
}

export function presentDecision(value) {
  return DECISION_LABELS[value] || humanizeToken(value) || "未分类";
}

export function presentMark(value) {
  if (!value) {
    return "未标记";
  }

  return MARK_LABELS[value] || humanizeToken(value);
}

export function presentDifficulty(value) {
  return DIFFICULTY_LABELS[value] || humanizeToken(value) || "未分类";
}

export function presentToneForDecision(value) {
  if (value === "AutoAccept") {
    return "accept";
  }

  if (value === "NeedsReview") {
    return "review";
  }

  if (value === "AutoReject") {
    return "reject";
  }

  return "neutral";
}

export function presentToneForMark(value) {
  if (value === "keep" || value === "intro" || value === "cover") {
    return "accept";
  }

  if (value === "review") {
    return "review";
  }

  if (value === "reject") {
    return "reject";
  }

  return "neutral";
}

export function presentBatchSubtitle(batch) {
  if (!batch) {
    return "等待批次数据";
  }

  return `${formatCount(batch.candidateCount)} 个候选 · ${formatDateTime(batch.generatedAtUtc)}`;
}
