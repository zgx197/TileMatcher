export function createDefaultFilters(defaultSort = "score_desc") {
  return {
    q: "",
    decision: "",
    difficultyBucket: "",
    tag: "",
    mark: "",
    minScore: "",
    sort: defaultSort
  };
}

export function createEmptyRuntimeSelectionState(batchId = "") {
  return {
    batchId,
    candidateIds: [],
    items: [],
    updatedAt: "",
    exportHistory: [],
    latestExportDraft: null
  };
}

export function createEmptyDiagnosticsState() {
  return {
    health: null,
    workbench: null,
    batchIntegrity: null,
    recentOperations: []
  };
}

export function createAppState() {
  return {
    bootstrap: null,
    reviewMarks: [],
    defaultSort: "score_desc",
    currentBatchId: "",
    overview: null,
    candidatePage: {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 200
    },
    candidateDetailsById: {},
    compareCandidateIds: [],
    runtimeSelection: createEmptyRuntimeSelectionState(),
    diagnostics: createEmptyDiagnosticsState(),
    filters: createDefaultFilters(),
    selectedCandidateId: "",
    selectedCandidateDetail: null,
    isBootstrapping: true,
    isLoadingOverview: false,
    isLoadingList: false,
    isLoadingDetail: false,
    isLoadingRuntimeSelection: false,
    isLoadingExportHistory: false,
    isLoadingDiagnostics: false,
    isExportingRuntimeSelection: false,
    isLoadingRuntimeDraft: false,
    isCommittingRuntimeDraft: false,
    notice: null
  };
}
