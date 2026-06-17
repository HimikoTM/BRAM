(function () {
  "use strict";

  const host = window.chrome && window.chrome.webview;

  if (!host) {
    console.warn("[bridge] not running inside WebView2 — using mock backend.");
    const listeners = new Map();
    const now = Date.now();
    const demoAccounts = [
      { id: "1", label: "Main", username: "xX_NeonRider_Xx", userId: "1842993021", group: "Main", notes: "Primary account.", status: "In game", avatarUrl: "", lastUsed: new Date(now - 2 * 60000).toISOString(), dateAdded: new Date(now).toISOString() },
      { id: "2", label: "Builder", username: "BuildMaster_RBX", userId: "7720114553", group: "Builders", notes: "Studio rig.", status: "Online", avatarUrl: "", lastUsed: new Date(now - 18 * 60000).toISOString(), dateAdded: new Date(now).toISOString() },
      { id: "3", label: "Trade", username: "PixelTrader_99", userId: "5510928374", group: "Trading", notes: "Limiteds holder.", status: "Offline", avatarUrl: "", lastUsed: new Date(now - 3 * 3600000).toISOString(), dateAdded: new Date(now).toISOString() },
      { id: "4", label: "", username: "GreenLeaf_777", userId: "3344120876", group: "General", notes: "", status: "In Studio", avatarUrl: "", lastUsed: null, dateAdded: new Date(now).toISOString() }
    ];
    const demo = {
      mode: "dpapi", locked: false, needsSetup: false,
      state: {
        accounts: demoAccounts,
        settings: { multiInstance: true, placeId: 0, autoRefreshSeconds: 60 },
        vaultMode: "dpapi", multiInstanceActive: false, runningClients: 0,
        log: [
          { time: "09:41:02", tag: "BOOT", text: "Vault decrypted — 4 session(s) restored.", color: "#2dff8a" },
          { time: "09:41:02", tag: "INFO", text: "Running outside WebView2 — demo data.", color: "#5d7a6c" }
        ]
      }
    };
    window.Bridge = {
      invoke(method) {
        if (method === "init") {
          const v = new URLSearchParams(location.search).get("vault");
          if (v === "setup") return Promise.resolve({ mode: "none", locked: false, needsSetup: true, corrupt: false });
          if (v === "locked") return Promise.resolve({ mode: "password", locked: true, needsSetup: false, corrupt: false });
          if (v === "corrupt") return Promise.resolve({ mode: "corrupt", locked: false, needsSetup: false, corrupt: true });
          return Promise.resolve(demo);
        }
        if (method === "getState") return Promise.resolve(demo.state);
        if (method === "unlock") return Promise.resolve({ ok: true, state: demo.state });
        if (method === "createVault" || method === "resetVault") return Promise.resolve({ ok: true, mode: "dpapi", state: demo.state });
        return Promise.resolve(null);
      },
      on(event, fn) {
        if (!listeners.has(event)) listeners.set(event, new Set());
        listeners.get(event).add(fn);
        return () => listeners.get(event).delete(fn);
      },
      _emit(event, data) { const s = listeners.get(event); if (s) s.forEach((fn) => fn(data)); }
    };
    return;
  }

  const pending = new Map();
  const listeners = new Map();

  host.addEventListener("message", (e) => {
    const m = e.data;
    if (!m || typeof m !== "object") return;

    if (m.kind === "res") {
      const p = pending.get(m.id);
      if (!p) return;
      pending.delete(m.id);
      if (m.ok) p.resolve(m.result);
      else p.reject(new Error(m.error || "Request failed"));
    } else if (m.kind === "evt") {
      const set = listeners.get(m.event);
      if (set) set.forEach((fn) => { try { fn(m.data); } catch (err) { console.error(err); } });
    }
  });

  function uuid() {
    return (crypto.randomUUID && crypto.randomUUID()) ||
      "id-" + Date.now() + "-" + Math.floor(Math.random() * 1e9);
  }

  window.Bridge = {
    invoke(method, args) {
      return new Promise((resolve, reject) => {
        const id = uuid();
        pending.set(id, { resolve, reject });
        host.postMessage({ kind: "req", id, method, args: args || {} });
      });
    },
    on(event, fn) {
      if (!listeners.has(event)) listeners.set(event, new Set());
      listeners.get(event).add(fn);
      return () => { const s = listeners.get(event); if (s) s.delete(fn); };
    }
  };
})();
