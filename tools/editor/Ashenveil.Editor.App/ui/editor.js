/* =====================================================================
   The editor.

   Note what is NOT in this file: any list of what tiles or objects exist.
   That comes from the backend, which reads it out of the game's own source
   (Assets.cs / MapLoader.cs / TileMap.cs). Add a tile to the game and it
   appears here on the next launch.
   ===================================================================== */

const $ = id => document.getElementById(id);

/* ---------------------------------------------------------------- state */

let catalogue = { tiles: [], objects: [], warnings: [] };
let tileById = {};
let objById = {};

let cols = 8, rows = 5;
let tiles = [];                 // tiles[r][c] = tile id string
let tileRot = [];               // tileRot[r][c] = rotation degrees (0/90/180/270)
let objects = [];               // { type, variety, col, row, rotation }
let exits = [];                 // { col, row, to, spawnCol, spawnRow, rotation } — doorways to
                                // other levels; rotation is which edge the bar is drawn on
let selectedExitCell = null;    // { col, row } of the door the sidebar fields are editing
let extras = {};                // keys of the level file the editor doesn't edit,
                                // kept verbatim so saving doesn't silently delete them
let levelNames = [];            // every level on disk, for checking a door's destination

let mode = "tiles";             // "tiles" | "objects" | "select" | "exits"
let selectedTile = null;
let selectedObject = null;
let objRotation = 0;            // applied to newly placed objects
let tileRotation = 0;           // applied to newly painted tiles
let exitRotation = 0;           // which edge a new door's bar sits on, as degrees
let hoverCell = null;           // { x, y } under the cursor, for the door preview
let painting = false;

let selection = null;           // { x, y, w, h } marquee over the grid
let selecting = null;           // { ax, ay } anchor cell while dragging one out
let clipboard = null;           // a block: { w, h, tiles, rot, objects:[{type,variety,rotation,dx,dy}] }
let pasteArmed = false;         // next grid click stamps the clipboard
let dragging = null;            // { block, ox, oy, home } while ctrl-dragging a selection
let ghost = null;               // { block, x, y } — what the ghost overlay is showing

let currentLevel = null;        // name of the level open on disk, or null for unsaved
let dirty = false;

const gridEl = $("grid");
let cellPx = 40;                // current on-screen cell size, set in render()

/* ------------------------------------------------------------- plumbing */

function setStatus(text, kind) {
  const el = $("status");
  el.textContent = text || " ";
  el.className = "hint" + (kind ? " " + kind : "");
}

function markDirty() {
  dirty = true;
  updateTitle();
}

function updateTitle() {
  const name = currentLevel || "(unsaved)";
  $("save").textContent = dirty ? "Save •" : "Save";
  document.title = `Ashenveil — ${name}${dirty ? " *" : ""}`;
}

/** Wraps a backend call so any failure lands in the status line instead of nowhere. */
async function guard(what, fn) {
  try {
    return await fn();
  } catch (err) {
    setStatus(`${what}: ${err.message}`, "error");
    return undefined;
  }
}

/* ----------------------------------------------------------- grid data */

function newGrid(c, r, fill) {
  const g = [];
  for (let y = 0; y < r; y++) {
    const rowArr = [];
    for (let x = 0; x < c; x++) rowArr.push(fill);
    g.push(rowArr);
  }
  return g;
}

function defaultTile() {
  return catalogue.tiles[0] ? catalogue.tiles[0].id : "grass";
}

function newTiles(c, r) { return newGrid(c, r, defaultTile()); }

function resizeGrid(newCols, newRows) {
  const oldTiles = tiles, oldRot = tileRot;
  const g = newTiles(newCols, newRows);
  const gr = newGrid(newCols, newRows, 0);

  // preserve overlapping region
  for (let y = 0; y < Math.min(newRows, oldTiles.length); y++)
    for (let x = 0; x < Math.min(newCols, oldTiles[0].length); x++) {
      g[y][x] = oldTiles[y][x];
      gr[y][x] = oldRot[y][x];
    }

  tiles = g; tileRot = gr;
  cols = newCols; rows = newRows;

  // drop objects and doors that fell outside the new bounds
  objects = objects.filter(o => o.col < cols && o.row < rows);
  exits = exits.filter(e => e.col < cols && e.row < rows);
  selectedExitCell = null;

  cancelPaste();
  clearSelection();
  markDirty();
  render();
}

/* --------------------------------------------------------------- render */

function objectAt(c, r) { return objects.find(o => o.col === c && o.row === r); }

function exitAt(c, r) { return exits.find(e => e.col === c && e.row === r); }

function cellEl(x, y) { return gridEl.querySelector(`.cell[data-x="${x}"][data-y="${y}"]`); }

// Build the inner content of one cell (tile layer + optional object layer).
// Takes its content as arguments so the paste ghost can draw with the same code.
function fillCell(el, tileId, rotation, o) {
  el.innerHTML = "";
  el.style.background = "";

  // --- tile layer ---
  const tile = tileById[tileId];
  if (tile && tile.url) {
    const img = document.createElement("img");
    img.className = "tile";
    img.src = tile.url;
    img.style.transform = `rotate(${rotation || 0}deg)`;
    el.appendChild(img);
  } else {
    // No art mapped (or an unknown id from an older file) — show it, don't hide it.
    el.style.background = "#000";
    el.title = tile ? `${tile.name} (no art)` : `Unknown tile "${tileId}"`;
  }

  // --- object layer ---
  if (!o) return;

  const type = objById[o.type];
  const variety = type && type.varieties.find(v => v.value === o.variety);

  if (variety && variety.url) {
    const img = document.createElement("img");
    img.className = "obj";
    img.src = variety.url;
    img.style.transform = `rotate(${o.rotation || 0}deg)`;
    el.appendChild(img);
  } else {
    const fallback = document.createElement("span");
    fallback.className = "obj-fallback";
    fallback.style.fontSize = (cellPx * 0.7) + "px";
    fallback.textContent = "?";
    el.appendChild(fallback);
  }

  const badge = document.createElement("span");
  badge.className = "objbadge";
  badge.textContent = o.variety;
  el.appendChild(badge);
}

