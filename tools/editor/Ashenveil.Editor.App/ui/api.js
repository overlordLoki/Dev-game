/* =====================================================================
   The bridge to the backend.

   Every call is one JSON message out and one JSON message back, correlated
   by id. The shape is deliberately HTTP-like — api.get("/levels"), api.put(
   "/levels/Medows", map) — so if the native shell is ever swapped for a
   local server, only the four lines that touch window.external change.
   ===================================================================== */
const Api = (() => {
  const pending = new Map();
  let nextId = 0;

  window.external.receiveMessage(raw => {
    let response;
    try {
      response = JSON.parse(raw);
    } catch {
      return;   // not ours
    }

    const waiting = pending.get(response.id);
    if (!waiting) return;
    pending.delete(response.id);

    if (response.ok) waiting.resolve(response.body);
    else waiting.reject(new Error(response.error || "Unknown backend error"));
  });

  function send(method, path, body) {
    return new Promise((resolve, reject) => {
      const id = String(++nextId);
      pending.set(id, { resolve, reject });
      window.external.sendMessage(JSON.stringify({ id, method, path, body: body ?? null }));
    });
  }

  return {
    // --- project ---
    getProject:  ()      => send("GET",  "/project"),
    pickProject: ()      => send("POST", "/project/browse"),

    // --- palette, read from the game's own source ---
    getCatalogue: ()     => send("GET",  "/catalogue"),

    // --- collision boxes, read from and written back to Content/bounds.json ---
    getBounds:  ()          => send("GET", "/bounds"),
    // The whole box list is sent each save, so this covers add, remove and edit.
    saveBounds: (id, boxes) => send("PUT", "/bounds/" + encodeURIComponent(id), { boxes }),

    // --- assets: content-pipeline audit + auto-register into the .mgcb ---
    auditAssets:    ()    => send("GET",  "/assets/audit"),
    // paths omitted => register everything currently unregistered
    registerAssets: paths => send("POST", "/assets/register", paths ? { paths } : null),

    // --- levels ---
    listLevels:  ()          => send("GET",    "/levels"),
    readLevel:   name        => send("GET",    "/levels/" + encodeURIComponent(name)),
    saveLevel:   (name, map) => send("PUT",    "/levels/" + encodeURIComponent(name), map),
    deleteLevel: name        => send("DELETE", "/levels/" + encodeURIComponent(name)),
  };
})();
