'use strict';
// SahaNet yönetim paneli. Güvenlik: kullanıcı içeriği yalnızca textContent ile basılır (innerHTML yok),
// belirteç sessionStorage'da (sekme kapanınca silinir), CSP satır içi betiğe izin vermez.
(() => {
  const $ = (id) => document.getElementById(id);
  const TOKEN_KEY = 'sahanet.admin.token';
  let token = sessionStorage.getItem(TOKEN_KEY);
  let role = null;

  const toast = (text) => {
    const t = $('toast');
    t.textContent = text;
    t.classList.add('show');
    setTimeout(() => t.classList.remove('show'), 2500);
  };

  const el = (tag, props = {}, ...children) => {
    const node = document.createElement(tag);
    for (const [k, v] of Object.entries(props)) {
      if (k === 'className') node.className = v;
      else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
      else node.setAttribute(k, v);
    }
    for (const c of children) node.append(c instanceof Node ? c : document.createTextNode(c ?? ''));
    return node;
  };

  const fmtDate = (s) => (s ? new Date(s).toLocaleString('tr-TR') : '');
  const fmtMoney = (n) => Number(n).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' });

  async function api(path, options = {}) {
    const res = await fetch(path, {
      ...options,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: options.body ? JSON.stringify(options.body) : undefined,
    });
    if (res.status === 401) { logout(); throw new Error('Oturum sona erdi.'); }
    if (res.status === 204) return null;
    const data = await res.json().catch(() => null);
    if (!res.ok) throw new Error(data?.detail || data?.title || `Hata ${res.status}`);
    return data;
  }

  function decodeRole(jwt) {
    try {
      const payload = JSON.parse(atob(jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      return payload.role || null;
    } catch { return null; }
  }

  // ---- Giriş ----
  $('send-code').addEventListener('click', async () => {
    $('login-error').textContent = '';
    try { await api('/api/auth/otp/request', { method: 'POST', body: { phone: $('phone').value } }); toast('Kod gönderildi.'); }
    catch (e) { $('login-error').textContent = e.message; }
  });

  $('verify').addEventListener('click', async () => {
    $('login-error').textContent = '';
    try {
      const auth = await api('/api/auth/otp/verify', { method: 'POST', body: { phone: $('phone').value, code: $('code').value } });
      const r = decodeRole(auth.accessToken);
      if (r !== 'Admin' && r !== 'Moderator') { $('login-error').textContent = 'Bu hesabın yönetim yetkisi yok.'; return; }
      token = auth.accessToken;
      sessionStorage.setItem(TOKEN_KEY, token);
      start();
    } catch (e) { $('login-error').textContent = e.message; }
  });

  function logout() {
    token = null;
    sessionStorage.removeItem(TOKEN_KEY);
    location.reload();
  }
  $('logout').addEventListener('click', logout);

  // ---- Sekmeler ----
  const loaders = { dashboard: loadDashboard, reports: loadReports, users: loadUsers, disputes: loadDisputes, bans: loadBans, audit: loadAudit };
  document.querySelectorAll('#tabs button').forEach((b) => b.addEventListener('click', () => show(b.dataset.tab)));

  function show(tab) {
    document.querySelectorAll('#tabs button').forEach((b) => b.classList.toggle('active', b.dataset.tab === tab));
    document.querySelectorAll('.tab').forEach((s) => { s.hidden = s.id !== tab; });
    loaders[tab]().catch((e) => toast(e.message));
  }

  function start() {
    role = decodeRole(token);
    if (!role) { logout(); return; }
    $('login').hidden = true;
    $('tabs').hidden = false;
    $('logout').hidden = false;
    $('who').textContent = role === 'Admin' ? 'Yönetici' : 'Moderatör';
    // Moderatör: itiraz, yasak ve denetim sekmeleri yöneticiye özel.
    if (role !== 'Admin') ['disputes', 'bans', 'audit'].forEach((t) => document.querySelector(`[data-tab="${t}"]`).remove());
    show('dashboard');
  }

  // ---- Gösterge ----
  async function loadDashboard() {
    const d = await api('/api/admin/dashboard');
    const c = d.core;
    const section = $('dashboard');
    const kpi = (label, value, alert) => el('div', { className: `kpi${alert ? ' alert' : ''}` }, el('div', { className: 'value' }, String(value)), el('div', { className: 'label' }, label));
    const kpis = el('div', { className: 'kpis' },
      kpi('Toplam kullanıcı', c.totalUsers), kpi('Yeni (7 gün)', c.newUsers7Days), kpi('Yeni (30 gün)', c.newUsers30Days),
      kpi('Aktif ilan', c.activeListings), kpi('Satış hacmi (30 gün)', fmtMoney(c.salesVolume30Days)), kpi('Komisyon (30 gün)', fmtMoney(c.commission30Days)),
      kpi('Açık şikâyet', c.openReports, c.openReports > 0), kpi('Açık itiraz', c.openDisputes, c.openDisputes > 0),
      kpi('Askıdaki hesap', c.suspendedUsers), kpi('Uyarı (30 gün)', c.alertsLast30Days), kpi('Bölge aboneliği', c.activeRegionSubscriptions));

    const max = Math.max(1, ...c.signupsLast30Days.map((x) => x.count));
    const bars = el('div', { className: 'bars', role: 'img', 'aria-label': 'Son 30 gün günlük kayıt' });
    for (const day of c.signupsLast30Days) {
      const bar = el('div', { title: `${day.date}: ${day.count}` });
      bar.style.height = `${(day.count / max) * 100}%`;
      bars.append(bar);
    }

    const modules = el('div', { className: 'kpis' });
    for (const [name, values] of Object.entries(d.modules)) {
      for (const [k, v] of Object.entries(values)) modules.append(kpi(`${name} · ${k}`, v));
    }

    const niches = el('p', { className: 'muted' }, 'Nişe göre: ' + c.usersByNiche.map((n) => `${n.key} ${n.count}`).join(' · '));
    section.replaceChildren(kpis, el('div', { className: 'card' }, el('h2', {}, 'Günlük yeni kayıt (30 gün)'), bars), niches, modules,
      el('p', { className: 'muted' }, `Oluşturulma: ${fmtDate(d.generatedAt)}`));
  }

  // ---- Şikâyetler ----
  $('reload-reports').addEventListener('click', () => loadReports().catch((e) => toast(e.message)));
  $('report-status').addEventListener('change', () => loadReports().catch((e) => toast(e.message)));

  async function loadReports() {
    const page = await api(`/api/admin/reports?status=${encodeURIComponent($('report-status').value)}`);
    const rows = page.items.map((r) => {
      const actions = el('td', { className: 'actions' });
      if (r.status === 'Open') {
        const act = (label, action, cls) => el('button', { className: cls, onclick: () => resolve(r.id, action) }, label);
        actions.append(act('Reddet', 'Dismiss', 'secondary'));
        if (r.targetType !== 'User') actions.append(act('İçeriği kaldır', 'RemoveContent'));
        actions.append(act('Askıya al', 'SuspendUser', 'danger'));
      } else {
        actions.append(r.action || '');
      }
      return el('tr', {},
        el('td', {}, fmtDate(r.createdAt)),
        el('td', {}, r.targetType, ' ', r.bySystem ? el('span', { className: 'badge system' }, `spam ${r.spamScore}`) : ''),
        el('td', {}, r.targetPreview || '(silinmiş)', r.openReportsOnTarget > 1 ? el('span', { className: 'badge' }, ` ${r.openReportsOnTarget} şikâyet`) : ''),
        el('td', {}, r.reason), el('td', {}, r.note || ''), el('td', {}, r.status), actions);
    });
    $('report-rows').replaceChildren(...rows);
  }

  async function resolve(id, action) {
    const note = prompt('Karar notu (isteğe bağlı):') ?? undefined;
    await api(`/api/admin/reports/${id}/resolve`, { method: 'POST', body: { action, note } });
    toast('Karar kaydedildi.');
    await loadReports();
  }

  // ---- Kullanıcılar ----
  $('search-users').addEventListener('click', () => loadUsers().catch((e) => toast(e.message)));

  async function loadUsers() {
    const q = new URLSearchParams();
    if ($('user-phone').value) q.set('phone', $('user-phone').value);
    if ($('user-q').value) q.set('q', $('user-q').value);
    if ($('user-status').value) q.set('status', $('user-status').value);
    const users = await api(`/api/admin/users?${q}`);
    $('user-rows').replaceChildren(...users.map((u) => {
      const actions = el('td', { className: 'actions' });
      if (u.status === 'Suspended') {
        actions.append(el('button', { className: 'secondary', onclick: () => suspension(u.id, false) }, 'Askıyı kaldır'));
      } else {
        actions.append(el('button', { className: 'danger', onclick: () => suspension(u.id, true) }, 'Askıya al'));
      }
      if (role === 'Admin') {
        const select = el('select', {}, ...['User', 'Moderator', 'Admin'].map((r) => el('option', r === u.role ? { selected: 'selected' } : {}, r)));
        select.addEventListener('change', async () => {
          try { await api(`/api/admin/users/${u.id}/role`, { method: 'PUT', body: { role: select.value } }); toast('Rol güncellendi.'); }
          catch (e) { toast(e.message); }
        });
        actions.append(select);
      }
      return el('tr', {},
        el('td', {}, u.phoneMasked), el('td', {}, u.displayName || ''), el('td', {}, `${u.nicheKey || '-'} / ${u.regionName || '-'}`),
        el('td', {}, u.status, u.suspensionReason ? ` (${u.suspensionReason})` : ''), el('td', {}, u.role), el('td', {}, fmtDate(u.createdAt)), actions);
    }));
  }

  async function suspension(id, suspend) {
    const reason = suspend ? prompt('Askıya alma gerekçesi:') : null;
    if (suspend && !reason) return;
    await api(`/api/admin/users/${id}/${suspend ? 'suspend' : 'unsuspend'}`, { method: 'POST', body: suspend ? { reason } : {} });
    toast('Güncellendi.');
    await loadUsers();
  }

  // ---- İtirazlar (Faz 5) ----
  async function loadDisputes() {
    const items = await api('/api/admin/payments/disputes');
    $('dispute-rows').replaceChildren(...items.map((o) => el('tr', {},
      el('td', {}, fmtDate(o.createdAt)), el('td', {}, fmtMoney(o.amount)), el('td', {}, o.disputeReason || ''),
      el('td', { className: 'actions' },
        el('button', { onclick: () => decide(o.id, 'ReleaseToSeller') }, 'Satıcıya aktar'),
        el('button', { className: 'danger', onclick: () => decide(o.id, 'RefundBuyer') }, 'Alıcıya iade')))));
  }

  async function decide(id, decision) {
    const note = prompt('Karar notu:') ?? undefined;
    await api(`/api/admin/payments/orders/${id}/resolve`, { method: 'POST', body: { decision, note } });
    toast('Karar kaydedildi.');
    await loadDisputes();
  }

  // ---- Av yasakları (balıkçı modülü) ----
  $('ban-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const f = new FormData(e.target);
    const body = Object.fromEntries([...f.entries()].map(([k, v]) => [k, v === '' ? null : (/Month|Day/.test(k) ? Number(v) : v)]));
    try { await api('/api/fishing/admin/bans', { method: 'POST', body }); e.target.reset(); toast('Kaydedildi.'); await loadBans(); }
    catch (err) { toast(err.message); }
  });

  async function loadBans() {
    const bans = await api('/api/fishing/bans');
    $('ban-rows').replaceChildren(...bans.map((b) => el('tr', {},
      el('td', {}, b.title), el('td', {}, b.period), el('td', {}, b.speciesCode || 'tümü'), el('td', {}, b.gear || ''),
      el('td', {}, b.isActiveToday ? 'Yürürlükte' : ''),
      el('td', { className: 'actions' }, el('button', { className: 'danger', onclick: async () => {
        if (!confirm(`"${b.title}" silinsin mi?`)) return;
        await api(`/api/fishing/admin/bans/${b.id}`, { method: 'DELETE' });
        toast('Silindi.');
        await loadBans();
      } }, 'Sil')))));
  }

  // ---- Denetim kaydı ----
  async function loadAudit() {
    const entries = await api('/api/admin/audit');
    $('audit-rows').replaceChildren(...entries.map((a) => el('tr', {},
      el('td', {}, fmtDate(a.at)), el('td', {}, a.action), el('td', {}, `${a.targetType} ${a.targetId}`), el('td', {}, a.details || ''), el('td', {}, a.actorId))));
  }

  if (token) start();
})();
