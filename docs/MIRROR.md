# The Scrying Mirror — the living prototype

**Rúnheim — Viking Agent Village — Mirror** is a fullstack Space in Muse
(slug `viking-agent-village-mirror`). It is the near-real-time face of the
village: it cannot reach into Volmarr's home network itself, so a
**bridge-keeper** — a scheduled agent job, the same species as the campaign
keepers — reads the live Muse agent records every ten minutes and carries a
snapshot across into the mirror. The mirror always states its snapshot's age
and never fakes freshness: heroes whose word is older than fifteen minutes
are shown resting at the Hearth Hall, and the night painting comes out when
the word is old.

## What it renders

- The painted village map on the grand-Longhouse grounds (day and night),
  with three spin-the-view angles as data — Village Green, Hill Overlook,
  Oathstone Shore — each carrying its own place anchors and road graph.
- Every hero wears a portrait: named cast members wear their own; everyone
  else is matched by kind — first against the cast archetypes, then scored
  against the 60-archetype trait library (`data/PORTRAIT_TRAITS.json`) —
  with a dashed purple ring and a "shown as" line wherever the face is
  borrowed.
- Heroes walk between places on roads (never crow-flight), at paces that
  tell their state, under the ten travel rules (`docs/TRAVEL_RULES.md`),
  and stand at named work stations inside each hall, matched to the tool
  they are currently using, with a small animated glyph of their real work
  beside them.
- Tapping any hero opens a full dossier: portrait, model / depth / born /
  running-for / token stat chips, the complete task brief, their latest own
  words, coordinator decisions, a newest-first timeline of their tool
  activity, tappable parent and children, and their final word when done.

## Actions

| Action | What it does |
| --- | --- |
| `getvillage` | Returns the current village state (latest snapshot + roster). |
| `ingestsnapshot` | Accepts a bridge snapshot (agents, dossiers, events). |
| `refreshmirror` | Dispatches the bridge-keeper for fresh word on demand. |
| `clearsnapshot` | Clears the stored snapshot (the village shows its empty night). |

## Provenance & limits

The mirror's application code lives in the hosted Space runtime; this
repository carries the complete design, art, data, and research the Space is
built from. The fully self-hosted live hall (second-grain, on
purplelongship) is build slice V-01 onward in `docs/VILLAGE_PLAN.md`.
Privacy law throughout: tool names, task labels, statuses, and counts only —
never message contents, never reasoning.
