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

## The Jarl walks (follow-up, 2026-11-13)

*Volmarr's order, verbatim: "but I also should be able to walk around if I
want to my witchie." Built the same day, on top of the hall above.*

**Server.** `POST /api/jarl/move` accepts `{ "place": "<place id>" }` for
any of the eight village places. The seven halls beyond the Longhouse are
*visits*; `"longhouse"`, a missing place, or JSON null is the **return** —
it clears the choice and hands placement back to the guess engine. An
unknown place is a 400 with the valid ids listed. The choice is state, not
a guess: it lives in `Jarl.ChosenPlace`, persists in one small file
(`jarl-state.json` — default `LocalApplicationData/Runheim/` on every OS,
overridable via `Runheim:JarlStatePath`, `"~/"` expands), is written on
every move, deleted on return, and loaded at startup. Snapshot ingest
cannot override it *by construction*: snapshots carry no jarl state at all
— his placement is computed server-side from his own word. While he
visits, `GET /api/village` reports `jarl.place`/`jarl.station` from the
visit (the Forge's great anvil, the Ring's judges' bench, the Watch's
tower rail, the Hearth Hall's hearth fire, the Library's great map table,
the Seiðr House's high seat, the Oathstones' ring centre), `status:
"visiting"`, and `chosen_place` naming the place (it is JSON `null` when
the guess engine governs). The activity text stays a labelled guess —
*"visiting the Dvergar Forge — the village guesses he is inspecting the
work"* — with the basis leading **"place chosen by the Jarl himself"**,
ahead of the usual time/session signals. Every move broadcasts the fresh
payload over SSE, like a snapshot does.

**Client.** The Jarl's figure is selectable: clicking him opens his
dossier *and* arms **walk mode** (so does the **Walk the Jarl** header
control; Esc or the control again disarms it). In walk mode the place
waypoints turn gold and become destinations — clicking one posts the move
(the Longhouse waypoint brings him home) and he walks there by the
existing road movement at a stately pace to that place's Jarl spot. His
dossier carries **Walk to…** buttons for all eight places plus **Return to
the high seat**, with the place he is at marked. A snapshot agent that
claims the id `jarl` is ignored by the client merge — the server's jarl
object is the only word about him. Movement honesty is unchanged: guesses
are labelled, and the chosen place is never dressed as a guess.

**Cross-platform, re-proved.** All five RIDs republished framework-
dependent from this build (total published bytes, all files):

| RID | Output size | Files | Proof |
| --- | ---: | ---: | --- |
| win-x64 | 260,477 B | 10 | publishes clean (apphost `Runheim.Server.exe` present) |
| linux-x64 | 180,966 B | 9 | publishes clean **and ran** (VM + purpleraider, below) |
| linux-arm64 | 181,150 B | 9 | publishes clean |
| osx-x64 | 190,494 B | 9 | publishes clean |
| osx-arm64 | 214,206 B | 9 | publishes clean |

Same honesty as before: only linux-x64 was *run*; the rest publish clean.

**Run proofs.** On the build VM (linux-x64 publish, port 4480): default
state guess-governed (`chosen_place: null`); move → forge / great anvil /
visiting, exact activity text, basis leading with his choice, state file
holding `{"chosen_place":"forge"}`; snapshot ingest → place unchanged;
invalid place → 400; **process killed and relaunched → still visiting the
Forge**; return → Longhouse, state file gone, basis back to guess signals
only; the served page carries the Walk the Jarl control and the client
posts to `/api/jarl/move`. On **purpleraider** the new linux-x64 publish
was carried over the same receiver road as the first build (tar
sha256 `94dd9cc0…f110`, verified byte-identical on the device), swapped
into `~/runheim-hall/server/` (the first build is kept beside it as
`server.bak-20261113`), and relaunched in tmux `runheim` on port 4477 with
the identical command line. The on-device battery passed **every check**:
move round-trip with his real session in the basis (*"place chosen by the
Jarl himself + local time 09:56 + seat0 active, idle 0s"*), restart →
choice survived, snapshot → place unchanged, return → the morning-counsel
guess governs again, invalid place → 400. The proof snapshot was cleared
afterwards; the hall was left running, the Jarl home at the high seat,
exactly as found but for the new build.

**Two seams, recorded plainly.** (1) In the local test run,
`appsettings.json`'s `Urls` (4477) outranked the `ASPNETCORE_URLS`
environment variable — a known `WebApplication` configuration-order quirk;
the battery therefore passed `--urls` on the command line (which outranks
both). No shipped serve path is affected: the systemd unit, launchd plist,
Windows script and the device's tmux line all agree on 4477. (2) The first
attempt to land the on-device battery script as a hand-pasted base64 block
corrupted in transit and failed its integrity check before anything ran;
the script was re-sent over the proven receiver road and sha256-verified
(`21161018…a163`) — no unverified bytes ever executed.
