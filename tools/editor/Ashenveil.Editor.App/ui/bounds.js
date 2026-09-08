/* =====================================================================
   The collision-box screen.

   A shape is now a list of boxes, each four fractions — x, y (top-left
   corner) and w, h — of some base rectangle. This screen reproduces the
   game's own placement maths at a size you can see, lets you add, remove
   and drag boxes, and writes the list back to Content/bounds.json.

   The base each fraction is measured against:

     objects   base = one cell            sprite drawn at 1.5x, bottom-aligned
     entities  base = the entity's own Width/Height (0.9 cell)

     boxLeft = base.w * x     boxW = base.w * w
     boxTop  = base.h * y     boxH = base.h * h

   This mirrors Box.ToRectangle in the game. If that changes, this has to
   change with it — nothing enforces the match.
   ===================================================================== */

const BoundsScreen = (() => {

  const CELL = 300;            // on-screen size of one cell, in px
  const FIELDS = [
    { key: "x", label: "X",      min: -1,   max: 2 },
    { key: "y", label: "Y",      min: -1,   max: 2 },
    { key: "w", label: "Width",  min: 0.02, max: 2 },
    { key: "h", label: "Height", min: 0.02, max: 2 },
  ];
  // A fresh box lands centred-ish so it's visible and easy to grab.
  const DEFAULT_BOX = { x: 0.30, y: 0.45, w: 0.40, h: 0.40 };

  let targets = [];
  let warnings = [];
  let current = null;          // the selected target
  let boxes = [];              // live boxes for the current target
  let saved = "[]";            // snapshot of what's on disk, to detect changes
  let selected = -1;           // index of the selected box, or -1
  let loaded = false;

  const el = id => document.getElementById(id);

  /* ------------------------------------------------------------ loading */

  async function load() {
    const data = await guard("Reading collision boxes", () => Api.getBounds());
    if (!data) return;

    targets = data.targets || [];
    warnings = data.warnings || [];
    loaded = true;

    buildList();
    buildWarnings();

    // Keep the current selection across a reload if it still exists.
    const keep = current && targets.find(t => t.id === current.id);
    select(keep || targets[0] || null);
  }

  function buildList() {
    const list = el("boundsList");
    list.innerHTML = "";

    let kind = null;
    targets.forEach(t => {
      if (t.kind !== kind) {
        kind = t.kind;
        const heading = document.createElement("div");
        heading.className = "hint";
        heading.style.margin = "6px 0 2px";
        heading.textContent = kind === "object" ? "Objects" : "Entities";
        list.appendChild(heading);
      }

      const row = document.createElement("div");
      row.className = "swatch" + (current && t.id === current.id ? " selected" : "");

      const chip = document.createElement("span");
      chip.className = "chip contain";
      if (t.spriteUrl) chip.style.backgroundImage = `url("${t.spriteUrl}")`;
      else chip.textContent = "?";
      // A sheet would show every frame at once; show only the first.
      if (t.spriteFrames > 1) {
        chip.style.backgroundSize = (t.spriteFrames * 100) + "% 100%";
        chip.style.backgroundPosition = "0 0";
      }

      const name = document.createElement("span");
      name.className = "name";
      name.textContent = t.name;

      row.append(chip, name);
      row.onclick = () => select(t);
      list.appendChild(row);
    });
  }

  function buildWarnings() {
    const list = el("boundsWarningList");
    list.innerHTML = "";

    // "Missing art" is noise here — the box is still tunable without a picture.
    const relevant = warnings.filter(w => !w.startsWith("Missing art:"));
    relevant.forEach(w => {
      const li = document.createElement("li");
      li.textContent = w;
      list.appendChild(li);
    });
    el("boundsWarnings").hidden = relevant.length === 0;
  }

  /* --------------------------------------------------- target selection */

  function select(target) {
    current = target;
    el("boundsEditor").hidden = !target;
    if (!target) { buildList(); return; }

    boxes = (target.boxes || []).map(b => ({ x: b.x, y: b.y, w: b.w, h: b.h }));
    saved = JSON.stringify(boxes);
    selected = boxes.length ? 0 : -1;

    el("boundsFile").textContent = target.file;
    el("basisWord").textContent = target.basis === "cell" ? "cell" : "sprite";

    buildList();
    buildBoxList();
    buildFields();
    draw();
    setBoundsStatus("");
  }

  /* ------------------------------------------------------- the box list */

  function buildBoxList() {
    const list = el("boxList");
    list.innerHTML = "";

    if (boxes.length === 0) {
      const empty = document.createElement("div");
      empty.className = "empty";
      empty.textContent = "No boxes yet — add one.";
      list.appendChild(empty);
      return;
    }

    boxes.forEach((_, i) => {
      const row = document.createElement("div");
      row.className = "boxrow" + (i === selected ? " selected" : "");

      const name = document.createElement("span");
      name.className = "name";
      name.textContent = "Box " + (i + 1);

      const del = document.createElement("button");
      del.className = "del";
      del.title = "Remove this box";
      del.innerHTML = "&times;";
      del.onclick = e => { e.stopPropagation(); removeBox(i); };

      row.append(name, del);
      row.onclick = () => selectBox(i);
      list.appendChild(row);
    });
  }

  function selectBox(i) {
    selected = i;
    buildBoxList();
    buildFields();
    draw();
  }

  function addBox() {
    boxes.push({ ...DEFAULT_BOX });
    selected = boxes.length - 1;
    buildBoxList();
    buildFields();
    draw();
    setBoundsStatus(dirty() ? "Unsaved changes" : "");
  }

  function removeBox(i) {
    boxes.splice(i, 1);
    if (selected >= boxes.length) selected = boxes.length - 1;
    buildBoxList();
    buildFields();
    draw();
    setBoundsStatus(dirty() ? "Unsaved changes" : "");
  }

  /* ------------------------------------------------ fields for one box */

  function buildFields() {
    const box = el("boxFields");
    box.innerHTML = "";
    box.hidden = selected < 0;
    if (selected < 0) return;

    const b = boxes[selected];
    FIELDS.forEach(spec => {
      const label = document.createElement("label");
      label.textContent = spec.label;

      const slider = document.createElement("input");
      slider.type = "range";
      slider.min = spec.min; slider.max = spec.max; slider.step = 0.01;
      slider.value = b[spec.key];
      slider.oninput = () => setField(spec.key, parseFloat(slider.value));

      const readout = document.createElement("span");
      readout.className = "val";
      readout.id = "val-" + spec.key;
      readout.textContent = b[spec.key].toFixed(2);

      box.append(label, slider, readout);
    });
  }

  function setField(key, value) {
    boxes[selected][key] = value;
    const readout = el("val-" + key);
    if (readout) readout.textContent = value.toFixed(2);

    draw();
    setBoundsStatus(dirty() ? "Unsaved changes" : "");
  }

  /** Pushes values back into the sliders after a drag on the stage. */
  function syncFields() {
    if (selected < 0) return;
    const b = boxes[selected];
    const sliders = el("boxFields").querySelectorAll("input[type=range]");
    FIELDS.forEach((spec, i) => {
      if (sliders[i]) sliders[i].value = b[spec.key];
      const readout = el("val-" + spec.key);
      if (readout) readout.textContent = b[spec.key].toFixed(2);
    });
  }

  function dirty() {
    return JSON.stringify(boxes) !== saved;
  }

  /* --------------------------------------------------------------- draw */

  /** The base rectangle the fractions are measured against, in stage pixels. */
  function baseSize() {
    // Objects measure against a whole cell; entities against their own drawn size.
    const scale = current.basis === "cell" ? 1 : current.spriteScale;
    return { w: CELL * scale, h: CELL * scale };
  }

  function draw() {
    const stage = el("stage");
    const base = baseSize();

    stage.style.width = CELL + "px";
    stage.style.height = CELL + "px";

    const baseLeft = (CELL - base.w) / 2;
    const baseTop = (CELL - base.h) / 2;

    const baseEl = el("stageBase");
    baseEl.style.left = baseLeft + "px";
    baseEl.style.top = baseTop + "px";
    baseEl.style.width = base.w + "px";
    baseEl.style.height = base.h + "px";

    drawSprite(base, baseLeft, baseTop);
    drawBoxes(base, baseLeft, baseTop);

    el("stageCaption").textContent =
      `${current.name} — ${current.basis === "cell" ? "boxes are fractions of the cell" :
        "boxes are fractions of the entity's own size"} (${boxes.length} box${boxes.length === 1 ? "" : "es"})`;
  }

  function drawSprite(base, baseLeft, baseTop) {
    const img = el("stageSprite");

    if (!current.spriteUrl) { img.hidden = true; return; }
    img.hidden = false;
    img.src = current.spriteUrl;

    if (current.basis === "cell") {
      // GameObject.Draw: size = CellSize * 1.5, centred horizontally, sat on the bottom.
      const size = CELL * current.spriteScale;
      img.style.width = size + "px";
      img.style.height = size + "px";
      img.style.left = ((CELL - size) / 2) + "px";
      img.style.top = (CELL - size) + "px";
      img.style.clipPath = "";
    } else {
      // Player.Draw: the frame fills Width x Height exactly.
      img.style.width = base.w + "px";
      img.style.height = base.h + "px";
      img.style.left = baseLeft + "px";
      img.style.top = baseTop + "px";
      // A sheet is many frames wide; show the first by widening and clipping.
      if (current.spriteFrames > 1) {
        img.style.width = (base.w * current.spriteFrames) + "px";
        img.style.clipPath = `inset(0 ${(current.spriteFrames - 1) * base.w}px 0 0)`;
      } else {
        img.style.clipPath = "";
      }
    }
  }

  /** One .box div per box; the selected one gets a resize handle. */
  function drawBoxes(base, baseLeft, baseTop) {
    const host = el("stageBoxes");
    host.innerHTML = "";

    boxes.forEach((b, i) => {
      const div = document.createElement("div");
      div.className = "box" + (i === selected ? " selected" : "");
      div.style.left = (baseLeft + base.w * b.x) + "px";
      div.style.top = (baseTop + base.h * b.y) + "px";
      div.style.width = Math.max(2, base.w * b.w) + "px";
      div.style.height = Math.max(2, base.h * b.h) + "px";

      if (i === selected) {
        const handle = document.createElement("div");
        handle.className = "handle";
        div.appendChild(handle);
      }

      div.addEventListener("mousedown", e => startDrag(e, i));
      host.appendChild(div);
    });
  }

  /* ------------------------------------------------------------ dragging */

  let drag = null;

  function startDrag(e, i) {
    e.preventDefault();
    // Clicking an unselected box just selects it; the next drag moves it.
    if (i !== selected) { selectBox(i); return; }

    drag = {
      resizing: e.target.classList.contains("handle"),
      startX: e.clientX,
      startY: e.clientY,
      base: baseSize(),
      from: { ...boxes[selected] },
    };
  }

  window.addEventListener("mousemove", e => {
    if (!drag || selected < 0) return;

    const dx = e.clientX - drag.startX;
    const dy = e.clientY - drag.startY;
    const base = drag.base;
    const b = boxes[selected];

    if (drag.resizing) {
      b.w = clamp(drag.from.w + dx / base.w, 0.02, 2);
      b.h = clamp(drag.from.h + dy / base.h, 0.02, 2);
    } else {
      b.x = clamp(drag.from.x + dx / base.w, -1, 2);
      b.y = clamp(drag.from.y + dy / base.h, -1, 2);
    }

    syncFields();
    draw();
    setBoundsStatus("Unsaved changes");
  });

  window.addEventListener("mouseup", () => drag = null);

  const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

  /* -------------------------------------------------------------- saving */

  async function save() {
    if (!current) return;

    const result = await guard(`Saving ${current.name}`, () => Api.saveBounds(current.id, boxes));
    if (!result) return;

    await load();
    setBoundsStatus(`Saved ${result.count} box${result.count === 1 ? "" : "es"} to ${result.file}`, "ok");
  }

  function setBoundsStatus(text, kind) {
    const status = el("boundsStatus");
    status.textContent = text || " ";
    status.className = "hint" + (kind ? " " + kind : "");
  }

  el("addBox").onclick = addBox;
  el("saveBounds").onclick = save;
  el("revertBounds").onclick = load;

  return {
    show() { if (!loaded) load(); },
    reload() { if (loaded) load(); },
  };
})();

/* ------------------------------------------------------------ screen tabs */

const SCREENS = {
  levels: { tab: "tabLevels", panel: "levelsPanel", view: "levelsView" },
  bounds: { tab: "tabBounds", panel: "boundsPanel", view: "boundsView" },
  assets: { tab: "tabAssets", panel: "assetsPanel", view: "assetsView" },
};

function setScreen(name) {
  for (const [key, ids] of Object.entries(SCREENS)) {
    const on = key === name;
    document.getElementById(ids.tab).classList.toggle("active", on);
    document.getElementById(ids.panel).hidden = !on;
    document.getElementById(ids.view).hidden = !on;
  }

  if (name === "bounds") BoundsScreen.show();
  else if (name === "assets") AssetsScreen.show();
  else render();   // the grid needs a re-fit after being hidden
}

for (const [key, ids] of Object.entries(SCREENS))
  document.getElementById(ids.tab).onclick = () => setScreen(key);
