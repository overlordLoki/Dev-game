/* =====================================================================
   The collision-box screen.

   Every box in the game is four fractions — width, height, lift off the
   bottom, sideways shift — of some base rectangle. This screen reproduces
   the game's own draw maths at a size you can actually see, lets you drag
   the box, and writes the numbers back into the source they came from.

   The maths mirrored here:

     objects (Tree.Bounds)      base = one cell
                                sprite drawn at 1.5x the cell, bottom-aligned
     entities (Player.Bounds)   base = the entity's own Width/Height (0.9 cell)
                                sprite fills the base exactly

     bw = baseW * width          bx = (baseW - bw)/2 + baseW * xShift
     bh = baseH * height         by =  baseH - bh    - baseH * footInset

   If the game's draw code changes, this has to change with it — nothing
   enforces the match.
   ===================================================================== */

const BoundsScreen = (() => {

  const CELL = 300;            // on-screen size of one cell, in px
  const KNOBS = [
    { key: "width",     label: "Width",      min: 0.02, max: 1.5 },
    { key: "height",    label: "Height",     min: 0.02, max: 1.5 },
    { key: "footInset", label: "Foot inset", min: -0.5, max: 1 },
    { key: "xShift",    label: "X shift",    min: -1,   max: 1 },
  ];

  let targets = [];
  let warnings = [];
  let current = null;          // the selected target
  let values = {};             // live values, keyed by knob
  let saved = {};              // what's on disk, to detect changes
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

  /* ---------------------------------------------------------- selection */

  function select(target) {
    current = target;
    el("boundsEditor").hidden = !target;
    if (!target) { buildList(); return; }

    values = {};
    saved = {};
    target.knobs.forEach(k => { values[k.key] = k.value; saved[k.key] = k.value; });

    el("boundsFile").textContent = target.file;
    el("basisWord").textContent = target.basis === "cell" ? "cell" : "sprite";

    buildList();
    buildKnobs();
    draw();
    setBoundsStatus("");
  }

  function buildKnobs() {
    const box = el("knobs");
    box.innerHTML = "";

    KNOBS.forEach(spec => {
      const knob = current.knobs.find(k => k.key === spec.key);
      if (!knob) return;

      const label = document.createElement("label");
      label.textContent = spec.label;
      // An inherited value has no line of its own yet; saving will add one.
      if (!knob.declared) {
        label.classList.add("inherited");
        label.title = `Inherited — saving adds ${knob.identifier} to this class`;
      }

      const slider = document.createElement("input");
      slider.type = "range";
      slider.min = spec.min; slider.max = spec.max; slider.step = 0.01;
      slider.value = values[spec.key];
      slider.oninput = () => setValue(spec.key, parseFloat(slider.value));

      const readout = document.createElement("span");
      readout.className = "val";
      readout.id = "val-" + spec.key;
      readout.textContent = values[spec.key].toFixed(2);

      box.append(label, slider, readout);
    });
  }

  function setValue(key, value) {
    values[key] = value;
    const readout = el("val-" + key);
    if (readout) readout.textContent = value.toFixed(2);

    syncSliders();
    draw();
    setBoundsStatus(dirtyBounds() ? "Unsaved changes" : "");
  }

  /** Pushes values back into the sliders after a drag on the stage. */
  function syncSliders() {
    const sliders = el("knobs").querySelectorAll("input[type=range]");
    let i = 0;
    KNOBS.forEach(spec => {
      if (!current.knobs.find(k => k.key === spec.key)) return;
      const slider = sliders[i++];
      if (slider) slider.value = values[spec.key];
      const readout = el("val-" + spec.key);
      if (readout) readout.textContent = values[spec.key].toFixed(2);
    });
  }

  function dirtyBounds() {
    return Object.keys(values).some(k => Math.abs(values[k] - saved[k]) > 0.0005);
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

    // The stage is one cell; the base sits centred in it (identical for objects).
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
    drawBox(base, baseLeft, baseTop);

    el("stageCaption").textContent =
      `${current.name} — ${current.basis === "cell" ? "box is a fraction of the cell (dashed)" :
        "box is a fraction of the entity's own size (blue)"}`;
  }

  function drawSprite(base, baseLeft, baseTop) {
    const img = el("stageSprite");

    if (!current.spriteUrl) { img.hidden = true; return; }
    img.hidden = false;
    img.src = current.spriteUrl;

    if (current.basis === "cell") {
      // Tree.Draw: size = CellSize * 1.5, centred horizontally, sat on the cell's bottom.
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

  /** The box, positioned by exactly the maths the game uses. */
  function boxRect(base) {
    const bw = base.w * values.width;
    const bh = base.h * values.height;
    const bx = (base.w - bw) / 2 + base.w * (values.xShift || 0);
    const by = base.h - bh - base.h * (values.footInset || 0);
    return { bw, bh, bx, by };
  }

  function drawBox(base, baseLeft, baseTop) {
    const { bw, bh, bx, by } = boxRect(base);
    const box = el("stageBox");
    box.style.left = (baseLeft + bx) + "px";
    box.style.top = (baseTop + by) + "px";
    box.style.width = Math.max(2, bw) + "px";
    box.style.height = Math.max(2, bh) + "px";
  }

  /* ------------------------------------------------------------ dragging */

  let drag = null;

  el("stageBox").addEventListener("mousedown", e => {
    if (!current) return;
    e.preventDefault();

    const base = baseSize();
    const resizing = e.target.id === "stageHandle";
    drag = {
      resizing,
      startX: e.clientX,
      startY: e.clientY,
      base,
      from: { ...values },
    };
  });

  window.addEventListener("mousemove", e => {
    if (!drag || !current) return;

    const dx = e.clientX - drag.startX;
    const dy = e.clientY - drag.startY;
    const base = drag.base;

    if (drag.resizing) {
      // The box is centred and bottom-anchored, so a corner drag moves both
      // edges: a pixel right widens it by two pixels' worth of fraction.
      values.width = clamp(drag.from.width + (dx * 2) / base.w, 0.02, 2);
      values.height = clamp(drag.from.height + (dy * 2) / base.h, 0.02, 2);
    } else {
      values.xShift = clamp(drag.from.xShift + dx / base.w, -1, 1);
      // Positive footInset lifts the box, so dragging down reduces it.
      values.footInset = clamp(drag.from.footInset - dy / base.h, -1, 1);
    }

    syncSliders();
    draw();
    setBoundsStatus("Unsaved changes");
  });

  window.addEventListener("mouseup", () => drag = null);

  const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

  /* -------------------------------------------------------------- saving */

  async function save() {
    if (!current) return;

    const result = await guard(`Saving ${current.name}`, () => Api.saveBounds(current.id, values));
    if (!result) return;

    // Line numbers shift when a knob is added, so re-read rather than assume.
    await load();
    setBoundsStatus(
      result.changed.length ? `Wrote ${result.changed.join(", ")} to ${result.file}` : "No changes",
      result.changed.length ? "ok" : "");
  }

  function setBoundsStatus(text, kind) {
    const status = el("boundsStatus");
    status.textContent = text || " ";
    status.className = "hint" + (kind ? " " + kind : "");
  }

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