/* A door's rotation is which edge its bar sits on, clockwise from the top —
   the same 0/90/180/270 the tile and object brushes already use, so R and the
   mouse wheel behave the way they do everywhere else. */
const SIDES = { 0: "top", 90: "right", 180: "bottom", 270: "left" };
const sideName = deg => SIDES[wrapDegrees(deg || 0)] || "top";

/** The bar itself. Shared by the real marker and the hover preview. */
function exitBar(deg, extra) {
  const bar = document.createElement("span");
  bar.className = `exitbar ${sideName(deg)}${extra ? " " + extra : ""}`;
  return bar;
}

/**
 * Draws the door layer on top of a cell that fillCell has just filled. Kept
 * separate because a door isn't a thing standing on the cell, it's a property
 * of it — there is no sprite to place, so the marker is pure editor furniture.
 */
function markExit(el, e) {
  // fillCell wipes children but not classes, so both states must be set.
  el.classList.toggle("has-exit", !!e);
  if (!e) { el.title = ""; return; }

  const selected = !!selectedExitCell
    && e.col === selectedExitCell.col && e.row === selectedExitCell.row;

  const state = !isKnownLevel(e.to) ? "broken" : selected ? "selected" : "";
  el.appendChild(exitBar(e.rotation, state));

  // The destination used to be a badge on the cell. The sidebar list carries it
  // now, so the grid stays a map instead of a wall of overlapping labels.
  el.title = `Door (${sideName(e.rotation)}) -> ${e.to || "(nowhere)"} at ${e.spawnCol},${e.spawnRow}`
    + (isKnownLevel(e.to) ? "" : "  — no such level");
}

/** False only when we have a level list and the name isn't in it. */
function isKnownLevel(name) {
  if (!name) return false;
  if (levelNames.length === 0) return true;   // list not loaded yet — don't cry wolf
  return levelNames.includes(name);
}

function renderCell(x, y) {
  const el = cellEl(x, y);
  if (!el) return;
  fillCell(el, tiles[y][x], tileRot[y][x], objectAt(x, y));
  markExit(el, exitAt(x, y));
}

function render() {
  // Fit cell size to the viewport; cap so big grids stay usable.
  const area = document.querySelector("main");
  const budget = Math.min(area.clientWidth - 48, area.clientHeight - 48);
  cellPx = Math.max(18, Math.min(72, Math.floor(budget / Math.max(cols, rows))));

  gridEl.style.gridTemplateColumns = `repeat(${cols}, ${cellPx}px)`;
  gridEl.style.gridTemplateRows = `repeat(${rows}, ${cellPx}px)`;
  gridEl.innerHTML = "";

  for (let y = 0; y < rows; y++) {
    for (let x = 0; x < cols; x++) {
      const el = document.createElement("div");
      el.className = "cell";
      el.dataset.x = x; el.dataset.y = y;
      gridEl.appendChild(el);
      renderCell(x, y);
    }
  }

  // The overlays outlive the cells, so they go back on after the rebuild.
  gridEl.append(selectionEl, ghostEl);
  drawSelection();
  drawGhost();

  $("cols").value = cols;
  $("rows").value = rows;
  updateJson();
  updateTitle();
  updateBrush();
  // The cells were thrown away and rebuilt, taking the preview bar with them.
  updateExitHover();
}

window.addEventListener("resize", () => { if (tiles.length) render(); });

/* ------------------------------------------------ selection + clipboard */

/* Both overlays live inside #grid and are positioned in cell coordinates.
   They are created once and re-attached after every render(). */
const selectionEl = document.createElement("div");
selectionEl.id = "selection";
selectionEl.hidden = true;

const ghostEl = document.createElement("div");
ghostEl.id = "pasteGhost";
ghostEl.hidden = true;

/** Places an overlay over a block of cells. The grid's gap is 1px. */
function placeOverlay(el, x, y, w, h) {
  const step = cellPx + 1;
  el.style.left = (x * step) + "px";
  el.style.top = (y * step) + "px";
  el.style.width = (w * cellPx + w - 1) + "px";
  el.style.height = (h * cellPx + h - 1) + "px";
}

function drawSelection() {
  if (!selection) { selectionEl.hidden = true; return; }
  placeOverlay(selectionEl, selection.x, selection.y, selection.w, selection.h);
  selectionEl.hidden = false;
}

/** Draws whatever block is in flight — a pending paste or a block being dragged. */
function drawGhost() {
  if (!ghost) { ghostEl.hidden = true; return; }

  const b = ghost.block;
  placeOverlay(ghostEl, ghost.x, ghost.y, b.w, b.h);
  ghostEl.style.gridTemplateColumns = `repeat(${b.w}, ${cellPx}px)`;
  ghostEl.style.gridTemplateRows = `repeat(${b.h}, ${cellPx}px)`;
  ghostEl.innerHTML = "";

  for (let dy = 0; dy < b.h; dy++)
    for (let dx = 0; dx < b.w; dx++) {
      const cell = document.createElement("div");
      cell.className = "cell";
      fillCell(cell, b.tiles[dy][dx], b.rot[dy][dx], b.objects.find(o => o.dx === dx && o.dy === dy));
      markExit(cell, b.exits.find(e => e.dx === dx && e.dy === dy));
      ghostEl.appendChild(cell);
    }

  ghostEl.hidden = false;
}

