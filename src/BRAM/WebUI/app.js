(function () {
  "use strict";

  const $ = (sel, root) => (root || document).querySelector(sel);
  const appEl = document.getElementById("app");
  const drawerRoot = document.getElementById("drawer-root");
  const toastsEl = document.getElementById("toasts");
  const bootRoot = document.getElementById("boot-root");

  const LOGO_SVG = `<svg viewBox="0 0 24 24" width="24" height="24" xmlns="http://www.w3.org/2000/svg"><path fill="#2dff8a" d="M18.926 23.998 0 18.892 5.075.002 24 5.108ZM15.348 10.09l-5.282-1.453-1.414 5.273 5.282 1.453z"/></svg>`;
  const SEARCH_SVG = `<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="#4f6f61" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="m20 20-3-3"/></svg>`;
  const LOCK_SVG = `<svg viewBox="0 0 24 24" width="38" height="38" fill="none" stroke="#2dff8a" stroke-width="1.8"><rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg>`;
  const KEY_SVG = `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="7.5" cy="15.5" r="5.5"/><path d="m21 2-9.6 9.6"/><path d="m15.5 7.5 3 3L22 7l-3-3"/></svg>`;
  const REFRESH_SVG = `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/></svg>`;
  const TRASH_SVG = `<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></svg>`;
  const CLOSE_SVG = `<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18M6 6l12 12"/></svg>`;

  const state = {
    booting: true,
    query: "",
    placeId: "",
    multi: false,
    multiActive: false,
    activeGroup: "All",
    selectedId: null,
    accounts: [],
    log: [],
    vault: { mode: "none", locked: false, needsSetup: false },
    settings: { multiInstance: true, placeId: 0, autoRefreshSeconds: 60 }
  };

  let mounted = false;
  let searchInput = null;
  let placeInput = null;
  let placeTimer = null;
  let drawerBuiltFor = null;
  let drawerAvatar = "";

  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const find = (id) => state.accounts.find((a) => a.id === id);

  function statusMeta(s) {
    switch (s) {
      case "In game":   return { color: "#2dff8a", bar: 1 };
      case "Online":    return { color: "#5affb0", bar: 1 };
      case "In Studio": return { color: "#ffc24d", bar: 1 };
      case "Offline":   return { color: "#536b60", bar: .25 };
      default:          return { color: "#7d99a3", bar: .4 };
    }
  }

  function grad(name) {
    name = name || "?";
    let h = 0;
    for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360;
    return `linear-gradient(135deg,hsl(${h} 90% 62%),hsl(${(h + 42) % 360} 95% 48%))`;
  }

  function relativeTime(iso) {
    if (!iso) return "never launched";
    const t = Date.parse(iso);
    if (isNaN(t)) return "—";
    const s = Math.floor((Date.now() - t) / 1000);
    if (s < 45) return "just now";
    const m = Math.floor(s / 60);
    if (m < 60) return m + "m ago";
    const h = Math.floor(m / 60);
    if (h < 24) return h + "h ago";
    const d = Math.floor(h / 24);
    if (d === 1) return "yesterday";
    if (d < 30) return d + "d ago";
    return new Date(t).toLocaleDateString();
  }

  function decorate(a) {
    const m = statusMeta(a.status);
    return {
      ...a,
      display: a.label ? `${a.label}  (${a.username})` : a.username,
      initial: (a.label || a.username || "?").charAt(0).toUpperCase(),
      grad: grad(a.username || a.id),
      ring: `linear-gradient(135deg,${m.color},rgba(45,255,138,.15))`,
      statusColor: m.color,
      barOpacity: m.bar,
      lastUsedText: relativeTime(a.lastUsed)
    };
  }

  function faceHtml(a) {
    return a.avatarUrl
      ? `<div class="face"><img src="${esc(a.avatarUrl)}" alt=""></div>`
      : `<div class="face" style="background:${a.grad}">${esc(a.initial)}</div>`;
  }

  function toast(text) {
    const el = document.createElement("div");
    el.className = "toast";
    el.style.pointerEvents = "auto";
    el.innerHTML = `<span class="tx">${esc(text || "")}</span>`;
    toastsEl.appendChild(el);
    setTimeout(() => {
      el.style.transition = "opacity .3s ease, transform .3s ease";
      el.style.opacity = "0";
      el.style.transform = "translateX(40px)";
      setTimeout(() => el.remove(), 320);
    }, 3200);
  }

  function filtered() {
    const q = state.query.trim().toLowerCase();
    const grp = state.activeGroup;
    return state.accounts.filter((a) => {
      if (grp !== "All" && a.group !== grp) return false;
      if (!q) return true;
      return ((a.label || "") + " " + (a.username || "") + " " + (a.group || "") + " " + (a.status || ""))
        .toLowerCase().includes(q);
    });
  }

  function headerHtml() {
    const mode = state.vault.mode;
    const pill = mode === "password" ? "VAULT&nbsp;UNLOCKED" : (mode === "dpapi" ? "DPAPI&nbsp;VAULT" : "VAULT");
    const lock = mode === "password" ? `<button class="lock-btn" data-act="lock">Lock</button>` : "";
    return `<div class="header">
      <div class="logo">${LOGO_SVG}</div>
      <div class="brand">
        <div class="brand-title">BROBLOX <span>ACCOUNT MANAGER</span></div>
        <div class="brand-sub">Sessions · Multi-launch · Local DPAPI vault</div>
      </div>
      <div class="spacer"></div>
      <div class="pill"><span class="dot"></span>${pill}</div>
      <button class="lock-btn icon-only" data-act="vaultSettings" title="Vault password">${KEY_SVG}</button>
      ${lock}
    </div>`;
  }

  function statsHtml() {
    const all = state.accounts;
    const online = all.filter((a) => a.status === "Online" || a.status === "In game").length;
    const ingame = all.filter((a) => a.status === "In game").length;
    const groups = new Set(all.map((a) => a.group)).size;
    const cards = [
      { label: "Accounts", value: all.length, unit: "saved",   color: "#eafff3", glow: "none", bar: "#2dff8a" },
      { label: "Online",   value: online,     unit: "now",     color: "#2dff8a", glow: "0 0 16px rgba(45,255,138,.7)", bar: "#2dff8a" },
      { label: "In game",  value: ingame,     unit: "playing", color: "#5affb0", glow: "0 0 14px rgba(90,255,176,.6)", bar: "#5affb0" },
      { label: "Groups",   value: groups,     unit: "tags",    color: "#eafff3", glow: "none", bar: "rgba(45,255,138,.4)" }
    ];
    return `<div class="stats">` + cards.map((s) => `
      <div class="stat">
        <div class="bar" style="background:${s.bar}"></div>
        <div class="label">${s.label}</div>
        <div class="row"><div class="value" style="color:${s.color};text-shadow:${s.glow}">${s.value}</div><div class="unit">${s.unit}</div></div>
      </div>`).join("") + `</div>`;
  }

  function toolbarHtml() {
    return `<div class="toolbar">
      <button class="btn-primary" data-act="addLogin">Add (login)</button>
      <button class="btn-ghost" data-act="addCookie">Add (cookie)</button>
      <button class="btn-ghost" data-act="refreshAll">Refresh all</button>
      <button class="btn-ghost" data-act="openClient">Open client</button>
      <div class="search">${SEARCH_SVG}<input id="search" placeholder="Search accounts, groups, usernames…"></div>
      <div class="toggle" id="multi"><div class="track"><div class="knob"></div></div><span>Multi-instance</span></div>
      <div class="place"><label>Place&nbsp;ID</label><input id="place" placeholder="optional"></div>
    </div>`;
  }

  function tabsHtml() {
    const all = state.accounts;
    const names = ["All", ...new Set(all.map((a) => a.group))];
    return `<div class="tabs">` + names.map((name) => {
      const active = state.activeGroup === name;
      const count = name === "All" ? all.length : all.filter((a) => a.group === name).length;
      return `<div class="tab${active ? " active" : ""}" data-tab="${esc(name)}">${esc(name)}<span class="count">${count}</span></div>`;
    }).join("") + `</div>`;
  }

  function listHeadHtml() {
    return `<div class="list-head"><div class="cap">Accounts</div><div class="num">[ ${filtered().length} ]</div><div class="rule"></div></div>`;
  }

  function cardHtml(raw) {
    const a = decorate(raw);
    return `<div class="card" data-card="${a.id}">
        <div class="statusbar" style="background:${a.statusColor};box-shadow:0 0 12px ${a.statusColor};opacity:${a.barOpacity}"></div>
        <div class="avatar" style="background:${a.ring}">${faceHtml(a)}<div class="badge" style="background:${a.statusColor};box-shadow:0 0 8px ${a.statusColor}"></div></div>
        <div class="info">
          <div class="name">${esc(a.display)}</div>
          <div class="meta">
            <span class="chip"><span class="sq"></span>${esc(a.group)}</span>
            <span class="status" style="color:${a.statusColor}"><span class="sd" style="background:${a.statusColor};box-shadow:0 0 7px ${a.statusColor}"></span>${esc(a.status)}</span>
            <span class="lastused">·&nbsp;${esc(a.lastUsedText)}</span>
          </div>
        </div>
        <div class="actions">
          <button class="act-launch" data-action="launch" data-id="${a.id}">Launch</button>
          <button class="icon-btn" data-action="refresh" data-id="${a.id}" title="Refresh status">${REFRESH_SVG}</button>
          <button class="icon-btn danger" data-action="delete" data-id="${a.id}" title="Remove">${TRASH_SVG}</button>
        </div>
      </div>`;
  }

  function cardsHtml() {
    const list = filtered();
    if (list.length === 0) return `<div class="cards"><div class="empty">NO ACCOUNTS MATCH FILTER</div></div>`;
    return `<div class="cards">` + list.map(cardHtml).join("") + `</div>`;
  }

  function consoleShellHtml() {
    return `<div class="console">
      <div class="console-head">
        <span class="lights"><span style="background:#ff5f57"></span><span style="background:#febc2e"></span><span style="background:#28c840"></span></span>
        <span class="cap">activity.log</span><div class="spacer"></div>
        <span class="clear" data-act="clearLog">clear</span>
      </div>
      <div class="console-body" id="console-body"></div>
    </div>`;
  }

  function renderLog() {
    const body = $("#console-body");
    if (!body) return;
    body.innerHTML = state.log.map((l) =>
      `<div class="log-row"><span class="t">${esc(l.time)}</span><span class="g" style="color:${esc(l.color)}">${esc(l.tag)}</span><span class="x">${esc(l.text)}</span></div>`
    ).join("") + `<div class="caret"><span class="p">ram&nbsp;❯</span><span class="b"></span></div>`;
    body.scrollTop = body.scrollHeight;
  }

  function mountIfNeeded() {
    if (mounted) return;
    appEl.innerHTML =
      `<div id="header-c"></div><div id="stats-c"></div>${toolbarHtml()}` +
      `<div id="tabs-c"></div><div id="listhead-c"></div><div id="cards-c"></div>${consoleShellHtml()}`;
    searchInput = $("#search");
    placeInput = $("#place");
    attachHandlers();
    mounted = true;
  }

  function update() {
    if (!mounted) return;
    $("#header-c").innerHTML = headerHtml();
    $("#stats-c").innerHTML = statsHtml();
    $("#tabs-c").innerHTML = tabsHtml();
    $("#listhead-c").innerHTML = listHeadHtml();
    $("#cards-c").innerHTML = cardsHtml();
    renderLog();
    const m = $("#multi");
    if (m) m.classList.toggle("on", state.multi);
    syncDrawer();
  }

  function desiredContentHeight() {
    const scroll = document.querySelector(".scroll");
    if (!scroll) return 0;
    let h = scroll.scrollHeight;
    const all = state.accounts.length;
    const vis = filtered().length;
    if (all > vis) {
      const one = document.querySelector(".card");
      const ch = one ? Math.round(one.getBoundingClientRect().height) + 11 : 80;
      h += (all - vis) * ch;
    }
    return Math.ceil(h);
  }

  function fitWindow() {
    const h = desiredContentHeight();
    if (h > 0) Bridge.invoke("fitWindow", { height: h }).catch(() => {});
  }

  function attachHandlers() {
    appEl.addEventListener("click", (e) => {
      const act = e.target.closest("[data-act]");
      if (act) { handleAct(act.dataset.act); return; }
      if (e.target.closest("#multi")) { toggleMulti(); return; }
      const tab = e.target.closest("[data-tab]");
      if (tab) { state.activeGroup = tab.dataset.tab; update(); return; }
      const accAct = e.target.closest("[data-action]");
      if (accAct) { e.stopPropagation(); handleAccAct(accAct.dataset.action, accAct.dataset.id); return; }
      const card = e.target.closest("[data-card]");
      if (card) { state.selectedId = card.dataset.card; syncDrawer(); }
    });

    searchInput.addEventListener("input", () => { state.query = searchInput.value; update(); });
    placeInput.addEventListener("input", () => {
      state.placeId = placeInput.value;
      clearTimeout(placeTimer);
      placeTimer = setTimeout(() => Bridge.invoke("setPlaceId", { placeId: state.placeId }).catch(() => {}), 400);
    });
  }

  async function handleAct(act) {
    if (act === "addLogin") {
      toast("Opening Roblox login…");
      try { const r = await Bridge.invoke("beginLogin"); if (r && !r.cancelled) toast("Account added."); }
      catch (err) { toast(err.message); }
    } else if (act === "addCookie") {
      const c = await promptCookie();
      if (!c) return;
      toast("Importing account…");
      try { await Bridge.invoke("addByCookie", { cookie: c }); toast("Account added."); }
      catch (err) { toast(err.message); }
    } else if (act === "refreshAll") {
      toast("Refreshing all account statuses…");
      Bridge.invoke("refreshAll").catch(() => {});
    } else if (act === "openClient") {
      toast("Opening a clean Roblox client.");
      Bridge.invoke("openClient").catch((err) => toast(err.message));
    } else if (act === "lock") {
      Bridge.invoke("lock").catch(() => {});
    } else if (act === "vaultSettings") {
      vaultSettings();
    } else if (act === "clearLog") {
      Bridge.invoke("clearLog").catch(() => {});
      state.log = [];
      renderLog();
    }
  }

  async function handleAccAct(action, id) {
    const acc = find(id);
    if (action === "launch") {
      if (acc) toast(`Launching ${acc.username}…`);
      Bridge.invoke("launch", { id, placeId: state.placeId }).catch((err) => toast(err.message));
    } else if (action === "refresh") {
      Bridge.invoke("refreshOne", { id }).catch(() => {});
    } else if (action === "delete") {
      const ok = await confirmDelete(acc ? acc.username : "this account");
      if (ok) Bridge.invoke("deleteAccount", { id }).catch((err) => toast(err.message));
    }
  }

  function toggleMulti() {
    const v = !state.multi;
    state.multi = v;
    const m = $("#multi");
    if (m) m.classList.toggle("on", v);
    toast(v ? "Multi-instance ON — holding singleton." : "Multi-instance OFF.");
    Bridge.invoke("setMultiInstance", { enabled: v }).then((r) => { state.multiActive = !!(r && r.active); }).catch(() => {});
  }

  function syncDrawer() {
    if (!state.selectedId) {
      if (drawerBuiltFor) { drawerRoot.innerHTML = ""; drawerBuiltFor = null; }
      return;
    }
    if (drawerBuiltFor !== state.selectedId) buildDrawer();
  }

  function buildDrawer() {
    const raw = find(state.selectedId);
    if (!raw) { drawerRoot.innerHTML = ""; drawerBuiltFor = null; return; }
    drawerBuiltFor = state.selectedId;
    const a = decorate(raw);
    drawerAvatar = a.avatarUrl || "";
    drawerRoot.innerHTML = `
      <div class="overlay" data-close="1"></div>
      <div class="drawer"><div class="drawer-inner">
        <div class="top"><span class="cap">Account detail</span><div class="spacer"></div><span class="x-btn" data-close="1">${CLOSE_SVG}</span></div>
        <div class="ident">
          <div class="avatar" id="d-avatar" style="background:${a.ring}">${faceHtml(a)}<div class="badge" id="d-badge" style="background:${a.statusColor};box-shadow:0 0 9px ${a.statusColor}"></div></div>
          <div style="min-width:0">
            <div class="uname">${esc(a.username)}</div>
            <div class="status" id="d-statusline" style="margin-top:6px;color:${a.statusColor}"><span class="sd" id="d-dot" style="background:${a.statusColor};box-shadow:0 0 7px ${a.statusColor}"></span><span id="d-status">${esc(a.status)}</span></div>
          </div>
        </div>
        <div class="fields">
          <div class="field"><div class="k">User ID</div><div class="v" id="d-userid">${esc(a.userId || "—")}</div></div>
          <div class="field"><div class="k">Last used</div><div class="v" id="d-lastused">${esc(a.lastUsedText)}</div></div>
        </div>
        <div class="field-edit"><div class="lbl">Label</div><input class="d-input" id="d-label" value="${esc(a.label)}" placeholder="nickname"></div>
        <div class="field-edit"><div class="lbl">Group</div><input class="d-input" id="d-group" value="${esc(a.group)}" placeholder="General"></div>
        <div class="field-edit"><div class="lbl">Notes</div><textarea class="d-area" id="d-notes" placeholder="Notes…">${esc(a.notes)}</textarea></div>
        <div class="acts">
          <button class="d-launch" data-action="launch" data-id="${a.id}">Launch into game</button>
          <div class="d-row">
            <button class="d-ghost" data-action="copy" data-id="${a.id}">Copy cookie</button>
            <button class="d-ghost" data-action="refresh" data-id="${a.id}">Refresh</button>
          </div>
        </div>
      </div></div>`;

    drawerRoot.querySelectorAll("[data-close]").forEach((el) =>
      el.addEventListener("click", () => { state.selectedId = null; syncDrawer(); }));

    drawerRoot.querySelectorAll("[data-action]").forEach((el) =>
      el.addEventListener("click", () => {
        const action = el.dataset.action, id = el.dataset.id, acc = find(id);
        if (action === "launch") {
          if (acc) toast(`Launching ${acc.username}…`);
          Bridge.invoke("launch", { id, placeId: state.placeId }).catch((err) => toast(err.message));
        } else if (action === "refresh") {
          Bridge.invoke("refreshOne", { id }).catch(() => {});
        } else if (action === "copy") {
          Bridge.invoke("copyCookie", { id }).then(() => toast(".ROBLOSECURITY copied to clipboard.")).catch((err) => toast(err.message));
        }
      }));

    const commit = () => Bridge.invoke("editAccount", {
      id: state.selectedId,
      label: $("#d-label").value,
      group: $("#d-group").value,
      notes: $("#d-notes").value
    }).catch(() => {});

    ["d-label", "d-group"].forEach((idn) => {
      const el = $("#" + idn);
      el.addEventListener("blur", commit);
      el.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); el.blur(); } });
    });
    $("#d-notes").addEventListener("blur", commit);
  }

  function refreshDrawerLive(dto) {
    if (drawerBuiltFor !== dto.id) return;
    const a = decorate(dto);
    const st = $("#d-status"); if (st) st.textContent = a.status;
    const sl = $("#d-statusline"); if (sl) sl.style.color = a.statusColor;
    const dot = $("#d-dot"); if (dot) { dot.style.background = a.statusColor; dot.style.boxShadow = `0 0 7px ${a.statusColor}`; }
    const badge = $("#d-badge"); if (badge) { badge.style.background = a.statusColor; badge.style.boxShadow = `0 0 9px ${a.statusColor}`; }
    const uid = $("#d-userid"); if (uid) uid.textContent = a.userId || "—";
    const lu = $("#d-lastused"); if (lu) lu.textContent = a.lastUsedText;
    const av = $("#d-avatar");
    if (av) {
      av.style.background = a.ring;
      if ((a.avatarUrl || "") !== drawerAvatar) {
        drawerAvatar = a.avatarUrl || "";
        const old = av.querySelector(".face"); if (old) old.remove();
        av.insertAdjacentHTML("afterbegin", faceHtml(a));
      }
    }
  }

  function promptCookie() {
    return new Promise((resolve) => {
      const ov = document.createElement("div");
      ov.className = "overlay";
      ov.style.cssText = "z-index:80;display:flex;align-items:center;justify-content:center";
      ov.innerHTML = `<div class="vault-card" style="width:440px">
        <div class="vt">Add by cookie</div><div class="vh">Paste .ROBLOSECURITY</div>
        <div class="vp">Paste the .ROBLOSECURITY cookie value of an account you own. It's stored encrypted and never leaves this machine except to Roblox.</div>
        <textarea class="d-area" id="ck" style="min-height:96px" placeholder="_|WARNING:-DO-NOT-SHARE-THIS.--Sharing-this-will-allow-someone-to-log-in-as-you-..."></textarea>
        <div class="vrow"><button class="btn-primary" id="ck-ok">Add account</button><button class="btn-ghost" id="ck-cancel">Cancel</button></div>
      </div>`;
      document.body.appendChild(ov);
      const ta = $("#ck", ov); ta.focus();
      const done = (v) => { ov.remove(); resolve(v); };
      $("#ck-ok", ov).onclick = () => done(ta.value.trim() || null);
      $("#ck-cancel", ov).onclick = () => done(null);
      ov.addEventListener("click", (e) => { if (e.target === ov) done(null); });
    });
  }

  function confirmDelete(name) {
    return new Promise((resolve) => {
      const ov = document.createElement("div");
      ov.className = "overlay";
      ov.style.cssText = "z-index:80;display:flex;align-items:center;justify-content:center";
      ov.innerHTML = `<div class="vault-card" style="width:400px">
        <div class="vt">Remove account</div><div class="vh">Remove ${esc(name)}?</div>
        <div class="vp">This only removes it from this manager — your Roblox account is untouched.</div>
        <div class="vrow"><button class="btn-primary" id="rm-ok" style="background:linear-gradient(180deg,#ff8a8a,#ff5f5f);box-shadow:0 0 22px rgba(255,90,90,.35)">Remove</button><button class="btn-ghost" id="rm-cancel">Cancel</button></div>
      </div>`;
      document.body.appendChild(ov);
      const done = (v) => { ov.remove(); resolve(v); };
      $("#rm-ok", ov).onclick = () => done(true);
      $("#rm-cancel", ov).onclick = () => done(false);
      ov.addEventListener("click", (e) => { if (e.target === ov) done(false); });
    });
  }

  function vaultSettings() {
    const isPw = state.vault.mode === "password";
    const ov = document.createElement("div");
    ov.className = "overlay";
    ov.style.cssText = "z-index:80;display:flex;align-items:center;justify-content:center";
    ov.innerHTML = `<div class="vault-card" id="vc" style="width:400px">
      <div class="vt">Vault security</div>
      <div class="vh">${isPw ? "Change master password" : "Set a master password"}</div>
      <div class="vp">${isPw
        ? "Enter a new password to change it, or remove it to fall back to Windows DPAPI only."
        : "Add a master password on top of Windows DPAPI. You'll enter it each time the app opens."}</div>
      <input type="password" id="vspass" placeholder="${isPw ? "New password" : "Master password"}" autocomplete="new-password">
      <div class="err" id="vserr"></div>
      <div class="vrow">
        <button class="btn-primary" id="vs-save">${isPw ? "Change" : "Set password"}</button>
        ${isPw ? `<button class="btn-ghost" id="vs-remove">Remove</button>` : `<button class="btn-ghost" id="vs-cancel">Cancel</button>`}
      </div>
      ${isPw ? `<button class="btn-ghost" id="vs-cancel" style="justify-content:center">Cancel</button>` : ""}
    </div>`;
    document.body.appendChild(ov);
    const pass = $("#vspass", ov); pass.focus();
    const close = () => ov.remove();
    const apply = async (password) => {
      try {
        const r = await Bridge.invoke("setMasterPassword", { password });
        if (r && r.mode) state.vault.mode = r.mode;
        update();
        toast(password ? "Master password updated." : "Master password removed.");
        close();
      } catch (err) { $("#vserr", ov).textContent = err.message; shake(); }
    };
    $("#vs-save", ov).onclick = () => {
      const p = pass.value;
      if (p.length < 4) { $("#vserr", ov).textContent = "Use at least 4 characters."; shake(); return; }
      apply(p);
    };
    const rem = $("#vs-remove", ov); if (rem) rem.onclick = () => apply(null);
    $("#vs-cancel", ov).onclick = close;
    ov.addEventListener("click", (e) => { if (e.target === ov) close(); });
    pass.addEventListener("keydown", (e) => { if (e.key === "Enter") $("#vs-save", ov).click(); });
  }

  function showProgress(line, pct) {
    bootRoot.innerHTML = `<div class="boot" id="boot">
      <div class="ring-wrap"><div class="ring"></div>${LOCK_SVG}</div>
      <div style="text-align:center"><div class="title">BROBLOX ACCOUNT MANAGER</div><div class="line">${esc(line)}<span class="b"></span></div></div>
      <div class="pbar"><div class="pfill" style="width:${pct}%"></div></div>
    </div>`;
  }

  function shake() {
    const vc = $("#vc");
    if (vc) { vc.classList.remove("shake"); void vc.offsetWidth; vc.classList.add("shake"); }
  }

  function showSetup() {
    bootRoot.innerHTML = `<div class="boot">
      <div class="ring-wrap"><div class="ring"></div>${LOCK_SVG}</div>
      <div class="vault-card" id="vc">
        <div class="vt">First run</div><div class="vh">Secure your vault</div>
        <div class="vp">Set a master password to encrypt your saved sessions, or skip to use Windows DPAPI only (no password, tied to this Windows account).</div>
        <input type="password" id="vpass" placeholder="Master password (optional)" autocomplete="new-password">
        <div class="err" id="verr"></div>
        <div class="vrow"><button class="btn-primary" id="vset">Set password</button><button class="btn-ghost" id="vskip">Skip (DPAPI)</button></div>
      </div></div>`;
    const pass = $("#vpass"); pass.focus();
    const create = async (password) => {
      try {
        const info = await Bridge.invoke("createVault", { password });
        applyState(info.state); await finishBoot();
      } catch (err) { $("#verr").textContent = err.message || "Couldn't create the vault."; shake(); }
    };
    $("#vset").onclick = () => {
      const p = pass.value;
      if (p.length < 4) { $("#verr").textContent = "Use at least 4 characters, or Skip."; shake(); return; }
      create(p);
    };
    $("#vskip").onclick = () => create(null);
    pass.addEventListener("keydown", (e) => { if (e.key === "Enter") $("#vset").click(); });
  }

  function showUnlock(err) {
    bootRoot.innerHTML = `<div class="boot">
      <div class="ring-wrap"><div class="ring"></div>${LOCK_SVG}</div>
      <div class="vault-card${err ? " shake" : ""}" id="vc">
        <div class="vt">Vault locked</div><div class="vh">Enter master password</div>
        <input type="password" id="vpass" placeholder="Master password" autocomplete="current-password">
        <div class="err" id="verr">${err ? esc(err) : ""}</div>
        <div class="vrow"><button class="btn-primary" id="vunlock">Unlock</button></div>
      </div></div>`;
    const pass = $("#vpass"); pass.focus();
    const go = async () => {
      try {
        const res = await Bridge.invoke("unlock", { password: pass.value });
        if (res && res.ok) { applyState(res.state); await finishBoot(); }
        else showUnlock("Wrong password — try again.");
      } catch (err) { showUnlock(err.message || "Unlock failed."); }
    };
    $("#vunlock").onclick = go;
    pass.addEventListener("keydown", (e) => { if (e.key === "Enter") go(); });
  }

  function showCorrupt() {
    bootRoot.innerHTML = `<div class="boot">
      <div class="ring-wrap"><div class="ring"></div>${LOCK_SVG}</div>
      <div class="vault-card" id="vc" style="width:440px">
        <div class="vt" style="color:#ffc24d">Vault unreadable</div>
        <div class="vh">Couldn't decrypt your vault</div>
        <div class="vp">A saved vault exists but can't be decrypted on this Windows account — it may belong to a different user/PC, or be corrupted. <b>Your file is left untouched.</b> Start a fresh vault (this erases the unreadable file), or close the app to restore a backup.</div>
        <input type="password" id="vpass" placeholder="New master password (optional)" autocomplete="new-password">
        <div class="err" id="verr"></div>
        <div class="vrow"><button class="btn-primary" id="vreset">Start fresh</button></div>
      </div></div>`;
    const pass = $("#vpass"); pass.focus();
    $("#vreset").onclick = async () => {
      const p = pass.value;
      if (p && p.length < 4) { $("#verr").textContent = "Use at least 4 characters, or leave blank."; shake(); return; }
      try {
        const info = await Bridge.invoke("resetVault", { password: p || null });
        applyState(info.state); await finishBoot();
      } catch (err) { $("#verr").textContent = err.message || "Couldn't reset the vault."; shake(); }
    };
    pass.addEventListener("keydown", (e) => { if (e.key === "Enter") $("#vreset").click(); });
  }

  async function finishBoot() {
    mountIfNeeded();
    update();
    showProgress(`loading ${state.accounts.length} session(s)…`, 55);
    await sleep(280);
    showProgress("checking presence…", 80);
    Bridge.invoke("refreshAll").catch(() => {});
    await sleep(240);
    showProgress("ready ❯", 100);
    await sleep(360);
    const b = $("#boot");
    if (b) b.classList.add("fade");
    setTimeout(() => { bootRoot.innerHTML = ""; state.booting = false; }, 600);
    fitWindow();
  }

  function applyState(s) {
    if (!s) return;
    state.accounts = s.accounts || [];
    state.settings = s.settings || state.settings;
    state.log = s.log || [];
    state.multi = !!(s.settings && s.settings.multiInstance);
    state.multiActive = !!s.multiInstanceActive;
    if (s.vaultMode) state.vault.mode = s.vaultMode;
    state.placeId = s.settings && s.settings.placeId ? String(s.settings.placeId) : "";
    mountIfNeeded();
    if (searchInput) searchInput.value = state.query;
    if (placeInput) placeInput.value = state.placeId;
    update();
  }

  function upsert(dto) {
    const i = state.accounts.findIndex((a) => a.id === dto.id);
    if (i >= 0) state.accounts[i] = dto;
    else state.accounts.push(dto);
  }

  function onAccountUpdated(dto) {
    const existed = state.accounts.some((a) => a.id === dto.id);
    upsert(dto);
    if (!existed) { update(); fitWindow(); }
    else { patchAccount(dto); }
    if (drawerBuiltFor === dto.id) refreshDrawerLive(dto);
  }

  function patchAccount(dto) {
    if (!mounted) return;
    const sel = window.CSS && CSS.escape ? CSS.escape(dto.id) : dto.id;
    const card = document.querySelector(`.card[data-card="${sel}"]`);
    if (card && filtered().some((a) => a.id === dto.id)) {
      card.outerHTML = cardHtml(dto);
    } else {
      $("#cards-c").innerHTML = cardsHtml();
    }
    $("#stats-c").innerHTML = statsHtml();
    $("#tabs-c").innerHTML = tabsHtml();
    $("#listhead-c").innerHTML = listHeadHtml();
  }

  Bridge.on("accountUpdated", onAccountUpdated);
  Bridge.on("accountRemoved", (d) => {
    state.accounts = state.accounts.filter((a) => a.id !== d.id);
    if (state.selectedId === d.id) state.selectedId = null;
    update();
    fitWindow();
  });
  Bridge.on("log", (entry) => {
    state.log.push(entry);
    if (state.log.length > 200) state.log = state.log.slice(-200);
    renderLog();
  });
  Bridge.on("vaultLocked", () => {
    state.accounts = [];
    state.selectedId = null;
    drawerBuiltFor = null;
    drawerRoot.innerHTML = "";
    state.booting = true;
    update();
    showUnlock();
  });
  Bridge.on("toast", (d) => toast(d.text || ""));
  Bridge.on("refreshProgress", () => {});

  function initCanvas() {
    const cv = document.getElementById("bg-canvas");
    if (!cv) return;
    const ctx = cv.getContext("2d");
    let w, h, pts;
    const DPR = Math.min(window.devicePixelRatio || 1, 2);
    function resize() {
      w = window.innerWidth; h = window.innerHeight;
      cv.width = w * DPR; cv.height = h * DPR;
      ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
      const n = Math.min(70, Math.round(w * h / 26000));
      pts = Array.from({ length: n }, () => ({
        x: Math.random() * w, y: Math.random() * h,
        vx: (Math.random() - .5) * .25, vy: (Math.random() - .5) * .25,
        r: Math.random() * 1.6 + .6
      }));
    }
    window.addEventListener("resize", resize);
    resize();
    (function draw() {
      ctx.clearRect(0, 0, w, h);
      for (const p of pts) {
        p.x += p.vx; p.y += p.vy;
        if (p.x < 0 || p.x > w) p.vx *= -1;
        if (p.y < 0 || p.y > h) p.vy *= -1;
      }
      for (let i = 0; i < pts.length; i++)
        for (let j = i + 1; j < pts.length; j++) {
          const dx = pts[i].x - pts[j].x, dy = pts[i].y - pts[j].y, d = dx * dx + dy * dy;
          if (d < 13000) {
            ctx.strokeStyle = `rgba(45,255,138,${(1 - d / 13000) * 0.18})`;
            ctx.lineWidth = 1;
            ctx.beginPath(); ctx.moveTo(pts[i].x, pts[i].y); ctx.lineTo(pts[j].x, pts[j].y); ctx.stroke();
          }
        }
      for (const p of pts) {
        ctx.fillStyle = "rgba(45,255,138,.6)";
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, 7); ctx.fill();
      }
      requestAnimationFrame(draw);
    })();
  }

  async function boot() {
    initCanvas();
    showProgress("initializing…", 12);
    let info;
    try { info = await Bridge.invoke("init"); }
    catch (e) { info = { mode: "none", needsSetup: true }; }
    state.vault = { mode: info.mode || "none", locked: !!info.locked, needsSetup: !!info.needsSetup };
    await sleep(250);
    if (info.corrupt) { showCorrupt(); return; }
    if (info.needsSetup) { showSetup(); return; }
    if (info.locked) { showUnlock(); return; }
    applyState(info.state);
    await finishBoot();
  }

  boot();
})();
