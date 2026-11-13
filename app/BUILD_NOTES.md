# Rúnheim Live Hall — Build Notes

*Built 2026-11-13 at Volmarr's order: "make a version that can be served from
a device using C#" — widened by his steering: "make sure is as cross platform
as possible, always!", and by the Jarl requirement: "also I should be there
too and it can guess about what I am doing!"*

**How it was built:** the six Mythic Engineering roles were run sequentially
by one pair of hands (platform depth: team leads cannot spawn their own crews
here) — Forge-Master (shape of the build), Plain-Speaker (behaviour in plain
words first), Map-Keeper (these notes), Juggernaut (the code), Gate-Warden
(the proofs below), Lesson-Carver (the lessons at the end). Recorded plainly,
as the Forge Protocol requires.

## What it is

`Runheim.Server` — a .NET 8 ASP.NET Core minimal-API app, **framework-only
dependencies** (no third-party NuGet packages), that serves the Viking Agent
Village from any device: the painted map with three spin views in day and
night dress, heroes who walk the roads to named work stations, portrait
matching by kind against the 60-archetype trait library, full dossiers, the
muster roll, and a live feed (server-sent events) driven by bridge snapshots.

| Surface | Behaviour |
| --- | --- |
| `GET /` | The hall UI (vanilla HTML/CSS/JS, no build step) |
| `GET /art/…` | The repo's own `art/` folder, read-only, from a configurable root |
| `GET /data/…` | The repo's `data/` folder (the trait library the matcher scores against) |
| `GET /api/health` | Liveness + whether a snapshot is held + art/data root health |
| `GET /api/village` | Current village payload: snapshot, enriched agents, snapshot age, and the `jarl` object |
| `POST /api/snapshot` | Accepts a bridge snapshot (same shape the Scrying Mirror ingests: an object with an `agents` array); replaces state and pushes it to every open hall window |
| `POST /api/clear` | Clears the snapshot (the village honestly shows its empty night) |
| `GET /api/events` | SSE stream: the full village payload on connect and on every change, 25 s heartbeat |
| `GET /api/roster` | Cast + portrait-library listings scanned from the art root |

**Snapshot honesty, enforced server-side:** an agent whose word is older
than fifteen minutes (stale snapshot, or stale `last_seen`) is served with
`effective_status: "resting"` and `effective_place: "hearth"` beside the
snapshot's own words — stale word is never dressed as live work. Finished
and failed work keeps its own truth. The hall holds state in memory only:
a restart shows the empty night until fresh word crosses, by design.

**Configuration** (appsettings.json, environment, or command line):
`Runheim:ArtRoot` (default `../art`), `Runheim:DataRoot` (default
`../data`), standard `ASPNETCORE_URLS` (default `http://0.0.0.0:4477`).
Roots resolve with `Path.Combine`/`Path.GetFullPath` only; a leading `~/`
in a config value expands to the user's home on every OS; environment
overrides follow the usual `Runheim__ArtRoot` form. If a configured root
does not exist, the server walks up from its content root looking for the
sibling `art`/`data` folder, so a fresh clone runs with zero configuration;
a truly missing root is a logged warning and honest 404s, never a crash.

## The Jarl in the village

Volmarr is a first-class villager: his own portrait
(`art/portraits/volmarr.webp`, never a "shown as"), role **The Jarl**, on the
map and in the roster like any hero, with a dossier. His activity is a
**guess — authorized by him for himself only** — and is always labelled:
`activity_is_guess: true` in the API, "the village guesses…" in the UI, with
`basis` naming the signals used. The guess composes, in order:

1. **Local time-of-day bands** on the host (deep night 00:00–05:29 →
   "dream-walking — the village keeps the watch", resting at the Hearth
   Hall; morning → "taking his morning counsel with his tablet", Longhouse
   high seat; day → "surveying his fleet at the war-map board"; evening →
   "by the long fire").
2. **On Linux only**, his login session via `loginctl` (seat0 active /
   IdleHint / IdleSinceHintMonotonic against /proc/uptime), best-effort
   behind a platform guard with a 2 s timeout and a 15 s cache. A session
   idle for an hour or more (outside deep night) overrides the band:
   "about his earthly business, away from the hall". Other OSes — and any
   probe failure — fall back to the time bands alone, and the basis says so.