/** Sets the marquee from two corner cells, in any order. */
function setSelection(x0, y0, x1, y1) {
  const x = Math.min(x0, x1), y = Math.min(y0, y1);
  selection = { x, y, w: Math.abs(x1 - x0) + 1, h: Math.abs(y1 - y0) + 1 };
  drawSelection();
  updateSelectionUi();
}

function clearSelection() {
  selection = null;
  drawSelection();
  updateSelectionUi();
}

function cancelPaste() {
  if (!pasteArmed) return;
  pasteArmed = false;
  ghost = null;
  drawGhost();
  updateSelectionUi();
}

function inRect(o, r) {
  return o.col >= r.x && o.col < r.x + r.w && o.row >= r.y && o.row < r.y + r.h;
}

function updateSelectionUi() {
  const has = !!selection;
  ["copySel", "cutSel", "deleteSel"].forEach(id => $(id).disabled = !has);
  $("pasteSel").disabled = !clipboard;

  if (pasteArmed)
    $("selInfo").textContent = `Clipboard ${clipboard.w}×${clipboard.h} — click a cell to stamp, Esc to cancel.`;
  else if (has)
    $("selInfo").textContent = `Selected ${selection.w}×${selection.h} at ${selection.x},${selection.y}.`;
  else
    $("selInfo").textContent = "Drag on the grid to select.";
}

/* A "block" is a rect of the map lifted out of it: tile ids, their rotations, the
   objects standing on them and any doors set on them, all at offsets from its
   top-left. The clipboard holds one; so does a ctrl-drag while it's in the air.

   Doors travel with the block for the same reason objects do — a move that
   silently dropped them would be a trap. Their destination and spawn are left
   alone: those are coordinates in *another* level, which this move didn't touch. */

function captureBlock(r) {
  const block = { w: r.w, h: r.h, tiles: [], rot: [], objects: [], exits: [] };

  for (let dy = 0; dy < r.h; dy++) {
    block.tiles.push(tiles[r.y + dy].slice(r.x, r.x + r.w));
    block.rot.push(tileRot[r.y + dy].slice(r.x, r.x + r.w).map(v => v || 0));
  }

  block.objects = objects.filter(o => inRect(o, r)).map(o => ({
    type: o.type, variety: o.variety, rotation: o.rotation || 0,
    dx: o.col - r.x, dy: o.row - r.y,
  }));

  block.exits = exits.filter(e => inRect(e, r)).map(e => ({
    to: e.to, spawnCol: e.spawnCol, spawnRow: e.spawnRow, rotation: e.rotation || 0,
    dx: e.col - r.x, dy: e.row - r.y,
  }));

  return block;
}

/** Writes a block with its top-left at (x, y), clipped to the grid. Returns the rect written. */
function stampBlock(block, x, y) {
  const w = Math.min(block.w, cols - x);
  const h = Math.min(block.h, rows - y);
  if (w <= 0 || h <= 0) return null;

  for (let dy = 0; dy < h; dy++)
    for (let dx = 0; dx < w; dx++) {
      tiles[y + dy][x + dx] = block.tiles[dy][dx];
      tileRot[y + dy][x + dx] = block.rot[dy][dx];
    }

  // A block replaces everything it lands on, so what was there goes first.
  const target = { x, y, w, h };
  objects = objects.filter(o => !inRect(o, target));
  exits = exits.filter(e => !inRect(e, target));
  selectedExitCell = null;

  block.objects.forEach(o => {
    if (o.dx >= w || o.dy >= h) return;
    objects.push({ type: o.type, variety: o.variety, col: x + o.dx, row: y + o.dy, rotation: o.rotation });
  });

  block.exits.forEach(e => {
    if (e.dx >= w || e.dy >= h) return;
    exits.push({
      col: x + e.dx, row: y + e.dy,
      to: e.to, spawnCol: e.spawnCol, spawnRow: e.spawnRow, rotation: e.rotation || 0,
    });
  });

  markDirty();
  render();
  return target;
}

function copySelection() {
  if (!selection) return;
  clipboard = captureBlock(selection);
  updateSelectionUi();
  setStatus(`Copied ${clipboard.w}×${clipboard.h}`, "ok");
}

/** Resets tiles to the default and drops every object and door in the rect. */
function clearRegion(r) {
  const fill = defaultTile();
  for (let dy = 0; dy < r.h; dy++)
    for (let dx = 0; dx < r.w; dx++) {
      tiles[r.y + dy][r.x + dx] = fill;
      tileRot[r.y + dy][r.x + dx] = 0;
    }

  objects = objects.filter(o => !inRect(o, r));
  exits = exits.filter(e => !inRect(e, r));
  if (selectedExitCell && inRect({ col: selectedExitCell.col, row: selectedExitCell.row }, r))
    selectedExitCell = null;
  markDirty();
  render();
}

function cutSelection() {
  if (!selection) return;
  copySelection();
  clearRegion(selection);
  setStatus(`Cut ${selection.w}×${selection.h}`, "ok");
}

function deleteSelection() {
  if (!selection) return;
  clearRegion(selection);
  setStatus(`Cleared ${selection.w}×${selection.h}`, "ok");
}

function beginPaste() {
  if (!clipboard) return;
  setMode("select");
  clearSelection();
  pasteArmed = true;
  ghost = null;
  updateSelectionUi();
  setStatus(`Paste armed — click to stamp ${clipboard.w}×${clipboard.h}`);
}

function stampClipboard(x, y) {
  const r = stampBlock(clipboard, x, y);
  if (!r) return;
  setSelection(r.x, r.y, r.x + r.w - 1, r.y + r.h - 1);
  setStatus(`Pasted ${r.w}×${r.h} at ${r.x},${r.y}`, "ok");
}

