/* Rúnheim Live Hall — client.
   One state, three renderings: the walking map, the place panels, the dossiers.
   Honesty rules enforced here as well as on the server: stale word rests,
   borrowed faces are ringed and labelled, the Jarl's activity is a guess. */
'use strict';

/* ---------- Places, stations, views (data — adjust anchors freely) ---------- */
const PLACES = {
  longhouse: { name: "Jarl's Longhouse", duty: 'Think · plan · coordinate', stations: [
    { id: 'high-seat', label: 'high seat', tools: ['coordinate', 'think'], dx: 0, dy: -0.02 },
    { id: 'long-fire', label: 'long fire', tools: ['discuss'], dx: -0.03, dy: 0.02 },
    { id: 'council-table', label: 'council table', tools: ['plan'], dx: 0.03, dy: 0.02 },
    { id: 'war-map', label: 'war-map board', tools: ['campaign', 'monitor'], dx: 0.05, dy: -0.01 },
    { id: 'door-post', label: 'door post', tools: ['waiting'], dx: -0.05, dy: 0.03 },
    { id: 'gift-chest', label: 'gift chest', tools: ['bank', 'publish'], dx: 0.02, dy: 0.04 } ] },
  library: { name: "Mimir's Rune Library", duty: 'Read · search · research', stations: [
    { id: 'reading-desk', label: 'reading desk', tools: ['read', 'fetch'], dx: 0, dy: 0 },
    { id: 'scroll-shelves', label: 'scroll shelves', tools: ['search'], dx: -0.04, dy: -0.01 },
    { id: 'map-table', label: 'great map table', tools: ['research', 'plan'], dx: 0.04, dy: 0.01 },
    { id: 'scribe-desk', label: "scribe's desk", tools: ['write'], dx: 0.02, dy: 0.04 },
    { id: 'rune-lectern', label: 'rune-stone lectern', tools: ['review'], dx: -0.02, dy: 0.04 } ] },
  forge: { name: 'Dvergar Forge', duty: 'Edit · write · build', stations: [
    { id: 'great-anvil', label: 'great anvil', tools: ['edit', 'build'], dx: 0, dy: 0 },
    { id: 'bellows', label: 'bellows hearth', tools: ['generate'], dx: -0.04, dy: -0.02 },
    { id: 'quench', label: 'quench barrel', tools: ['fix', 'debug'], dx: 0.04, dy: 0.02 },
    { id: 'grindstone', label: 'grindstone', tools: ['refactor', 'polish'], dx: -0.03, dy: 0.03 },
    { id: 'workbench', label: 'workbench', tools: ['write', 'assemble'], dx: 0.03, dy: -0.03 },
    { id: 'weapon-rack', label: 'weapon rack', tools: ['commit'], dx: 0.05, dy: 0.04 } ] },
  ring: { name: "Týr's Training Ring", duty: 'Run · test · prove', stations: [
    { id: 'sparring', label: 'sparring circle', tools: ['bash', 'run'], dx: 0, dy: 0 },
    { id: 'proving-post', label: 'proving post', tools: ['test'], dx: 0.04, dy: -0.02 },
    { id: 'ring-rack', label: 'weapon rack', tools: ['benchmark'], dx: -0.04, dy: 0.02 },
    { id: 'judges-bench', label: "judges' bench", tools: ['review', 'verify'], dx: 0, dy: 0.05 } ] },
  seidr: { name: 'Seiðr House', duty: 'Debug · heal · divine', stations: [
    { id: 'seidr-high-seat', label: 'high seat', tools: ['think'], dx: 0, dy: -0.02 },
    { id: 'herb-bench', label: 'herb bench', tools: ['debug', 'heal', 'fix'], dx: -0.04, dy: 0.02 },
    { id: 'cauldron', label: 'cauldron fire', tools: ['generate', 'transform'], dx: 0.04, dy: 0 },
    { id: 'divination', label: 'divination table', tools: ['research'], dx: 0.02, dy: 0.04 },
    { id: 'dream-loft', label: 'dream-loft steps', tools: ['idle'], dx: -0.02, dy: -0.04 } ] },
  watch: { name: "Huginn's Watch", duty: 'Delegate · monitor · message', stations: [
    { id: 'tower-rail', label: 'tower rail', tools: ['monitor'], dx: 0, dy: -0.02 },
    { id: 'brazier', label: 'signal brazier', tools: ['alert'], dx: 0.04, dy: 0.01 },
    { id: 'message-desk', label: 'message desk', tools: ['send'], dx: -0.04, dy: 0.02 },
    { id: 'raven-perch', label: 'raven perch', tools: ['delegate'], dx: 0.02, dy: -0.04 },
    { id: 'stair-post', label: 'stair post', tools: ['review'], dx: 0, dy: 0.04 } ] },
  oathstones: { name: 'Sacred Oathstones', duty: 'Bank · commit · publish', stations: [
    { id: 'ring-centre', label: 'ring centre', tools: ['bank', 'commit', 'push'], dx: 0, dy: 0 },
    { id: 'tally-stone', label: 'tally stone', tools: ['verify', 'count'], dx: -0.04, dy: 0.01 },
    { id: 'offering-stone', label: 'offering stone', tools: ['publish'], dx: 0.04, dy: 0.01 },
    { id: 'witness', label: 'witness benches', tools: ['monitor', 'review'], dx: 0, dy: 0.045 } ] },
  hearth: { name: 'Hearth Hall', duty: 'Rest · finished · awaiting word', stations: [
    { id: 'long-tables', label: 'long tables', tools: ['done', 'rest'], dx: 0, dy: 0 },
    { id: 'hearth-fire', label: 'hearth fire', tools: ['idle'], dx: -0.04, dy: -0.01 },
    { id: 'ale-barrels', label: 'ale barrels', tools: ['done'], dx: 0.04, dy: 0.02 },
    { id: 'skald-corner', label: "skald's corner", tools: ['write', 'song'], dx: 0.03, dy: -0.03 },
    { id: 'rest-benches', label: 'rest benches', tools: ['rest', 'stale'], dx: -0.02, dy: 0.04 } ] },
};
const PLACE_ORDER = ['longhouse', 'library', 'forge', 'ring', 'seidr', 'watch', 'oathstones', 'hearth'];

