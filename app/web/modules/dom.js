function requireElement(id) {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing required DOM element: #${id}`);
  }

  return element;
}

function ensureFlashBanner() {
  const existing = document.getElementById("flash-banner");
  if (existing) {
    return existing;
  }

  const pageShell = document.querySelector(".page-shell");
  if (!pageShell) {
    throw new Error("Missing required DOM element: .page-shell");
  }

  const banner = document.createElement("div");
  banner.id = "flash-banner";
  banner.className = "flash-banner";
  pageShell.insertBefore(banner, pageShell.firstChild);
  return banner;
}

export function getDomRefs() {
  return {
    flashBanner: ensureFlashBanner(),
    heroBatchName: requireElement("hero-batch-name"),
    heroGeneratedAt: requireElement("hero-generated-at"),
    batchSelect: requireElement("batch-select"),
    searchInput: requireElement("search-input"),
    decisionSelect: requireElement("decision-select"),
    difficultySelect: requireElement("difficulty-select"),
    tagSelect: requireElement("tag-select"),
    markSelect: requireElement("mark-select"),
    minScoreInput: requireElement("min-score-input"),
    sortSelect: requireElement("sort-select"),
    applyFiltersButton: requireElement("apply-filters-button"),
    resetFiltersButton: requireElement("reset-filters-button"),
    candidateCountBadge: requireElement("candidate-count-badge"),
    candidateList: requireElement("candidate-list"),
    detailPanel: requireElement("detail-panel"),
    summaryCandidateCount: requireElement("summary-candidate-count"),
    summaryAcceptedCount: requireElement("summary-accepted-count"),
    summaryReviewCount: requireElement("summary-review-count"),
    summaryRuntimeCount: requireElement("summary-runtime-count"),
    topTags: requireElement("top-tags")
  };
}