3. **Bridge-snapshot recency**: fresh Muse word (≤ 15 min) is named in the
   basis — it implies he is near his machines.

This probe is the only platform-conditional code in the build; everything
else is uniform managed code. No registry, no syscalls, no P/Invoke.

## Cross-platform proof

Publish-proved **framework-dependent** for all five RIDs from this Linux
build host (2026-11-13, SDK 8.0.428). Proof level per RID is exactly
**"publishes clean"** — except linux-x64, which was also *run* (below).
A publish is not a run, and these notes never claim one that did not happen.

| RID | Output size | Files | Proof |
| --- | ---: | ---: | --- |
| win-x64 | 248,000 B | 10 | publishes clean (apphost `Runheim.Server.exe` present) |
| linux-x64 | 168,485 B | 9 | publishes clean **and ran** (VM + purpleraider, below) |
| linux-arm64 | 168,669 B | 9 | publishes clean |
| osx-x64 | 178,017 B | 9 | publishes clean |
| osx-arm64 | 201,729 B | 9 | publishes clean |

Reproduce: `app/scripts/publish-all.sh` / `publish-all.ps1`.

**Serve path per OS** (no Docker anywhere, ever): Linux →
`app/runheim.service` (systemd user unit); macOS → `app/runheim.plist`
(launchd agent); Windows → `app/scripts/run-windows.ps1`. A developer's
run from source: `app/scripts/run-local.sh`.

## Run proofs (what actually ran, where)

**On the build VM (linux-x64, Debug build):** health OK with art/data
roots found by the clone walk-up; `/`, paintings, portraits and the traits
JSON all served (roster: **54 cast + 60 library**, matching the repo);
`POST /api/snapshot` round-tripped 3 agents with correct effective
place/status; SSE emitted the village event on connect; an agent whose
`last_seen` was 32 minutes old came back `resting @ hearth, is_stale: true`;
`POST /api/clear` emptied the village honestly.

**On purpleraider (linux-x64 Release publish, the device's own .NET
8.0.33):** the publish output plus six village paintings, three portraits
and the trait library were carried to `~/runheim-hall/` and verified
sha256-identical to the build host; the hall runs there still, in tmux
session `runheim`, bound to `0.0.0.0:4477` behind the machine's firewall.
On-device proofs: health OK; snapshot round-trip; index and painting
served (829,776 B). **The Jarl proof:** at 09:45 EST the payload read
*"taking his morning counsel with his tablet"* — Longhouse, high seat —
with basis **"local time 09:45 + seat0 active, idle 0s"**: the loginctl
signal read his real, live session. Device art scope for this proof was
deliberately the map set (6 paintings) + 3 portraits, not the whole
114-piece wall; the server code is identical either way.

## Toolchain note (a lesson, carved)

The VM had no .NET SDK. The official `dotnet-install.sh` (channel 8.0)
downloaded the 8.0.428 tarball fine, but its extraction aborted in this
sandbox (tar cannot chown SDK files to uid 2001 inside the user namespace,
and one attempt also reported a spurious "No space left"). The same
official tarball from the official CDN was extracted directly to
`~/dotnet`: **4,704 of 4,704 files**, `dotnet --list-sdks` → 8.0.428, and a
smoke console build passed with 0 errors before any app code was written.
No Docker, no sudo, no third-party source.

## Known bounds

- Map anchors and station offsets are data in `wwwroot/js/village.js`
  (three views × eight places), estimated against the paintings — tune
  them there; nothing else needs to change.
- The movement engine implements the ten travel rules in the client's
  honest subset: road-hub routing, dock arrivals/departures, the
  Oathstones dwell for newly-finished work, the waiting hero's right of
  way to the Longhouse door post, pace-by-state with the ~6-second cap,
  night settling, and home-bound idle strolling.
- Adapters (Muse records, Hermes, Claude Code, Codex) remain design slices
  V-01…V-10 of `docs/VILLAGE_PLAN.md`; this hall is fed by the same bridge
  snapshots as the mirror until they land.
