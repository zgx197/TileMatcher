import { escapeHtml } from "./presenters.js";

function getTileFill(type) {
  const raw = String(type || "").toLowerCase();
  if (raw.startsWith("bam")) {
    return { top: "#8bcf9a", side: "#5f9d71", stroke: "#32624a" };
  }

  if (raw.startsWith("dot")) {
    return { top: "#f3b57d", side: "#ce8442", stroke: "#8f5328" };
  }

  if (raw.startsWith("chr")) {
    return { top: "#a6c7e8", side: "#6f96c1", stroke: "#42688f" };
  }

  if (["east", "south", "west", "north", "red", "green", "white"].includes(raw)) {
    return { top: "#f4d68f", side: "#d0a95d", stroke: "#8f6a2c" };
  }

  return { top: "#d5c6f0", side: "#a595c8", stroke: "#67578c" };
}

function getBounds(tiles) {
  if (!tiles.length) {
    return {
      minX: 0,
      minY: 0,
      maxX: 12,
      maxY: 8,
      maxZ: 0
    };
  }

  return tiles.reduce((bounds, tile) => ({
    minX: Math.min(bounds.minX, tile.gx),
    minY: Math.min(bounds.minY, tile.gy),
    maxX: Math.max(bounds.maxX, tile.gx + tile.shape.widthUnits),
    maxY: Math.max(bounds.maxY, tile.gy + tile.shape.heightUnits),
    maxZ: Math.max(bounds.maxZ, tile.gz)
  }), {
    minX: Number.POSITIVE_INFINITY,
    minY: Number.POSITIVE_INFINITY,
    maxX: Number.NEGATIVE_INFINITY,
    maxY: Number.NEGATIVE_INFINITY,
    maxZ: 0
  });
}

export function renderLayoutPreview(layout) {
  const tiles = Array.isArray(layout?.tiles) ? [...layout.tiles] : [];
  if (!tiles.length) {
    return `<div class="empty-state">当前候选没有可预览的牌桌布局。</div>`;
  }

  const bounds = getBounds(tiles);
  const widthUnits = Math.max(1, bounds.maxX - bounds.minX);
  const heightUnits = Math.max(1, bounds.maxY - bounds.minY);
  const padding = 18;
  const viewWidth = 480;
  const viewHeight = 320;
  const unit = Math.max(5, Math.min(
    (viewWidth - padding * 2 - bounds.maxZ * 10) / widthUnits,
    (viewHeight - padding * 2 - bounds.maxZ * 14) / heightUnits
  ));
  const elevationY = Math.max(6, unit * 0.9);
  const elevationX = Math.max(4, unit * 0.42);

  tiles.sort((left, right) => left.gz - right.gz || left.gy - right.gy || left.gx - right.gx);

  const content = tiles.map((tile) => {
    const palette = getTileFill(tile.type);
    const width = tile.shape.widthUnits * unit;
    const height = tile.shape.heightUnits * unit;
    const x = padding + (tile.gx - bounds.minX) * unit + tile.gz * elevationX;
    const y = padding + (tile.gy - bounds.minY) * unit - tile.gz * elevationY;
    const label = escapeHtml(tile.type);
    const opacity = tile.faceHiddenInitial ? 0.65 : 1;
    const hiddenRibbon = tile.faceHiddenInitial
      ? `<rect x="${x + width * 0.1}" y="${y + 6}" width="${width * 0.8}" height="8" rx="4" fill="rgba(47, 38, 27, 0.14)" />`
      : "";

    return `
      <g opacity="${opacity}">
        <rect x="${x + elevationX}" y="${y + height - 2}" width="${width}" height="${elevationY + 4}" rx="8" fill="${palette.side}" />
        <rect x="${x}" y="${y}" width="${width}" height="${height}" rx="10" fill="${palette.top}" stroke="${palette.stroke}" stroke-width="2" />
        ${hiddenRibbon}
        <text x="${x + width / 2}" y="${y + height / 2 + 5}" text-anchor="middle" font-size="${Math.max(10, unit * 1.1)}" font-family="IBM Plex Sans, Segoe UI, PingFang SC, sans-serif" fill="#2f261b">${label}</text>
      </g>
    `;
  }).join("");

  return `
    <div class="layout-preview">
      <svg viewBox="0 0 ${viewWidth} ${viewHeight}" role="img" aria-label="牌桌布局预览">
        <defs>
          <filter id="preview-shadow" x="-20%" y="-20%" width="140%" height="140%">
            <feDropShadow dx="0" dy="10" stdDeviation="10" flood-color="rgba(77, 60, 37, 0.18)" />
          </filter>
        </defs>
        <rect x="0" y="0" width="${viewWidth}" height="${viewHeight}" rx="24" fill="transparent"></rect>
        <g filter="url(#preview-shadow)">${content}</g>
      </svg>
    </div>
  `;
}
