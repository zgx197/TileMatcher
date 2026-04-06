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
    filters: createDefaultFilters(),
    selectedCandidateId: "",
    selectedCandidateDetail: null,
    isBootstrapping: true,
    isLoadingOverview: false,
    isLoadingList: false,
    isLoadingDetail: false,
    notice: null
  };
}
