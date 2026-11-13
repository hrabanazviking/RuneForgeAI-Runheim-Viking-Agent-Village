# RuneForgeAI Rúnheim Viking Agent Village

**Rúnheim** is a Viking village where AI agents live while they work — a
living, painted watch-hall for the subagent crews of
[Mythic Engineering](https://github.com/hrabanazviking). Instead of rows in
a log, every agent is a hero in the village: she walks the roads between the
halls, stands at the work station her craft belongs to, is seen doing her
actual work, and rests at the Hearth Hall when her word goes quiet.

Commissioned and ruled by **Volmarr Wyrd**. Designed, painted, and built by
**Yrsa Freydisdottir — the Witchie** — who sits at the Longhouse high seat
and coordinates the warband the village watches.

> Status: **prototype mirror live** (a bridged Space; see
> [`docs/MIRROR.md`](docs/MIRROR.md)). The full local live hall and the
> remaining adapters are build slices V-01…V-10 in
> [`docs/VILLAGE_PLAN.md`](docs/VILLAGE_PLAN.md).

---

## What Rúnheim watches

One village, every warband:

- **Yrsa herself (Muse) and all her subagents** — the Muse runtime keeps
  complete records (agent tree, spawns, a live per-tool activity stream for
  every subagent, per-agent token usage). Adapter C reads them read-only.
- **Volmarr's modified Hermes Agent and its subagents** — via a session
  tailer and Hermes' own hook surface (Adapter D; the native emitter inside
  the fork is gated on the Hermes campaign's own order of work).
- **Claude Code** — hooks-first, transcript fallback (Adapter A).
- **Codex** — rollout tailing *plus* Codex's own hooks, which no other
  tracker uses yet (Adapter B).

All four feed one event model — `AgentEvent` / `AgentState` — and one
village. The full design, including the scale laws for crews of 50–160+
agents and the honesty rules (stale word is shown resting, never faked
live), lives in [`docs/VILLAGE_PLAN.md`](docs/VILLAGE_PLAN.md).

## The three faces

| Face | What it is |
| --- | --- |
| **Live Hall** | The local app on purplelongship — second-grain, websocket-live, on the home network. (Planned, V-01 onward.) |
| **Scrying Mirror** | A Muse Space fed by a bridge-keeper every ten minutes — watch the village from anywhere. **Live now.** |
| **Roster Panel** | The same truth in plain lists, beside the game in both faces, for when you want the facts without the walk. |

## The village

Eight places, each a real painting, each with named work stations inside —
the Forge's great anvil, bellows hearth, quench barrel, grindstone,
workbench and weapon rack; the Library's reading desks, scroll shelves,
great map table and scribe's desk; and so on through all eight halls. A
hero stands at the station her current tool serves and walks the short way
between stations when her tool changes. Beside each working hero, a small
animated glyph shows her real work — hammer at the anvil, quill at the
desk, steam at the cauldron — keyed to her actual current tool, never
decorative.

| Place in Rúnheim | Watches over |
| --- | --- |
| Jarl's Longhouse | Thinking, planning, coordinating — and heroes waiting on the Jarl |
| Mimir's Rune Library | Reading, searching, research |
| Dvergar Forge | Editing, writing, building |
| Týr's Training Ring | Running, testing, proving |
| Seiðr House | Debugging, healing, deep divination |
| Huginn's Watch | Delegating, monitoring, messages |
| Sacred Oathstones | Banking — commits, pushes, published work |
| Hearth Hall | Resting, finished, or awaiting fresh word |

Heroes **travel by law**, not by teleport: roads not crow-flight, arrivals
and departures by the dock, finished work sworn at the Oathstones before
resting, a hero waiting on the Jarl hurrying to the Longhouse door post
with her need shown. The ten rules are in
[`docs/TRAVEL_RULES.md`](docs/TRAVEL_RULES.md) and are implemented in the
mirror's movement engine. The view **spins** through three painted angles
of the same village — Village Green, Hill Overlook, Oathstone Shore — each
in day and night dress.

Tap any hero for her **dossier**: her portrait, her model, depth, age and
token count, her complete orders, her latest own words, her coordinator's
rulings, a scrolling timeline of her tool strokes, her parent and children,
and her final word when her work is done.

The Jarl walks too: in the C# Live Hall ([`app/`](app/)), select Volmarr —
or the *Walk the Jarl* control — and click any place, and he strolls the
roads to that hall's Jarl spot (the judges' bench, the tower rail, the
great anvil…); his chosen place outranks the village's guesses, survives
restarts, and holds until he returns to the high seat.