/* ----- ctrl-drag: lift the selected block and set it down somewhere else ----- */

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

function beginDrag(x, y) {
  dragging = {
    block: captureBlock(selection),
    ox: x - selection.x,          // where inside the block it was grabbed
    oy: y - selection.y,
    home: { x: selection.x, y: selection.y },
  };

  // The block is in the air now: the marquee is left over an emptied hole.
  ghost = { block: dragging.block, x: selection.x, y: selection.y };
  gridEl.classList.add("moving");
  clearRegion(selection);
  setStatus(`Moving ${dragging.block.w}×${dragging.block.h} — release to drop, Esc to cancel`);
}

function dragTo(x, y) {
  const b = dragging.block;
  const nx = clamp(x - dragging.ox, 0, cols - b.w);
  const ny = clamp(y - dragging.oy, 0, rows - b.h);
  if (ghost.x === nx && ghost.y === ny) return;

  ghost.x = nx; ghost.y = ny;
  drawGhost();
}

/** Ends a drag by setting the block down — where the ghost is, or back home. */
function dropDrag(cancelled) {
  const at = cancelled ? dragging.home : ghost;
  const block = dragging.block;

  dragging = null;
  ghost = null;
  gridEl.classList.remove("moving");

  const r = stampBlock(block, at.x, at.y);
  setSelection(r.x, r.y, r.x + r.w - 1, r.y + r.h - 1);
  setStatus(cancelled ? "Move cancelled" : `Moved to ${r.x},${r.y}`, cancelled ? null : "ok");
}

$("copySel").onclick = copySelection;
$("cutSel").onclick = cutSelection;
$("pasteSel").onclick = beginPaste;
$("deleteSel").onclick = deleteSelection;

/* ---------------------------------------------------------- held brush */

const brushEl = $("brush");
const brushImg = $("brushImg");
const brushRotEl = $("brushRot");

/** What the next click will place: its art and the rotation it lands with. */
function currentBrush() {
  // Neither mode holds a piece: select drags a marquee, doors set a property.
  if (mode === "select" || mode === "exits") return { url: null, rotation: 0 };

  if (mode === "tiles") {
    const tile = tileById[selectedTile];
    return { url: tile && tile.url, rotation: tileRotation };
  }

  const type = objById[selectedObject];
  if (!type) return { url: null, rotation: objRotation };

  // Follow the dropdown, not just the type — variety is part of the brush.
  const wanted = parseInt($("variety").value, 10);
  const variety = type.varieties.find(v => v.value === wanted) || type.varieties[0];
  return { url: variety && variety.url, rotation: objRotation };
}

/** Repaints the ghost. Cheap enough to call on any change that could alter it. */
function updateBrush() {
  const { url, rotation } = currentBrush();

  // Nothing to show without art — don't leave a stale sprite hanging off the cursor.
  if (!url) {
    brushEl.hidden = true;
    return;
  }

  brushEl.className = mode;
  brushEl.style.width = cellPx + "px";
  brushEl.style.height = cellPx + "px";

  brushImg.src = url;
  brushEl.querySelector(".brush-art").style.transform = `rotate(${rotation}deg)`;

  // The label is only noise at 0°.
  brushRotEl.textContent = rotation + "°";
  brushRotEl.hidden = rotation === 0;
}

function moveBrush(e) {
  // Centred on the pointer, so it reads as "holding" the piece.
  brushEl.style.left = (e.clientX - cellPx / 2) + "px";
  brushEl.style.top  = (e.clientY - cellPx / 2) + "px";
}

/**
 * Doors have no cursor brush — there's no sprite to hold — so the preview is a
 * faded bar drawn in the cell under the pointer, on the side it would land on.
 * Without it there is no feedback at all until after the click.
 */
function updateExitHover() {
  gridEl.querySelectorAll(".exitbar.ghost").forEach(b => b.remove());
  if (mode !== "exits" || !hoverCell) return;

  // A cell that already has a door shows its real bar; a second, faded one on
  // top of it would just read as a rendering glitch.
  if (exitAt(hoverCell.x, hoverCell.y)) return;

  const el = cellEl(hoverCell.x, hoverCell.y);
  if (el) el.appendChild(exitBar(exitRotation, "ghost"));
}

gridEl.addEventListener("mousemove", e => {
  if (pasteArmed) {
    const cell = e.target.closest(".cell");
    if (!cell) return;
    const x = +cell.dataset.x, y = +cell.dataset.y;
    if (ghost && ghost.x === x && ghost.y === y) return;
    ghost = { block: clipboard, x, y };
    drawGhost();
    return;
  }

  if (mode === "exits") {
    const cell = e.target.closest(".cell");
    const x = cell ? +cell.dataset.x : null, y = cell ? +cell.dataset.y : null;
    if (hoverCell && hoverCell.x === x && hoverCell.y === y) return;
    hoverCell = cell ? { x, y } : null;
    updateExitHover();
    return;
  }

  if (!currentBrush().url) return;
  moveBrush(e);
  brushEl.hidden = false;
});

// Only meaningful over the grid; anywhere else it's just clutter.
gridEl.addEventListener("mouseleave", () => {
  brushEl.hidden = true;
  if (pasteArmed) { ghost = null; drawGhost(); }
  hoverCell = null;
  updateExitHover();
});

/* -------------------------------------------------------------- editing */

function paintCell(x, y) {
  if (mode !== "tiles" || !selectedTile) return;
  tiles[y][x] = selectedTile;
  tileRot[y][x] = tileRotation;
  renderCell(x, y);
  updateJson();
  markDirty();
}