const VIEWS = [
  { id: 'green', name: 'Village Green', day: '/art/places/village-day-grand.webp', night: '/art/places/village-night-grand.webp',
    hub: [0.50, 0.80], dock: [0.50, 0.95], anchors: {
      longhouse: [0.50, 0.36], library: [0.19, 0.44], forge: [0.81, 0.47], ring: [0.68, 0.66],
      seidr: [0.12, 0.62], watch: [0.89, 0.27], oathstones: [0.33, 0.71], hearth: [0.50, 0.57] } },
  { id: 'a2', name: 'Hill Overlook', day: '/art/places/village-day-grand-a2.webp', night: '/art/places/village-night-grand-a2.webp',
    hub: [0.50, 0.45], dock: [0.50, 0.08], anchors: {
      longhouse: [0.50, 0.64], library: [0.73, 0.56], forge: [0.23, 0.53], ring: [0.31, 0.37],
      seidr: [0.83, 0.39], watch: [0.15, 0.68], oathstones: [0.63, 0.29], hearth: [0.48, 0.49] } },
  { id: 'a3', name: 'Oathstone Shore', day: '/art/places/village-day-grand-a3.webp', night: '/art/places/village-night-grand-a3.webp',
    hub: [0.45, 0.62], dock: [0.22, 0.90], anchors: {
      longhouse: [0.56, 0.44], library: [0.81, 0.41], forge: [0.29, 0.39], ring: [0.17, 0.58],
      seidr: [0.91, 0.61], watch: [0.73, 0.20], oathstones: [0.42, 0.74], hearth: [0.61, 0.59] } },
];