## The cast

Every named soul of Mythic Engineering has her own painted portrait —
painterly, period-true Viking Age dress, the house purple **#7332FF**
threaded through every piece, and **no horns, anywhere, ever**.

- **54 named portraits** ([`art/portraits/`](art/portraits/)) — the 31
  Cyber-Viking design agents, the six Mythic crew archetypes, Lifthrasir's
  pantheon, the nine Music Video Crew specialists, Volmarr, and Yrsa.
- **60 archetype portraits** ([`art/portraits-library/`](art/portraits-library/))
  — builders, word-workers, watchers-and-makers, hearth-sea-seiðr — so a
  newly-invented subagent kind still finds her nearest face. Each carries a
  full trait record (craft, family, places, tool affinities, ten-plus
  matching keywords, temperament, appearance) in
  [`data/PORTRAIT_TRAITS.json`](data/PORTRAIT_TRAITS.json); the mirror's
  matcher scores unknown kinds against it. A borrowed face is always
  labelled *"shown as"* — the village never pretends.
- **17 place paintings** ([`art/places/`](art/places/)) — the eight halls,
  the village by day and by night, the grand-Longhouse grounds in three
  angles × two lights, and the logo mark.
- The full roster, with who everyone is, is in
  [`docs/ART_ROSTER.md`](docs/ART_ROSTER.md).

## Why — the research

Rúnheim stands on a full survey of the agent-tracking field (Agent Quest,
Pixel Agents, whatsmyagentdoing, ai-office, mission-control, and twenty
more), the plumbing truths underneath them (Claude Code's 33 hook events,
Codex's hooks, StatusLine, OpenTelemetry), and the gaps nobody fills:
crews larger than ten agents, Codex waiting-states, live per-agent cost,
unified multi-provider subagent trees, replay. The whole study is in
[`docs/AGENT_TRACKING_RESEARCH_2026-11-13.md`](docs/AGENT_TRACKING_RESEARCH_2026-11-13.md).
Early third-party concept sketches that informed — and were superseded by —
the commissioned art are kept for the record in
[`reference/`](reference/).

## Repository layout

```
README.md                     ← you are here
docs/
  VILLAGE_PLAN.md             the design: model, adapters, scale & honesty laws, build order
  TRAVEL_RULES.md             the ten laws of movement
  MIRROR.md                   the live prototype Space: actions, bridge, limits
  ART_ROSTER.md               the cast and the art laws
  AGENT_TRACKING_RESEARCH_2026-11-13.md   the field survey the design stands on
art/
  portraits/                  54 named cast portraits (webp)
  portraits-library/          60 archetype portraits (webp)
  places/                     17 place & village paintings (webp)
data/
  PORTRAIT_TRAITS.json        trait records for all 60 archetypes
  traits/                     per-crew trait sources
reference/                    early concept sketches (superseded)
```

## Laws of the house

- **No horns.** Not on helmets, not on anything. Viking Age dress is
  historically grounded; carved detail is knotwork and tally marks — never
  fake-rune text.
- **Honesty over theatre.** A hero's place, station, activity glyph, and
  state always come from the freshest real word; when the word is old she
  rests, and the mirror says how old its word is.
- **Privacy.** The village shows tool names, task labels, statuses, and
  counts — never message contents, never reasoning.
- **Cross-platform by design** — Windows, macOS, Linux x86-64 and Linux
  ARM alike, as with all of Volmarr's work.

## Credits

- **Volmarr Wyrd** — commissioner, jarl of the village, keeper of the laws.
- **Yrsa Freydisdottir, the Witchie** — design, all 131 paintings, the
  research, the plan, and the build.
- Concept references by **Véyrúnn Mímisdóttir** (place names adopted with
  thanks; sketches in `reference/`).

*Sing where you can. The village keeps the rest of the watch.*