function placeObject(x, y) {
  if (!selectedObject) return;
  const variety = parseInt($("variety").value, 10);
  const obj = { type: selectedObject, variety, col: x, row: y, rotation: objRotation };

  const existing = objects.findIndex(o => o.col === x && o.row === y);
  if (existing >= 0) objects[existing] = obj;
  else objects.push(obj);

  renderCell(x, y);
  updateJson();
  markDirty();
}

function removeObject(x, y) {
  const before = objects.length;
  objects = objects.filter(o => !(o.col === x && o.row === y));
  if (objects.length === before) return;

  renderCell(x, y);
  updateJson();
  markDirty();
}

/* ------------------------------------------------------------------ doors */

/* The sidebar fields do double duty: with no door selected they describe the next
   one you place, and with one selected they edit it live. Selection is held as a
   coordinate, not a reference — the exits array gets rebuilt by filter() in a few
   places, and a stale object reference would edit a door no longer in the level. */

function selectedExit() {
  return selectedExitCell && exitAt(selectedExitCell.col, selectedExitCell.row);
}

function selectExit(e) {
  const was = selectedExitCell;
  selectedExitCell = e ? { col: e.col, row: e.row } : null;

  // Repaint whichever cells changed state rather than the whole grid.
  if (was) renderCell(was.col, was.row);
  if (e) renderCell(e.col, e.row);
  updateExitUi();
}

/** Left-click: edit the door already here, or drop a new one and select it. */
function placeExit(x, y) {
  const existing = exitAt(x, y);
  if (existing) { selectExit(existing); loadExitIntoForm(existing); return; }

  // A door with no destination throws the moment the game walks through it,
  // so don't let one be created in the first place.
  const aim = exitFromForm();
  if (!aim.to) { setStatus("Pick a destination level before placing a door.", "error"); return; }

  const e = { col: x, row: y, ...aim };
  exits.push(e);
  selectExit(e);
  renderCell(x, y);
  updateJson();
  updateExitUi();
  markDirty();
}

function removeExit(x, y) {
  const before = exits.length;
  exits = exits.filter(e => !(e.col === x && e.row === y));
  if (exits.length === before) return;

  if (selectedExitCell && selectedExitCell.col === x && selectedExitCell.row === y)
    selectedExitCell = null;

  renderCell(x, y);
  updateJson();
  updateExitUi();
  markDirty();
}

/** The destination the sidebar fields currently describe. */
function exitFromForm() {
  return {
    to: $("exitTo").value,
    spawnCol: Math.max(0, +$("exitSpawnCol").value || 0),
    spawnRow: Math.max(0, +$("exitSpawnRow").value || 0),
    rotation: exitRotation,
  };
}

function loadExitIntoForm(e) {
  // A door pointing at a deleted level has no matching <option>; add one rather
  // than letting the field silently snap to a different destination.
  if (e.to && !Array.from($("exitTo").options).some(o => o.value === e.to)) {
    const opt = document.createElement("option");
    opt.value = e.to;
    opt.textContent = `${e.to} (missing)`;
    $("exitTo").appendChild(opt);
  }

  $("exitTo").value = e.to || "";
  $("exitSpawnCol").value = e.spawnCol;
  $("exitSpawnRow").value = e.spawnRow;

  // Picking a door also picks up its side, so R carries on from where that door
  // is rather than from whatever the last one placed happened to be.
  exitRotation = wrapDegrees(e.rotation || 0);
  $("exitRotateBtn").textContent = sideName(exitRotation);

  updateExitUi();
}

/** Writes the fields back into the selected door, if there is one. */
function applyExitForm() {
  const e = selectedExit();
  if (!e) return;

  Object.assign(e, exitFromForm());
  renderCell(e.col, e.row);
  updateJson();
  updateExitUi();
  markDirty();
}

["exitTo", "exitSpawnCol", "exitSpawnRow"].forEach(id => {
  $(id).onchange = applyExitForm;
});

/** Rebuilds the destination dropdown from the levels on disk. */
function buildExitTargets() {
  const sel = $("exitTo");
  const keep = sel.value;
  sel.innerHTML = "";

  levelNames.forEach(n => {
    const opt = document.createElement("option");
    opt.value = n;
    // A door back into the same level is legal and occasionally useful.
    opt.textContent = n === currentLevel ? `${n} (this level)` : n;
    sel.appendChild(opt);
  });

  if (keep && levelNames.includes(keep)) sel.value = keep;
}

/**
 * The door list: one row per door, reading "where you stand | where it puts you".
 * The grid shows the same thing, but only as outlines you have to go hunting for —
 * this is the view that answers "what doors does this level have" in one look.
 */
function buildExitList() {
  const list = $("exitList");
  list.innerHTML = "";

  if (exits.length === 0) {
    const empty = document.createElement("div");
    empty.className = "exit-empty";
    empty.textContent = "No doors yet.";
    list.appendChild(empty);
    return;
  }

  // Reading order, not insertion order — the list should match the eye's scan of
  // the grid, top-left to bottom-right, however the doors were actually added.
  const ordered = exits.slice().sort((a, b) => a.row - b.row || a.col - b.col);
  const sel = selectedExitCell;

  ordered.forEach(e => {
    const row = document.createElement("div");
    row.className = "exit-row";
    if (sel && sel.col === e.col && sel.row === e.row) row.classList.add("selected");
    if (!isKnownLevel(e.to)) row.classList.add("broken");

    const at = document.createElement("span");
    at.className = "exit-at";
    at.textContent = `${e.col},${e.row}`;

    const bar = document.createElement("span");
    bar.className = "exit-bar";

    const dest = document.createElement("span");
    dest.className = "exit-dest";
    dest.textContent = e.to || "(nowhere)";

    const spawn = document.createElement("span");
    spawn.className = "exit-spawn";
    spawn.textContent = `${e.spawnCol},${e.spawnRow}`;

    // Which edge the bar is on. Cheap to show and the only way to check a door
    // faces the right way without hunting for its cell on the grid.
    const side = document.createElement("span");
    side.className = "exit-side";
    side.textContent = sideName(e.rotation);

    const del = document.createElement("button");
    del.className = "exit-del";
    del.textContent = "\u00d7";
    del.title = "Remove this door";
    // The row itself selects, so the delete must not also fire that.
    del.onclick = ev => { ev.stopPropagation(); removeExit(e.col, e.row); };

    row.append(at, side, bar, dest, spawn, del);
    row.title = `Stand on ${e.col},${e.row} (bar on the ${sideName(e.rotation)})`
      + ` to reach ${e.to || "nowhere"} at ${e.spawnCol},${e.spawnRow}`;
    row.onclick = () => { selectExit(e); loadExitIntoForm(e); };

    list.appendChild(row);
  });
}