/* The named cast — slug to display name (own faces, never "shown as"). */
const CAST = {
  bergdis: 'Bergdís', bodbera: 'Boðbera', draumdis: 'Draumdís', eir: 'Eir', eldrithr: 'Eldríðr',
  'forge-master': 'The Forge-Master', gagnadis: 'Gagnadís', 'gate-warden': 'The Gate-Warden',
  hallveig: 'Hallveig', hamdis: 'Hamdís', heidr: 'Heiðr', heimdall: 'Heimdall', hrafnhildr: 'Hrafnhildr',
  ingvild: 'Ingvild', juggernaut: 'The Juggernaut', kaupdis: 'Kaupdís', kraftdis: 'Kraftdís',
  leirun: 'Leirún', 'lesson-carver': 'The Lesson-Carver', lifdis: 'Lífdís', ljosbera: 'Ljósbera',
  'map-keeper': 'The Map-Keeper', mimir: 'Mímir', 'mv-canonkeeper': 'MV Canon-Keeper',
  'mv-director': 'MV Director', 'mv-editor': 'MV Editor', 'mv-genhand': 'MV Gen-Hand', 'mv-qc': 'MV QC',
  'mv-scholar': 'MV Scholar', 'mv-scout': 'MV Scout', 'mv-soundmaster': 'MV Soundmaster',
  'mv-warden': 'MV Warden', myndis: 'Myndís', 'plain-speaker': 'The Plain-Speaker', rannveig: 'Rannveig',
  regludis: 'Regludís', reiknidis: 'Reiknidís', replicater: 'The Replicater', runhildr: 'Rúnhildr',
  skapdis: 'Skapdís', solrun: 'Sólrún', solveig: 'Solveig', steinunn: 'Steinunn', syndis: 'Syndís',
  taflun: 'Taflún', thingdis: 'Þingdís', thor: 'Thor', tofludis: 'Töfludís', tove: 'Tove',
  'upstream-forge': 'The Upstream Forge', vedis: 'Védís', verkdis: 'Verkdís', volmarr: 'Volmarr', yrsa: 'Yrsa',
};
/* Kind words to cast archetypes — the second matching road after own names. */
const KIND_CAST = [
  [['verifier', 'verify', 'checker', 'gate', 'warden', 'auditor', 'reviewer'], 'gate-warden'],
  [['builder', 'build', 'coder', 'implementer', 'smith', 'developer', 'engineer'], 'juggernaut'],
  [['coordinator', 'lead', 'orchestrator', 'captain'], 'forge-master'],
  [['researcher', 'research', 'scholar', 'investigator', 'scout'], 'mv-scholar'],
  [['messenger', 'herald', 'sender', 'courier'], 'bodbera'],
  [['mapper', 'cartographer', 'surveyor'], 'map-keeper'],
  [['scribe', 'writer of records', 'recorder', 'carver'], 'lesson-carver'],
  [['speaker', 'spokesperson', 'announcer'], 'plain-speaker'],
  [['artist', 'page builder', 'artifact builder', 'designer', 'illustrator'], 'solveig'],
  [['healer', 'medic', 'repairer'], 'eir'],
  [['watcher', 'monitor', 'sentry', 'guard'], 'heimdall'],
];
const TOOL_PLACE = [
  [['read', 'search', 'fetch', 'research', 'find', 'lookup', 'get', 'list'], 'library'],
  [['edit', 'write', 'create', 'build', 'generate', 'make'], 'forge'],
  [['bash', 'run', 'exec', 'test', 'benchmark', 'shell', 'command'], 'ring'],
  [['debug', 'fix', 'repair', 'heal'], 'seidr'],
  [['think', 'plan', 'design', 'reason'], 'longhouse'],
  [['delegate', 'review', 'spawn', 'coordinate', 'send', 'message', 'monitor'], 'watch'],
  [['commit', 'push', 'bank', 'publish', 'merge'], 'oathstones'],
];
const GLYPHS = {
  read: '📖', search: '🔍', fetch: '📖', research: '🗺️', edit: '⚒️', write: '✒️', build: '🔨',
  generate: '🔥', bash: '⚡', run: '⚡', test: '🎯', benchmark: '🎯', debug: '🌿', fix: '🛠️',
  think: '🌀', plan: '🗺️', delegate: '🐦‍⬛', review: '👁️', send: '✉️', commit: '🪨', push: '🪨',
  bank: '🪨', publish: '🎁', verify: '⚖️', monitor: '👁️', heal: '🌿', refactor: '🪨', transform: '🔥',
};
const PACE = { working: 0.13, thinking: 0.055, waiting: 0.22, idle: 0.05, done: 0.13, failed: 0.08, resting: 0.05 };

