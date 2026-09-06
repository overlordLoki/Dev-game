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

let mode = "tiles";             // "tiles" | "objects"
let selectedTile = null;
let selectedObject = null;
let objRotation = 0;            // applied to newly placed objects
let tileRotation = 0;           // applied to newly painted tiles
let painting = false;

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

  // drop objects that fell outside the new bounds
  objects = objects.filter(o => o.col < cols && o.row < rows);

  markDirty();
  render();
}

/* --------------------------------------------------------------- render */

function objectAt(c, r) { return objects.find(o => o.col === c && o.row === r); }

function cellEl(x, y) { return gridEl.querySelector(`.cell[data-x="${x}"][data-y="${y}"]`); }

// Build the inner content of one cell (tile layer + optional object layer).
function renderCell(x, y) {
  const el = cellEl(x, y);
  if (!el) return;
  el.innerHTML = "";
  el.style.background = "";

  // --- tile layer ---
  const tile = tileById[tiles[y][x]];
  if (tile && tile.url) {
    const img = document.createElement("img");
    img.className = "tile";
    img.src = tile.url;
    img.style.transform = `rotate(${tileRot[y][x] || 0}deg)`;
    el.appendChild(img);
  } else {
    // No art mapped (or an unknown id from an older file) — show it, don't hide it.
    el.style.background = "#000";
    el.title = tile ? `${tile.name} (no art)` : `Unknown tile "${tiles[y][x]}"`;
  }

  // --- object layer ---
  const o = objectAt(x, y);
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

  $("cols").value = cols;
  $("rows").value = rows;
  updateJson();
  updateTitle();
  updateBrush();
}

window.addEventListener("resize", () => { if (tiles.length) render(); });

/* ---------------------------------------------------------- held brush */

const brushEl = $("brush");
const brushImg = $("brushImg");
const brushRotEl = $("brushRot");

/** What the next click will place: its art and the rotation it lands with. */
function currentBrush() {
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

gridEl.addEventListener("mousemove", e => {
  if (!currentBrush().url) return;
  moveBrush(e);
  brushEl.hidden = false;
});

// Only meaningful over the grid; anywhere else it's just clutter.
gridEl.addEventListener("mouseleave", () => brushEl.hidden = true);

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

gridEl.addEventListener("mousedown", e => {
  const el = e.target.closest(".cell");
  if (!el) return;
  const x = +el.dataset.x, y = +el.dataset.y;

  if (mode === "tiles") { painting = true; paintCell(x, y); }
  else if (e.button === 2) removeObject(x, y);
  else placeObject(x, y);
});

gridEl.addEventListener("mouseover", e => {
  if (!painting) return;
  const el = e.target.closest(".cell");
  if (el) paintCell(+el.dataset.x, +el.dataset.y);
});

window.addEventListener("mouseup", () => painting = false);
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

function setMode(m) {
  mode = m;
  $("modeTiles").classList.toggle("active", m === "tiles");
  $("modeObjects").classList.toggle("active", m === "objects");
  $("tilePanel").hidden = m !== "tiles";
  $("objectPanel").hidden = m !== "objects";
  updateBrush();
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

/** Turns whichever brush is active. step is +90 or -90. */
function rotateBrush(step) {
  if (mode === "objects") rotateObjects(step);
  else rotateTiles(step);
}

$("rotateBtn").onclick = () => rotateObjects(90);
$("tileRotateBtn").onclick = () => rotateTiles(90);

// Scrolling over the grid rotates instead of scrolling the canvas — the wheel is
// the natural gesture for this, and there's nothing worth scrolling to under it.
gridEl.addEventListener("wheel", e => {
  e.preventDefault();
  rotateBrush(e.deltaY > 0 ? 90 : -90);
}, { passive: false });

window.addEventListener("keydown", e => {
  // Don't steal keys from the level-name prompt or the number inputs.
  if (e.target.matches("input, select, textarea")) return;

  if (e.key.toLowerCase() === "r") {
    rotateBrush(e.shiftKey ? -90 : 90);
    return;
  }

  if (e.key.toLowerCase() === "s" && (e.ctrlKey || e.metaKey)) {
    e.preventDefault();
    save();
  }
});

/* ------------------------------------------------------- the level file */

function buildMap() {
  return { cols, rows, tiles, rotations: tileRot, objects };
}

function updateJson() {
  $("json").value = JSON.stringify(buildMap(), null, 2);
}

function loadMap(map) {
  cols = map.cols;
  rows = map.rows;
  tiles = map.tiles;
  objects = map.objects || [];
  tileRot = map.rotations || newGrid(cols, rows, 0);   // older files have no rotations
  dirty = false;
  render();
}

function blankMap() {
  cols = 8; rows = 5;
  tiles = newTiles(cols, rows);
  tileRot = newGrid(cols, rows, 0);
  objects = [];
  currentLevel = null;
  dirty = false;
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