function updateExitUi() {
  buildExitList();

  const sel = selectedExit();
  $("exitInfo").textContent = sel
    ? `Editing the door at ${sel.col},${sel.row} — the fields above change it.`
    : exits.length
      ? "Click a door above or on the grid to edit it."
      : "Left-click a cell to put a door on it.";

  // The one mistake worth shouting about: the game throws on a missing level file.
  const broken = exits.filter(e => !isKnownLevel(e.to));
  const list = $("exitWarnings");
  list.innerHTML = "";
  broken.forEach(e => {
    const li = document.createElement("li");
    li.textContent = `Door at ${e.col},${e.row} points at "${e.to || "nothing"}" — no such level.`;
    list.appendChild(li);
  });
  list.hidden = broken.length === 0;
}

gridEl.addEventListener("mousedown", e => {
  const el = e.target.closest(".cell");
  if (!el) return;
  const x = +el.dataset.x, y = +el.dataset.y;

  if (pasteArmed) {
    if (e.button === 2) cancelPaste();
    else stampClipboard(x, y);
    return;
  }

  if (mode === "select") {
    if (e.button === 2) { clearSelection(); return; }
    e.preventDefault();          // stop the drag from becoming a native text selection

    // Ctrl inside the marquee grabs the block instead of starting a new one.
    if ((e.ctrlKey || e.metaKey) && selection && inRect({ col: x, row: y }, selection))
      beginDrag(x, y);
    else {
      selecting = { ax: x, ay: y };
      setSelection(x, y, x, y);
    }
  }
  else if (mode === "tiles") { painting = true; paintCell(x, y); }
  else if (mode === "exits") {
    if (e.button === 2) removeExit(x, y); else placeExit(x, y);
  }
  else if (e.button === 2) removeObject(x, y);
  else placeObject(x, y);
});

gridEl.addEventListener("mouseover", e => {
  if (!painting && !selecting && !dragging) return;
  const el = e.target.closest(".cell");
  if (!el) return;
  const x = +el.dataset.x, y = +el.dataset.y;

  if (dragging) dragTo(x, y);
  else if (selecting) setSelection(selecting.ax, selecting.ay, x, y);
  else paintCell(x, y);
});

window.addEventListener("mouseup", () => {
  if (dragging) dropDrag(false);
  painting = false;
  selecting = null;
});
gridEl.addEventListener("contextmenu", e => e.preventDefault());

/* ------------------------------------------------------------- palettes */

function buildTilePalette() {
  const p = $("tilePalette");
  p.innerHTML = "";

  catalogue.tiles.forEach(t => {
    const el = document.createElement("div");
    el.className = "swatch" + (t.id === selectedTile ? " selected" : "");

    const chip = document.createElement("span");
    chip.className = "chip";
    if (t.url) chip.style.backgroundImage = `url("${t.url}")`;
    else chip.textContent = "?";

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = t.name;

    el.append(chip, name);
    el.onclick = () => { selectedTile = t.id; buildTilePalette(); updateBrush(); };
    p.appendChild(el);
  });
}

function buildObjectPalette() {
  const p = $("objectPalette");
  p.innerHTML = "";

  catalogue.objects.forEach(o => {
    const el = document.createElement("div");
    el.className = "swatch" + (o.id === selectedObject ? " selected" : "");

    const first = o.varieties[0];
    const chip = document.createElement("span");
    chip.className = "chip contain";
    if (first && first.url) chip.style.backgroundImage = `url("${first.url}")`;
    else chip.textContent = "?";

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = o.name;

    el.append(chip, name);
    el.onclick = () => { selectedObject = o.id; buildObjectPalette(); buildVariety(); updateBrush(); };
    p.appendChild(el);
  });
}

function buildVariety() {
  const sel = $("variety");
  sel.innerHTML = "";

  const type = objById[selectedObject];
  if (!type) return;

  type.varieties.forEach(v => {
    const opt = document.createElement("option");
    opt.value = v.value;
    opt.textContent = `${v.value} — ${v.label}`;
    sel.appendChild(opt);
  });
}

function applyCatalogue(next) {
  catalogue = next;
  tileById = Object.fromEntries(catalogue.tiles.map(t => [t.id, t]));
  objById  = Object.fromEntries(catalogue.objects.map(o => [o.id, o]));

  selectedTile   = catalogue.tiles[0]   ? catalogue.tiles[0].id   : null;
  selectedObject = catalogue.objects[0] ? catalogue.objects[0].id : null;

  buildTilePalette();
  buildObjectPalette();
  buildVariety();

  // Anything the backend couldn't line up is shown rather than silently dropped.
  const list = $("warningList");
  list.innerHTML = "";
  catalogue.warnings.forEach(w => {
    const li = document.createElement("li");
    li.textContent = w;
    list.appendChild(li);
  });
  $("warnings").hidden = catalogue.warnings.length === 0;

  // Objects mode is pointless with nothing to place.
  $("modeObjects").disabled = catalogue.objects.length === 0;

  updateBrush();
}