/* ---------- Small helpers ---------- */
const $ = (s) => document.querySelector(s);
const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const norm = (v) => String(v ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, ' ').trim();
const hash = (s) => { let h = 0; for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0; return h; };
const REDUCED = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function toolFamily(raw) {
  const t = norm(raw);
  if (!t) return '';
  for (const fam of Object.keys(GLYPHS)) if (t.includes(fam)) return fam;
  if (t.includes('exec') || t.includes('shell')) return 'bash';
  if (t.includes('open') || t.includes('read')) return 'read';
  return t.split(' ')[0] || '';
}
function placeFor(agent) {
  const spoken = norm(agent.effective_place || agent.place);
  for (const id of PLACE_ORDER) if (spoken === id || spoken === norm(PLACES[id].name)) return id;
  const fam = toolFamily(agent.current_tool);
  for (const [words, place] of TOOL_PLACE) if (words.includes(fam)) return place;
  return 'hearth';
}
function stationFor(placeId, agent) {
  const place = PLACES[placeId];
  const spoken = norm(agent.station);
  if (spoken) {
    const hit = place.stations.find((s) => norm(s.label) === spoken || s.id === spoken.replace(/ /g, '-'));
    if (hit) return hit;
  }
  const fam = toolFamily(agent.current_tool);
  const byTool = place.stations.find((s) => s.tools.includes(fam));
  if (byTool) return byTool;
  const status = agent.effective_status || agent.status;
  if (status === 'waiting') return place.stations.find((s) => s.tools.includes('waiting')) || place.stations[0];
  if (status === 'resting') return place.stations.find((s) => s.tools.includes('stale') || s.tools.includes('rest')) || place.stations[0];
  if (status === 'done') return place.stations.find((s) => s.tools.includes('done')) || place.stations[0];
  return place.stations[0];
}
function formatAge(seconds) {
  if (seconds == null) return 'no word yet';
  if (seconds < 60) return `${Math.floor(seconds)}s ago`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m ago`;
  return `${Math.floor(seconds / 86400)}d ago`;
}

/* ---------- Portrait matching: own name → kind archetype → trait library ---------- */
let TRAITS = [];
function matchPortrait(agent) {
  if (agent.portrait) {
    const slug = String(agent.portrait).split('/').pop().replace('.webp', '');
    if (CAST[slug] || TRAITS.some((t) => t.slug === slug))
      return { slug, own: !!CAST[slug] && norm(CAST[slug]) === norm(agent.name), label: CAST[slug] || slug };
    return { slug, own: false, label: slug };
  }
  const nameN = norm(agent.name);
  for (const [slug, display] of Object.entries(CAST))
    if (nameN === norm(display) || nameN === norm(slug)) return { slug, own: true, label: display };
  const kindText = norm([agent.kind, agent.role, agent.agent_type].filter(Boolean).join(' '));
  for (const [words, slug] of KIND_CAST)
    if (words.some((w) => kindText.includes(w))) return { slug, own: false, label: CAST[slug] };
  // Trait-library scoring: keywords + tool affinities + place affinity.
  const hay = norm([agent.kind, agent.role, agent.name, agent.task_label].filter(Boolean).join(' '));
  const fam = toolFamily(agent.current_tool);
  const place = placeFor(agent);
  let best = null, bestScore = 0;
  for (const t of TRAITS) {
    let score = 0;
    for (const kw of t.keywords || []) if (kw && hay.includes(norm(kw))) score += 2;
    if (fam && (t.affinities || []).includes(fam)) score += 2;
    if ((t.places || []).includes(place)) score += 1;
    if (score > bestScore) { bestScore = score; best = t; }
  }
  if (best && bestScore >= 2) return { slug: best.slug, own: false, label: titleCase(best.slug), library: true };
  return { slug: null, own: false, label: null }; // letter medallion — no face claimed
}
const titleCase = (slug) => slug.split('-').map((w) => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');

/* ---------- Village state and the walking engine ---------- */
let payload = null;
let viewIndex = 0;
let heroes = new Map(); // id -> hero record
let selectedId = null;
let nightMode = true;

const mapEl = $('#map'), heroesEl = $('#heroes'), groundEl = $('#ground');

function view() { return VIEWS[viewIndex]; }
function anchorOf(placeId) { return view().anchors[placeId] || view().anchors.hearth; }

function heroTarget(agent, slotIndex, slotCount) {
  const status = agent.effective_status || agent.status;
  let placeId = placeFor(agent);
  if (status === 'waiting') placeId = 'longhouse'; // rule 9: the Jarl's call has right of way
  const station = stationFor(placeId, agent);
  const [ax, ay] = anchorOf(placeId);
  // Deterministic per-station slot ring: polite offsets, nobody stands in anyone.
  const angle = (hash(agent.id || agent.name || '') % 360) * Math.PI / 180;
  const ringR = slotCount > 1 ? 0.016 + 0.011 * Math.floor(slotIndex / 6) : 0;
  const x = ax + station.dx + Math.cos(angle) * ringR;
  const y = ay + station.dy + Math.sin(angle) * ringR * 0.7;
  return { placeId, station, x, y };
}

function makeHeroEl(agent, match) {
  const el = document.createElement('button');
  el.type = 'button';
  el.className = 'hero';
  el.innerHTML = `
    <span class="glyph" hidden></span>
    <span class="need" hidden>needs you</span>
    ${match.slug
      ? `<img class="face" alt="Portrait of ${esc(agent.name || match.label || '')}">`
      : `<span class="face"></span>`}
    <span class="dot"></span>
    <span class="hname"></span>
    <span class="guess-tag" hidden>the village guesses…</span>`;
  if (match.slug) el.querySelector('.face').src = match.library
    ? `/art/portraits-library/${match.slug}.webp`
    : `/art/portraits/${match.slug}.webp`;
  el.addEventListener('click', () => openDossier(agent.id));
  heroesEl.appendChild(el);
  return el;
}

function retarget(hero, agent, slotIndex, slotCount, isNew) {
  const status = agent.effective_status || agent.status;
  const target = heroTarget(agent, slotIndex, slotCount);
  hero.placeId = target.placeId;
  hero.station = target.station;
  const dist = Math.hypot(hero.x - target.x, hero.y - target.y);
  const queue = [];
  if (!REDUCED && dist > 0.02) {
    if (hero.placeId !== hero.lastPlaceId && hero.lastPlaceId) {
      const [hx, hy] = view().hub;
      queue.push({ x: hx, y: hy }); // rule 1: by the roads (yard hub), never crow-flight through halls
    }
    queue.push({ x: target.x, y: target.y });
  }
  hero.queue = REDUCED ? [] : queue;
  if (REDUCED || dist <= 0.02) { hero.x = target.x; hero.y = target.y; }
  const total = queue.reduce((acc, p, i) => acc + Math.hypot(p.x - (i ? queue[i - 1].x : hero.x), p.y - (i ? queue[i - 1].y : hero.y)), 0);
  const pace = PACE[status] ?? 0.08;
  hero.speed = Math.max(pace, total / 6); // rule 6: no walk runs longer than ~6 seconds
  hero.lastPlaceId = target.placeId;
  if (isNew) { /* arrivals come by the shore: start set by caller at the dock */ }
}

function applyPayload(next) {
  payload = next;
  const agents = Array.isArray(next.agents) ? next.agents : [];
  const age = next.snapshot_age_seconds;
  nightMode = !next.snapshot || (age != null && age > 900);
  groundEl.src = nightMode ? view().night : view().day;
  $('#emptyNote').hidden = !!next.snapshot;

  // The Jarl is a first-class villager, merged in like any hero.
  const all = agents.map((a) => ({ ...a }));
  if (next.jarl) {
    all.push({
      id: 'jarl', name: next.jarl.name, role: next.jarl.role, kind: 'the jarl',
      status: next.jarl.status === 'resting' ? 'resting' : 'idle',
      effective_status: next.jarl.status === 'resting' ? 'resting' : 'idle',
      place: next.jarl.place, effective_place: next.jarl.place, station: next.jarl.station,
      current_tool: '', task_label: next.jarl.activity, is_jarl: true, jarl: next.jarl,
      portrait: 'volmarr',
    });
  }

  // Slot assignment per station, stable by id order.
  const byStation = new Map();
  for (const a of all) {
    const t = heroTarget(a, 0, 1);
    const key = t.placeId + ':' + t.station.id;
    if (!byStation.has(key)) byStation.set(key, []);
    byStation.get(key).push(a.id);
  }
  const slotOf = new Map();
  for (const ids of byStation.values()) ids.sort().forEach((id, i) => slotOf.set(id, { i, n: ids.length }));

  const seen = new Set();
  for (const agent of all) {
    seen.add(agent.id);
    const status = agent.effective_status || agent.status;
    let hero = heroes.get(agent.id);
    const isNew = !hero;
    if (isNew) {
      const match = matchPortrait(agent);
      const el = makeHeroEl(agent, match);
      const [dx, dy] = view().dock;
      hero = { id: agent.id, el, x: dx, y: dy, queue: [], speed: 0.1, lastPlaceId: null, nextWander: 0, dwellUntil: 0, wasDone: false };
      heroes.set(agent.id, hero);
    }
    hero.data = agent;
    hero.match = matchPortrait(agent);
    // Rule 5: newly finished work is sworn at the Oathstones before resting.
    const justDone = status === 'done' && !hero.wasDone && !isNew;
    hero.wasDone = status === 'done';
    const slot = slotOf.get(agent.id) || { i: 0, n: 1 };
    if (justDone && !REDUCED) {
      const oath = { ...agent, effective_place: 'oathstones', place: 'oathstones', station: 'ring centre' };
      retarget(hero, oath, 0, 1, false);
      hero.dwellUntil = Date.now() + 4500;
      hero.pendingRetarget = () => retarget(hero, agent, slot.i, slot.n, false);
    } else if (!hero.dwellUntil || Date.now() > hero.dwellUntil) {
      retarget(hero, agent, slot.i, slot.n, isNew);
    }
    paintHero(hero);
  }
  // Departures walk to the dock and fade at the water (rule 4).
  for (const [id, hero] of heroes) {
    if (seen.has(id)) continue;
    const [dx, dy] = view().dock;
    hero.queue = REDUCED ? [] : [{ x: dx, y: dy }];
    hero.speed = 0.16;
    hero.el.classList.add('fading');
    setTimeout(() => { hero.el.remove(); heroes.delete(id); }, REDUCED ? 50 : 1600);
    if (selectedId === id) closeDossier();
  }
  paintFreshness();
  if (selectedId && heroes.get(selectedId)) openDossier(selectedId, true);
}

function paintHero(hero) {
  const agent = hero.data;
  const status = agent.effective_status || agent.status;
  const el = hero.el;
  el.style.left = (hero.x * 100) + '%';
  el.style.top = (hero.y * 100) + '%';
  el.classList.toggle('borrowed', !!hero.match && !hero.match.own && !!hero.match.slug);
  el.classList.toggle('letter', !hero.match?.slug);
  el.classList.toggle('jarl', !!agent.is_jarl);
  el.classList.toggle('waiting-hero', status === 'waiting');
  el.classList.toggle('resting-hero', status === 'resting' || status === 'idle');
  const face = el.querySelector('.face');
  if (!hero.match?.slug && face) face.textContent = (agent.name || '?').trim().charAt(0).toUpperCase();
  const dot = el.querySelector('.dot');
  dot.className = 'dot ' + (status || 'idle');
  dot.title = status;
  const glyph = el.querySelector('.glyph');
  const fam = toolFamily(agent.current_tool);
  const showGlyph = !!fam && GLYPHS[fam] && (status === 'working' || status === 'thinking');
  glyph.hidden = !showGlyph;
  if (showGlyph) glyph.textContent = GLYPHS[fam];
  el.querySelector('.need').hidden = status !== 'waiting';
  el.querySelector('.hname').textContent = agent.name || hero.match?.label || 'Unnamed';
  el.querySelector('.guess-tag').hidden = !agent.is_jarl;
  el.title = `${agent.name || ''} — ${status}${agent.current_tool ? ' · ' + agent.current_tool : ''}`;
}

function paintFreshness() {
  const el = $('#freshness');
  if (!payload) { el.textContent = 'Listening for word…'; return; }
  const agents = Array.isArray(payload.agents) ? payload.agents : [];
  const counts = {};
  for (const a of agents) { const s = a.effective_status || a.status; counts[s] = (counts[s] || 0) + 1; }
  const bits = Object.entries(counts).map(([s, n]) => `${n} ${s}`).join(' · ');
  el.textContent = payload.snapshot
    ? `Word carried ${formatAge(payload.snapshot_age_seconds)} · ${agents.length} heroes in the village${bits ? ' · ' + bits : ''}`
    : 'No word yet — the night painting hangs and the village rests';
}

let lastTick = performance.now();
function tick(now) {
  const dt = Math.min(0.05, (now - lastTick) / 1000);
  lastTick = now;
  for (const hero of heroes.values()) {
    if (hero.pendingRetarget && hero.dwellUntil && now > hero.dwellUntil && hero.queue.length === 0) {
      hero.pendingRetarget(); hero.pendingRetarget = null; hero.dwellUntil = 0;
    }
    if (hero.queue.length) {
      const target = hero.queue[0];
      const dx = target.x - hero.x, dy = target.y - hero.y;
      const dist = Math.hypot(dx, dy);
      const step = hero.speed * dt;
      if (dist <= step) { hero.x = target.x; hero.y = target.y; hero.queue.shift(); }
      else { hero.x += dx / dist * step; hero.y += dy / dist * step; }
      paintHero(hero);
    } else if (!nightMode && !REDUCED && hero.data) {
      // Rule 10: idle strolling stays home — a small loop inside the hero's own place.
      const status = hero.data.effective_status || hero.data.status;
      if ((status === 'idle' || status === 'thinking') && !hero.data.is_jarl && now > hero.nextWander) {
        hero.nextWander = now + 6000 + Math.random() * 8000;
        const [ax, ay] = anchorOf(hero.placeId || 'hearth');
        hero.queue = [{ x: ax + (Math.random() - 0.5) * 0.05, y: ay + (Math.random() - 0.5) * 0.035 }];
        hero.speed = 0.03;
      }
    }
  }
  requestAnimationFrame(tick);
}

/* ---------- Dossier ---------- */
function openDossier(id, keepScroll) {
  const hero = heroes.get(id);
  if (!hero) return;
  selectedId = id;
  const a = hero.data;
  const status = a.effective_status || a.status;
  const place = PLACES[hero.placeId] || PLACES.hearth;
  const others = (payload?.agents || []);
  const children = others.filter((c) => c.parent_id === id);
  const parent = others.find((c) => c.id === a.parent_id);
  const born = a.started_at ? new Date(a.started_at) : null;
  const running = born && !Number.isNaN(born.getTime())
    ? Math.max(0, Math.floor((Date.now() - born.getTime()) / 1000)) : null;
  const runningText = running == null ? '—'
    : running < 3600 ? `${Math.floor(running / 60)}m ${running % 60}s` : `${Math.floor(running / 3600)}h ${Math.floor((running % 3600) / 60)}m`;
  const timeline = Array.isArray(a.timeline) ? a.timeline.slice(-30).reverse() : [];
  const faceHtml = hero.match?.slug
    ? `<img class="${hero.match.own ? '' : 'borrowed'}" src="${hero.match.library ? '/art/portraits-library/' : '/art/portraits/'}${hero.match.slug}.webp" alt="Portrait of ${esc(a.name || '')}">`
    : '';
  $('#dossier').innerHTML = `
    <button class="close-x" type="button" id="dossierClose">✕ Close</button>
    <div class="dossier-head">${faceHtml}<div>
      <h3>${esc(a.name || 'Unnamed hero')}</h3>
      <p>${esc(a.role || a.kind || 'agent')} · ${esc(status)} at ${esc(place.name)} — at the ${esc(hero.station?.label || '')}</p>
      ${hero.match?.slug && !hero.match.own ? `<p>Shown as <strong>${esc(hero.match.label || titleCase(hero.match.slug))}</strong> — the face of her kind, not her own.</p>` : ''}
      ${a.is_jarl ? `<p class="guess-note">The village guesses: “${esc(a.jarl.activity)}”<br>Basis: ${esc(a.jarl.basis)}. A guess, always labelled — never word from the Jarl himself.</p>` : ''}
    </div></div>
    <div class="chips">
      <span class="chip">model <b>${esc(a.model || '—')}</b></span>
      <span class="chip">kind <b>${esc(a.kind || a.agent_type || '—')}</b></span>
      <span class="chip">depth <b>${esc(a.depth ?? '—')}</b></span>
      <span class="chip">running <b>${esc(runningText)}</b></span>
      <span class="chip">tokens <b>${esc(a.tokens != null ? Number(a.tokens).toLocaleString() : '—')}</b></span>
      ${a.context_pct != null ? `<span class="chip">context <b>${esc(a.context_pct)}%</b></span>` : ''}
      ${a.is_stale ? '<span class="chip">word gone stale — resting</span>' : ''}
    </div>
    <dl>
      ${a.task_brief || a.task_label ? `<dt>Her orders</dt><dd>${esc(a.task_brief || a.task_label)}</dd>` : ''}
      ${a.current_tool ? `<dt>Tool in hand</dt><dd>${esc(a.current_tool)}</dd>` : ''}
      ${a.latest_word ? `<dt>Her latest word</dt><dd>${esc(a.latest_word)}</dd>` : ''}
      ${a.coordinator_word ? `<dt>The coordinator's word</dt><dd>${esc(a.coordinator_word)}</dd>` : ''}
      ${a.final_word ? `<dt>Her final word</dt><dd>${esc(a.final_word)}</dd>` : ''}
      ${!a.task_brief && !a.latest_word && !a.is_jarl ? '<dt>Word</dt><dd>No deeper word yet — the bridge has carried her name and state, nothing more. The panel says so rather than dressing up thin air.</dd>' : ''}
    </dl>
    ${parent ? `<p>Parent: <button class="kin-link" data-open="${esc(parent.id)}">${esc(parent.name || parent.id)}</button></p>` : ''}
    ${children.length ? `<p>Children: ${children.map((c) => `<button class="kin-link" data-open="${esc(c.id)}">${esc(c.name || c.id)}</button>`).join(' · ')}</p>` : ''}
    ${timeline.length ? `<dt>Timeline of her doings (newest first)</dt><ul class="timeline">${timeline.map((t) => `<li><span class="t">${esc(t.ts ? new Date(t.ts).toLocaleTimeString() : '')}</span> ${esc(t.tool || '')} — ${esc(t.description || '')} <span class="t">${esc(t.status || '')}</span></li>`).join('')}</ul>` : ''}
  `;
  $('#dossier').hidden = false;
  $('#dossierClose').addEventListener('click', closeDossier);
  $('#dossier').querySelectorAll('[data-open]').forEach((b) => b.addEventListener('click', () => openDossier(b.dataset.open)));
  if (!keepScroll) $('#dossier').scrollIntoView({ behavior: REDUCED ? 'auto' : 'smooth', block: 'nearest' });
}
function closeDossier() { selectedId = null; $('#dossier').hidden = true; }

/* ---------- Places: waypoints + panels ---------- */
function paintWaypoints() {
  const wrap = $('#waypoints');
  wrap.innerHTML = '';
  for (const id of PLACE_ORDER) {
    const [x, y] = view().anchors[id];
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'waypoint' + (id === 'watch' || id === 'longhouse' ? ' below' : '');
    btn.style.left = (x * 100) + '%';
    btn.style.top = (y * 100) + '%';
    btn.dataset.place = id;
    btn.innerHTML = `${esc(PLACES[id].name)} <small data-count>0</small>`;
    btn.addEventListener('click', () => openPlace(id));
    wrap.appendChild(btn);
  }
}
function refreshWaypointCounts() {
  const counts = {};
  for (const hero of heroes.values()) counts[hero.placeId] = (counts[hero.placeId] || 0) + 1;
  document.querySelectorAll('.waypoint').forEach((w) => {
    w.querySelector('[data-count]').textContent = counts[w.dataset.place] || 0;
  });
}
setInterval(refreshWaypointCounts, 1500);

function openPlace(id) {
  const place = PLACES[id];
  const occupants = [...heroes.values()].filter((h) => h.placeId === id);
  $('#placePanel').innerHTML = `
    <button class="close-x" type="button" id="placeClose">✕ Close</button>
    <h3>${esc(place.name)}</h3>
    <p>${esc(place.duty)} — ${occupants.length} here now</p>
    ${place.stations.map((s) => {
      const here = occupants.filter((h) => h.station?.id === s.id);
      return `<div class="station-row"><span>${esc(s.label)}</span><span class="who">${here.length
        ? here.map((h) => esc(h.data.name || h.id)).join(', ')
        : 'free'}</span></div>`;
    }).join('')}`;
  $('#placePanel').hidden = false;
  $('#placeClose').addEventListener('click', () => { $('#placePanel').hidden = true; });
}

/* ---------- Roster (muster roll) ---------- */
async function paintRoster() {
  try {
    const [roster, traits] = await Promise.all([
      fetch('/api/roster').then((r) => r.json()),
      fetch('/data/PORTRAIT_TRAITS.json').then((r) => r.ok ? r.json() : []).catch(() => []),
    ]);
    TRAITS = Array.isArray(traits) ? traits : [];
    const craftOf = new Map(TRAITS.map((t) => [t.slug, t.craft]));
    $('#castGrid').innerHTML = (roster.cast || []).map((c) => `
      <div class="roster-cell"><img src="${esc(c.url)}" alt="Portrait of ${esc(CAST[c.slug] || c.slug)}" loading="lazy">
      <p>${esc(CAST[c.slug] || titleCase(c.slug))}</p></div>`).join('');
    $('#libraryGrid').innerHTML = (roster.library || []).map((c) => `
      <div class="roster-cell"><img src="${esc(c.url)}" alt="Portrait of the ${esc(titleCase(c.slug))}" loading="lazy">
      <p>${esc(titleCase(c.slug))}</p><p class="craft">${esc(craftOf.get(c.slug) || '')}</p></div>`).join('');
    if (payload) applyPayload(payload); // re-match faces now that traits are known
  } catch { /* roster is decoration over truth; the map still stands */ }
}

/* ---------- Feeds: SSE first, honest polling behind it ---------- */
async function fetchVillage() {
  try { applyPayload(await (await fetch('/api/village')).json()); } catch { /* next poll tries again */ }
}
function connectEvents() {
  try {
    const es = new EventSource('/api/events');
    es.addEventListener('village', (e) => { try { applyPayload(JSON.parse(e.data)); } catch { /* keep last good word */ } });
    es.onerror = () => { /* EventSource retries on its own; polling below covers the rest */ };
  } catch { /* polling covers it */ }
}
$('#refreshBtn').addEventListener('click', fetchVillage);
$('#spinBtn').addEventListener('click', () => {
  viewIndex = (viewIndex + 1) % VIEWS.length;
  $('#viewName').textContent = view().name;
  groundEl.src = nightMode ? view().night : view().day;
  paintWaypoints();
  // Rule 6 on a turned view: heroes still walk to their places in the new angle.
  if (payload) applyPayload(payload);
});

paintWaypoints();
paintRoster();
fetchVillage();
connectEvents();
setInterval(fetchVillage, 30000);
requestAnimationFrame(tick);
