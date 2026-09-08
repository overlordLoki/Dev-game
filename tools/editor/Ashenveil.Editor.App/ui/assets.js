/* =====================================================================
   The assets screen.

   Adding art to the game means three things must agree: the PNG exists, the
   .mgcb builds it, and Assets.cs names it. Miss the .mgcb step and the game
   crashes at load with a bare "file not found". This screen shows the gaps and
   fixes the one that matters — unregistered sprites — by appending the standard
   block to the .mgcb, so that file never has to be hand-edited.

   All the work is in the backend (AssetAudit / MgcbRegistrar). This just calls
   /assets/audit, draws the three lists, and calls /assets/register.
   ===================================================================== */
const AssetsScreen = (() => {
  const $ = id => document.getElementById(id);

  // Turns "Sprites/Tiles/turn_dirt_road" into just "Tiles/turn_dirt_road" for display.
  const short = p => p.replace(/^Sprites\//, "");

  function list(el, items, empty) {
    el.innerHTML = "";
    if (items.length === 0) {
      const li = document.createElement("li");
      li.className = "muted";
      li.textContent = empty;
      el.appendChild(li);
      return;
    }
    for (const p of items) {
      const li = document.createElement("li");
      li.textContent = short(p);
      el.appendChild(li);
    }
  }

  function paint(report) {
    const nReg = report.notRegistered.length;
    const nMiss = report.missingFile.length;
    const nUnused = report.notInAssets.length;

    // Sidebar sections only appear when they have something to say.
    $("assetsUnregistered").hidden = nReg === 0;
    $("assetsMissing").hidden      = nMiss === 0;
    $("assetsUnused").hidden       = nUnused === 0;

    list($("unregisteredList"), report.notRegistered, "");
    list($("missingList"),      report.missingFile,   "");
    list($("unusedList"),       report.notInAssets,   "");

    $("assetsSummary").textContent =
      nReg === 0
        ? "Every sprite on disk is in the pipeline. ✓"
        : `${nReg} sprite${nReg === 1 ? "" : "s"} on disk aren't built yet.`;

    // The main-area board — a plain, readable status rather than a canvas.
    $("assetsHero").innerHTML = `
      <div class="assets-stat ${nReg ? "bad" : "good"}">
        <div class="n">${nReg}</div><div class="k">not in the pipeline</div>
      </div>
      <div class="assets-stat ${nMiss ? "bad" : ""}">
        <div class="n">${nMiss}</div><div class="k">missing files</div>
      </div>
      <div class="assets-stat">
        <div class="n">${nUnused}</div><div class="k">not wired up</div>
      </div>`;
  }

  async function load() {
    $("assetsStatus").textContent = "Scanning…";
    try {
      paint(await Api.auditAssets());
      $("assetsStatus").innerHTML = "&nbsp;";
    } catch (e) {
      $("assetsStatus").textContent = e.message;   // usually "no game folder selected yet"
    }
  }

  async function registerAll() {
    $("assetsStatus").textContent = "Registering…";
    try {
      const result = await Api.registerAssets();      // no list => everything unregistered
      paint(result.audit);                             // backend hands back the fresh state
      const n = result.added.length;
      $("assetsStatus").textContent =
        n === 0 ? "Nothing to add." : `Added ${n} to the .mgcb. Rebuild to pick them up.`;
    } catch (e) {
      $("assetsStatus").textContent = e.message;
    }
  }

  $("registerAll").onclick = registerAll;
  $("refreshAssets").onclick = load;

  return {
    show() { load(); },
    reload() { load(); },
  };
})();