/* ---------------------------------------------------------- mode + keys */

$("variety").onchange = updateBrush;

$("modeTiles").onclick = () => setMode("tiles");
$("modeObjects").onclick = () => setMode("objects");
$("modeSelect").onclick = () => setMode("select");
$("modeExits").onclick = () => setMode("exits");

const MODES = {
  tiles: "modeTiles", objects: "modeObjects", select: "modeSelect", exits: "modeExits",
};
const MODE_PANELS = {
  tiles: "tilePanel", objects: "objectPanel", select: "selectPanel", exits: "exitPanel",
};

function setMode(m) {
  // Leaving select mode drops the marquee and any pending paste; the clipboard stays.
  if (mode === "select" && m !== "select") { cancelPaste(); clearSelection(); }

  mode = m;
  for (const [key, id] of Object.entries(MODES)) $(id).classList.toggle("active", key === m);
  for (const [key, id] of Object.entries(MODE_PANELS)) $(id).hidden = key !== m;
  gridEl.classList.toggle("selecting", m === "select");
  gridEl.classList.toggle("doors", m === "exits");

  // The door preview belongs to this mode only, and the pointer hasn't moved,
  // so it has to be taken down (or put up) here rather than on the next mousemove.
  if (m !== "exits") hoverCell = null;

  updateBrush();
  updateSelectionUi();
  updateExitUi();
  updateExitHover();
}

/* rotation controls — each mode has its own current-rotation brush */

/** Keeps a rotation in 0–270 whichever way it was turned. */
const wrapDegrees = d => ((d % 360) + 360) % 360;

function rotateObjects(step) {
  objRotation = wrapDegrees(objRotation + step);
  $("rotateBtn").textContent = objRotation + "°";
  updateBrush();
}

function rotateTiles(step) {
  tileRotation = wrapDegrees(tileRotation + step);
  $("tileRotateBtn").textContent = tileRotation + "°";
  updateBrush();
}

/* Doors turn too, but a door's rotation is which edge of the cell its bar sits on
   rather than how a sprite is drawn. With one selected, R moves that door's bar —
   the obvious reading of pressing rotate while something is picked. With none
   selected it aims the next one, matching how the tile and object brushes behave. */
function rotateExits(step) {
  exitRotation = wrapDegrees(exitRotation + step);
  $("exitRotateBtn").textContent = sideName(exitRotation);

  const sel = selectedExit();
  if (sel) {
    sel.rotation = exitRotation;
    renderCell(sel.col, sel.row);
    updateJson();
    updateExitUi();
    markDirty();
  }

  updateExitHover();
}

/** Turns whichever brush is active. step is +90 or -90. */
function rotateBrush(step) {
  if (mode === "select") return;         // nothing is held in select mode
  if (mode === "exits") rotateExits(step);
  else if (mode === "objects") rotateObjects(step);
  else rotateTiles(step);
}

$("rotateBtn").onclick = () => rotateObjects(90);
$("tileRotateBtn").onclick = () => rotateTiles(90);
$("exitRotateBtn").onclick = () => rotateExits(90);

// Scrolling over the grid rotates instead of scrolling the canvas — the wheel is
// the natural gesture for this, and there's nothing worth scrolling to under it.
gridEl.addEventListener("wheel", e => {
  e.preventDefault();
  rotateBrush(e.deltaY > 0 ? 90 : -90);
}, { passive: false });

window.addEventListener("keydown", e => {
  // Don't steal keys from the level-name prompt or the number inputs.
  if (e.target.matches("input, select, textarea")) return;
  // The Bounds and Assets screens borrow the same window.
  if ($("levelsView").hidden) return;

  const key = e.key.toLowerCase();
  const cmd = e.ctrlKey || e.metaKey;

  if (cmd) {
    if (key === "s") { e.preventDefault(); save(); return; }
    if (key === "c") { e.preventDefault(); copySelection(); return; }
    if (key === "x") { e.preventDefault(); cutSelection(); return; }
    if (key === "v") { e.preventDefault(); beginPaste(); return; }
    if (key === "a") {
      e.preventDefault();
      setMode("select");
      setSelection(0, 0, cols - 1, rows - 1);
      return;
    }
    return;
  }

  if (key === "r") { rotateBrush(e.shiftKey ? -90 : 90); return; }
  if (key === "delete" || key === "backspace") { e.preventDefault(); deleteSelection(); return; }
  if (key === "escape") {
    if (dragging) dropDrag(true);
    else { cancelPaste(); clearSelection(); selectExit(null); }
  }
});

/* ------------------------------------------------------- the level file */

const EDITED_KEYS = ["cols", "rows", "tiles", "rotations", "objects", "exits"];

function buildMap() {
  // exits is omitted when empty rather than written as [] — most levels have no
  // doors, and the loader treats a missing key and an empty list the same way.
  return {
    ...extras,
    cols, rows, tiles, rotations: tileRot, objects,
    ...(exits.length ? { exits } : {}),
  };
}

function updateJson() {
  $("json").value = JSON.stringify(buildMap(), null, 2);
}

function loadMap(map) {
  cols = map.cols;
  rows = map.rows;
  tiles = map.tiles;
  objects = map.objects || [];
  exits = map.exits || [];                             // levels with no doors have no key
  tileRot = map.rotations || newGrid(cols, rows, 0);   // older files have no rotations
  extras = Object.fromEntries(
    Object.entries(map).filter(([k]) => !EDITED_KEYS.includes(k)));
  dirty = false;
  cancelPaste();
  clearSelection();
  selectedExitCell = null;
  buildExitTargets();
  updateExitUi();
  render();
}

