function buildQueryString(params) {
  const searchParams = new URLSearchParams();

  for (const [key, value] of Object.entries(params || {})) {
    if (value === undefined || value === null || value === "") {
      continue;
    }

    searchParams.set(key, String(value));
  }

  const query = searchParams.toString();
  return query ? `?${query}` : "";
}

async function request(path, options = {}) {
  const response = await fetch(path, {
    headers: {
      "Content-Type": "application/json"
    },
    ...options
  });

  if (response.status === 204) {
    return null;
  }

  const payload = await response.json();
  if (!response.ok) {
    const error = new Error(payload?.error?.message || "Request failed.");
    error.statusCode = response.status;
    error.details = payload?.error?.details || null;
    throw error;
  }

  return payload;
}

export function getBootstrap() {
  return request("/api/bootstrap");
}

export function getBatchOverview(batchId) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/overview`);
}

export function getCandidates(batchId, filters) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/candidates${buildQueryString({
    ...filters,
    page: 1,
    pageSize: 200
  })}`);
}

export function getCandidateDetail(batchId, candidateId) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/candidates/${encodeURIComponent(candidateId)}`);
}

export function getRuntimeSelection(batchId) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/runtime-selection`);
}

export function saveRuntimeSelection(batchId, candidateIds) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/runtime-selection`, {
    method: "PUT",
    body: JSON.stringify({ candidateIds })
  });
}

export function createRuntimeSelectionDraft(batchId) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/exports/runtime-selection`, {
    method: "POST"
  });
}

export function upsertReview(batchId, candidateId, review) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/reviews/${encodeURIComponent(candidateId)}`, {
    method: "PUT",
    body: JSON.stringify(review)
  });
}

export function deleteReview(batchId, candidateId) {
  return request(`/api/batches/${encodeURIComponent(batchId)}/reviews/${encodeURIComponent(candidateId)}`, {
    method: "DELETE"
  });
}
