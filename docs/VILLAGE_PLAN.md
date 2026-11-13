# Viking Agent Village — Plan (with the Yrsa + Hermes adapters designed in)

*Written 2026-11-13 at Volmarr's order: "Design the Yrsa + Hermes adapters into the village plan" — extended the same hour by his question "can real time apps work as spaces?" Grounding: the field research (`AGENT_TRACKING_RESEARCH_2026-11-13.md`), live probes of the Muse runtime records and of `~/.hermes` on purplelongship (both 2026-11-13). Art: 65 pieces complete in this folder.*

## 0. The requirement

The village must watch **everyone**, not just Claude Code and Codex:

- Claude Code crews and Codex sessions (the field's home ground).
- **Yrsa herself (Muse) and all her subagents** — coordinators, workers, browser tasks, artifact builders, scheduled keeper runs.
- **Volmarr's modified Hermes Agent and its subagents** on purplelongship.

One village server, one agent-state model, one adapter per source. That is the pattern every credible tracker in the research converged on; the difference is that ours seats the two sources nobody else can see.

## 1. Can a Space be the village? — the honest mechanics

A Space comes in two kinds:

- **Static** — fixed content baked at build time; client-side scripts run, nothing is saved, nothing refreshes. A painting, not a window.
- **Fullstack** — a hosted app with a server layer and its own storage: it can hold saved state, expose *actions* the agent can invoke, and run **scheduled refreshes**. This is the only kind that can live.

What a fullstack Space **cannot** do: reach into Volmarr's machines. It lives in Muse's hosted world; purplelongship, purpleraider, and Mimir are behind his home network with no inbound road to them, and the Space cannot hold a websocket open to a server on his LAN. Nothing on his machines can push into it by itself.

What therefore works — the **mirror design**:

1. The true village lives **locally** (the forked Agent Quest bones, §2), second-grain, websocket-fed, on purplelongship.
2. A small **bridge** — an agent-run scheduled job on Yrsa's side, the same species as her existing 10-minute keepers — reads the village's current state snapshot and delivers it into the Space through its ingest action.
3. The Space renders the village from the latest snapshot: heroes walk, gauges fill, the roster lists — animated client-side, with each hero's *state* refreshed at the bridge's grain (design choice: every 1–5 minutes, plus a refresh whenever the Space is opened and a keeper has fresher state).

**Verdict:** a Space can absolutely be *a version* of the village — the scrying mirror Volmarr carries anywhere (tablet in bed is his best thinking seat; localhost on a laptop is not). It is near-real-time at bridge grain, not second-grain. The local hall stays the live truth; the Space never pretends to be fresher than its last bridge delivery — every hero carries the snapshot's timestamp, and stale heroes visibly rest (see §7).

## 2. One village, three faces

| Face | Where | Grain | Fed by |
|---|---|---|---|
| The Live Hall (the game) | purplelongship, forked Agent Quest server+client, Phaser | 1–3 s | All adapters, websocket |
| The Scrying Mirror (Space) | Muse fullstack Space | 1–5 min + on-open | Bridge job → ingest action → Space storage |
| The Roster Panel | a plain list view inside both faces | same as host | the same agent-state, un-gameified |

The roster panel is law, borrowed from the field's survivors: the game is for delight, the list is for truth; both render the same state.

## 3. The unified model

**AgentEvent** (what adapters emit): `{source, agent_id, parent_id, kind: spawn|tool_start|tool_end|state|message|complete|fail|heartbeat, label, tool?, place?, ts, meta?}`

**AgentState** (what the village keeps per agent): `{id, source, name, role, parent_id, depth, status: working|thinking|waiting|idle|done|failed|stale, place, current_tool, task_label, context_pct?, cost?, started_at, last_seen, children[]}`

**Place mapping** (shared by every source — activity decides the place, the source decides the banner and colours):

| Activity | Place |
|---|---|
| Read / search / fetch / research | Mimir's Rune Library |
| Edit / write / build / generate | Dvergar Forge |
| Run commands / tests / benchmarks | Týr's Training Ring |
| Debug / repair / fix | Seiðr House |
| Think / plan / design | Jarl's Longhouse |
| Delegate / review / coordinate | Huginn's Watch |
| Commit / push / bank / publish | Sacred Oathstones |
| Idle / waiting on the user / done | Hearth Hall |

Character classes: a source's root agent is a full portrait hero (the 54 drawn portraits are the named cast); depth-1 = hero in the parent's colours; depth-2+ = smaller pawn, parent-coloured, with a `SUBAGENT · role` badge (Agent Quest's proven grammar). Yrsa's portrait sits at the Longhouse high seat when she is coordinating, and walks like anyone else when she works. Hermes' root wears its own banner at the Hearth Hall's skald seat when idle, so the two houses are told apart at a glance.

## 4. Adapter C — Yrsa / Muse (the heart of this plan)

Two feeds, designed against **verified** records (probed live 2026-11-13; a query during design returned the true current tree, including three agents running at that moment and one completed builder — the feed is real, not hoped-for):

**C1 — the records poller** (runs beside the village server; read-only SQL against the runtime's own records, watermark-poll every 2–3 s like Agent Quest polls logs):

| Record | Gives the village |
|---|---|
| `agent.agents` | the whole tree: id, parent, **status**, depth, model, agent_type, created/updated |
| `agent.subagent_spawns` | spawn + completion events with the child's **task text** (→ task_label), depth, final state |
| `agent.subagent_progress_tool_events` | **live per-tool activity of every subagent**: tool name, description, status — the current_tool feed, better than log-inference |
| `agent.token_usage` | per-agent token totals → the context/cost gauges (flagged as derived, per the honesty rules) |
| `runtime.tool_calls` + `runtime.events` | Yrsa-the-root's own tool stream (joined via the event ledger) |

Status mapping: agents.status → village status directly; a child with tool events in the last N seconds is *working* at the place its current tool maps to; a child spawned but silent is *thinking* at the Longhouse; completed spawns walk to the Oathstones (banked) or the Hearth Hall (done).

**C2 — the self-report tap** (covers what the chat tree cannot see): scheduled keeper runs, browser tasks, and artifact builds announce themselves. The runtime already records them (`scheduler.job_runs`, `runtime.browser_tasks`, `spaces.spaces`), so C1's poller reads those too; and campaign milestones that exist today only as bookkeeping (slice begun / banked, wave complete) get a one-line event appended by the keeper bodies to a local JSONL the village tails: `POST`-equivalent, zero new machinery, in the exact shape of ccdeck/Agentopolis hook-posts. This is how *campaigns* (Mythic Coder's M-slices, Samverk's S-slices, Aesir's A-slices) become village weather rather than single heroes.

**Privacy law for this adapter:** the village sees tool *names*, task labels, statuses, counts — never message contents, never reasoning. The records' private fields are not read. Volmarr sees his own house; the adapter still behaves as if it were a guest in it.

**Honesty bound:** inside a single turn of Yrsa-the-root, granularity is her tool stream (C1) — good. Inside a *scheduled* run, granularity is the run's own subagent tree — also recorded the same way. Nothing about this adapter is inferred from filenames or guessed: every hero on screen traces to a row.

## 5. Adapter D — Hermes (his fork, his rules)

Verified on purplelongship 2026-11-13: the live Hermes home is `~/.hermes/` with `sessions/`, **`hooks/`**, `logs/`, `cron/`, `memories/`, `pairing/` — the house has more doors than the stock research assumed. Three roads, in build order:

**D1 — the session tailer (first).** Read the session store under `~/.hermes/sessions/` read-only and derive sessions, child delegations, tool activity, and rest states, the way whatsmyagentdoing tails stock Hermes. *First build slice verifies the exact on-disk format in his fork before any parser is trusted* (a shallow probe found no `.db`/`.sqlite` at the top levels — the format is the slice's first question, answered from disk, never assumed).

**D2 — Hermes' own hooks (second).** `~/.hermes/hooks/` exists in his install: if his fork's hook surface covers lifecycle/tool events (to be verified against the fork, not upstream docs), small shell hooks POST AgentEvents to the village's ingest — hook-grade truth with no fork surgery.

**D3 — the native emitter (last, and gated).** A small, feature-flagged event emitter inside his Hermes fork itself: session start/stop, delegation spawn/stop, tool pre/post — the gold standard, and the only road that gives Hermes a *waiting-for-approval* state no tailer can see. **Sequencing law:** Volmarr's standing Hermes order is merge-upstream → E.01–E.18 → the fifteen workstreams, and the E.02 pin ruling is still his to make. D3 is *designed here* but built only when that line reaches it — it must not jump his queue or muddy the pin question. D1/D2 are external and read-only; they may be built any time.

Character truth: Hermes' subagents nest under Hermes' root exactly as Muse children nest under parents — the unified model does not care which house minted the hero.

## 6. Adapters A + B — Claude Code and Codex (from the research, brief)

- **A (Claude Code): hooks-first** (Pixel Agents' road, not Agent Quest's log-scraping): SessionStart/PreToolUse/PostToolUse/SubagentStart/SubagentStop/PermissionRequest/Stop written into `~/.claude/settings.json`, POSTing to the village ingest; JSONL tailing as fallback; StatusLine JSON for honest context % and cost. Per-tool subagent granularity is real (hooks carry agent identity inside subagents).
- **B (Codex): dual** — rollout tailing (Agent Quest's road, works today for sessions + token counts) **plus Codex's own hooks** (present since v0.114; no tracker uses them yet — our B is the first, and it is what gives
...[truncated 4474 chars]