function blankMap() {
  cols = 8; rows = 5;
  tiles = newTiles(cols, rows);
  tileRot = newGrid(cols, rows, 0);
  objects = [];
  exits = [];
  extras = {};
  currentLevel = null;
  dirty = false;
  cancelPaste();
  clearSelection();
  selectedExitCell = null;
  buildExitTargets();
  updateExitUi();
  render();
}

/* ------------------------------------------------------------- commands */

async function refreshLevelList(select) {
  const names = await guard("Listing levels", () => Api.listLevels());
  if (!names) return;

  const list = $("levelList");
  list.innerHTML = "";

  if (names.length === 0) {
    const opt = document.createElement("option");
    opt.value = "";
    opt.textContent = "(no levels yet)";
    list.appendChild(opt);
  }

  names.forEach(n => {
    const opt = document.createElement("option");
    opt.value = n;
    opt.textContent = n;
    list.appendChild(opt);
  });

  if (select) list.value = select;

  // Doors point at levels by name, so this list is also what a door can target
  // and what tells us a door has been left pointing at a deleted file.
  levelNames = names;
  buildExitTargets();
  updateExitUi();
  if (tiles.length) render();       // repaint door markers with fresh validity
}

async function openLevel(name) {
  if (!name) return;
  if (!confirmDiscard()) {
    $("levelList").value = currentLevel || "";
    return;
  }

  const map = await guard(`Loading ${name}`, () => Api.readLevel(name));
  if (!map) return;

  currentLevel = name;
  loadMap(map);
  setStatus(`Opened ${name}`, "ok");
}

function confirmDiscard() {
  if (!dirty) return true;
  return window.confirm("This level has unsaved changes. Discard them?");
}

async function save() {
  if (!currentLevel) return saveAs();

  const ok = await guard(`Saving ${currentLevel}`, () => Api.saveLevel(currentLevel, buildMap()));
  if (!ok) return;

  dirty = false;
  updateTitle();
  setStatus(`Saved ${currentLevel}.json`, "ok");
}

async function saveAs() {
  const name = promptForName(currentLevel || "NewLevel");
  if (!name) return;

  const ok = await guard(`Saving ${name}`, () => Api.saveLevel(name, buildMap()));
  if (!ok) return;

  currentLevel = name;
  dirty = false;
  await refreshLevelList(name);
  updateTitle();
  setStatus(`Saved ${name}.json`, "ok");
}

/**
 * Asks for a level name and rejects anything that isn't a plain filename.
 * The backend enforces this too — this is just so the user finds out before
 * a round trip rather than after.
 */
function promptForName(suggestion) {
  const name = window.prompt("Level name (no extension):", suggestion);
  if (name === null) return null;

  const trimmed = name.trim();
  if (!/^[A-Za-z0-9 _-]+$/.test(trimmed)) {
    setStatus("Level names can only use letters, numbers, spaces, - and _", "error");
    return null;
  }
  return trimmed;
}

$("changeProject").onclick = async () => {
  const state = await guard("Choosing folder", () => Api.pickProject());
  if (!state) return;
  await bindProject(state, { reload: true });
};

$("levelList").onchange = e => openLevel(e.target.value);
$("reloadLevel").onclick = () => currentLevel && openLevel(currentLevel);
$("save").onclick = save;
$("saveAs").onclick = saveAs;

$("newLevel").onclick = () => {
  if (!confirmDiscard()) return;
  blankMap();
  setStatus("New level — Save As to name it");
};

$("duplicate").onclick = async () => {
  const name = promptForName((currentLevel || "Level") + "Copy");
  if (!name) return;

  const ok = await guard(`Saving ${name}`, () => Api.saveLevel(name, buildMap()));
  if (!ok) return;

  currentLevel = name;
  dirty = false;
  await refreshLevelList(name);
  updateTitle();
  setStatus(`Duplicated to ${name}.json`, "ok");
};

$("deleteLevel").onclick = async () => {
  const name = $("levelList").value;
  if (!name) return;
  if (!window.confirm(`Delete ${name}.json? This cannot be undone.`)) return;

  const ok = await guard(`Deleting ${name}`, () => Api.deleteLevel(name));
  if (!ok) return;

  if (currentLevel === name) { currentLevel = null; dirty = false; }
  await refreshLevelList();
  setStatus(`Deleted ${name}.json`, "ok");
};

$("resize").onclick = () => {
  const c = Math.max(1, Math.min(60, +$("cols").value || 1));
  const r = Math.max(1, Math.min(60, +$("rows").value || 1));
  resizeGrid(c, r);
};

/* ----------------------------------------------------------------- boot */

async function bindProject(state, opts) {
  // "auto" means we found the game around the editor rather than being told.
  $("projectPath").textContent = state.gameFolder
    ? (state.auto ? state.gameFolder + "  (auto)" : state.gameFolder)
    : "No folder found";
  $("projectPath").title = state.gameFolder || "";

  const enabled = state.valid;
  ["save", "saveAs", "newLevel", "duplicate", "deleteLevel", "reloadLevel", "levelList", "resize"]
    .forEach(id => $(id).disabled = !enabled);

  if (!enabled) {
    setStatus("Pick the Ashenveil folder to start.", "error");
    return;
  }

  const next = await guard("Reading the game's tiles and objects", () => Api.getCatalogue());
  if (!next) return;

  applyCatalogue(next);
  await refreshLevelList();

  if (opts && opts.reload) {
    const first = $("levelList").value;
    if (first) await openLevel(first);
    else blankMap();
  }
}

async function boot() {
  const state = await guard("Loading settings", () => Api.getProject());
  if (!state) return;

  blankMap();                       // something on screen while we load
  await bindProject(state, { reload: true });
}

boot();
