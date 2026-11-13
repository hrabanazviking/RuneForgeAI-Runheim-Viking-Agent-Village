# Agent Tracking Applications — Landscape Research

*Researched 2026-11-13. “Verified live” = read in the research browser on the project’s own GitHub/site that day. “Index” = read via web-index extracts of the primary README/docs or a reputable secondary. Star counts move daily; treat them as order-of-magnitude signals dated 2026-11-13.*

## Summary

- **Agent Quest (Fulvio Scichilone) is real, MIT-licensed, actively maintained, and deeper than its cute surface.** Verified live on GitHub: **185 stars, 34 forks, 139 commits, latest release v0.4.0 (29 Oct 2026)** — note his marketing site still says 0.3.1, it lags the repo. It is a Bun + Hono server (port 4444) + React 19 / Phaser 4 client (port 4445) that polls Claude Code `~/.claude*/projects/**/*.jsonl` and Codex `~/.codex/sessions/**/rollout-*.jsonl` every 2–3 s, pushes `AgentState` over native Bun WebSocket, and renders each session as a hero walking between 8 fantasy buildings by activity. Its differentiators vs. the field are **context-window gauges, skills/plugins telemetry, and subagent-family delegation accounting** (context processed by subagents vs. returned to parent), plus a tile-map editor, day/night + rain/lightning, and CC0 art (Tiny Swords) that makes forking/re-skinning clean.
- **The category leader by adoption is not Agent Quest, it is Pixel Agents** (now `pixel-agents-hq/pixel-agents`, by Pablo De Lucca): verified live **10.2k stars, 1.7k forks, v1.6.0 released ~3 days before research, ~96k VS Code installs**. Same idea, office instead of village, Canvas 2D, and — importantly — it has moved to **hooks-first detection with JSONL as fallback**, where Agent Quest is still primarily log-polling. It spawned 15+ forks/variants.
- **The broadest provider coverage is whatsmyagentdoing** (Charonyuu, MIT, 401 stars, created 23 Oct 2026): 12+ CLIs (Claude, Codex, Gemini, Copilot, OpenCode, Hermes, Grok, Kimi, Pi, etc.) from their local session stores, with **both a zero-dependency 2D canvas office and an optional Three.js 3D office with downloadable scene packs** — the strongest current proof that 2D+3D in one product works.
- **True 3D-first trackers of *existing* Claude Code/Codex sessions are rare.** Most “3D agent offices” found (The Delegation, AGENTVILLE, BagIdea Office in HD-2D/Godot) are *orchestrators or simulators that run their own agents*, not passive monitors of your terminals. The credible 3D for this use case today is an alternate render mode of a 2D product (whatsmyagentdoing, tfavareto/ai-office).
- **Data plumbing converges on three taps:** (1) **transcript/rollout JSONL tailing** — zero-install, read-only, works for Claude + Codex + most CLIs, 1–3 s latency, brittle to format changes; (2) **lifecycle hooks** — lowest latency and the only reliable subagent/permission signals, but requires editing `settings.json`/`hooks.json` and trust approval; (3) **vendor supervisors / app-servers** (`claude agents --json`, Codex app-server) and **OpenTelemetry** for fleet/cost analytics, not character state. Claude Code now documents **33 hook events** including `SubagentStart/SubagentStop`; **Codex has also shipped a Claude-style hooks system (since v0.114, now on by default)** — Agent Quest’s README line “Codex doesn’t expose hooks” is outdated as a general statement, though its Codex provider is still log-based.
- **Forking Agent Quest is a sound base** — MIT code + CC0 art, clean provider/parser/state-manager separation — but a new entrant should borrow: hooks-first ingestion + permission/waiting states (Pixel Agents / ccdeck), multi-provider adapters + 3D scene packs (whatsmyagentdoing), two-way control (ai-office approves/steers from the UI; most trackers are read-only), replay + anomaly detection (Agent Mission Control / ai-office day replay), and attention-first triage (ccdeck / Claude Agent View: “needs you” sorted by wait time).

---

## Findings

### 1. Agent Quest — Deep Dive (Priority 1)

**Identity.** “Open-source fantasy village for monitoring Claude Code and Codex across terminals, apps and IDEs. Live activity, context gauges, skills, plugins and subagent families.” Maker: Fulvio Scichilone (FulAppiOS). Site: https://www.fulvioscichilone.it/agent-quest/ · Repo: https://github.com/FulAppiOS/Agent-Quest · Product Hunt launch was scheduled 27 Oct 2026: https://www.producthunt.com/products/agent-quest?embed=true&utm_source=badge-featured&utm_medium=badge&utm_campaign=badge-agent-quest

> ⚠️ **Name collision:** `Hin-Nattapat/agent-quest` on GitHub / VS Code Marketplace is a *different* project also called “Agent Quest” (a single-hero RPG progression panel, §2). Do not confuse the two when searching or installing.

#### 1.1 How agents map to buildings / activities

From the maker’s site (index, read 2026-11-13, https://www.fulvioscichilone.it/agent-quest/) and the repo’s `AgentState` type (CLAUDE.md, index: https://github.com/FulAppiOS/Agent-Quest/blob/main/CLAUDE.md):

| Activity (internal) | Trigger | Building |
|---|---|---|
| `reading` | `Read / Grep / Glob` | **The Library** |
| `editing` | `Edit / Write` | **The Forge** |
| `bash` | `Bash` (shell, tests) | **The Arena** |
| `debugging` | errors / debugging | **The Alchemist** |
| `thinking` | reasoning / planning | **The Castle** |
| `reviewing` | `Agent` tool / review / delegating | **The Watchtower** |
| `git` | commit / push / merge | **The Chapel** |
| `idle` | waiting for input | **The Tavern** |

Heroes walk between buildings; clicking a hero opens a Detail Panel; a Party Bar, Activity Feed, Minimap and Top Bar are React overlays on the Phaser scene (see §1.3).

#### 1.2 How it receives data — no instrumentation of your workflow by default

Verified from README (live, https://github.com/FulAppiOS/Agent-Quest) and CLAUDE.md (index, URL above):

- **Primary path: log polling.** Two providers run in parallel. `ClaudeProvider` auto-discovers *every* `~/.claude*` directory containing `projects/` (e.g. `~/.claude`, `~/.claude-work`, `~/.claude-personale`); `CodexProvider` watches `~/.codex/sessions/`. On macOS both also discover Xcode’s stores (`~/Library/Developer/Xcode/CodingAssistant/ClaudeAgentConfig/projects` and `.../codex/sessions`, added in v0.4.0). Providers **poll every 2–3 s**, parse with per-format `SessionParser`s into `AgentState` objects, and push over **native Bun WebSocket**.
- **Data flow (verbatim architecture):** `~/.claude*/projects/**/*.jsonl` and `~/.codex/sessions/**/rollout-*.jsonl` → ClaudeProvider / CodexProvider → SessionParser → AgentStateManager → WebSocket → Browser (React state + Phaser scene).
- **Optional low-latency path: Claude Code hooks only.** An optional Hono endpoint receives Claude Code `postToolUse` hooks; v0.1.0 added an opt-in one-click Claude `Stop` hook for instant turn-end detection (merged into `settings.json`, removable). The README states Codex has no equivalent hook path *in Agent Quest’s design*; its `SessionRegistry` pidfile liveness oracle is likewise Claude-only, and Codex liveness is inferred from rollout-file activity. (General Codex hooks now exist — see §6.2 — Agent Quest simply doesn’t use them.)
- **Codex format handling:** both the legacy per-kind rollout events (`user_message`, `exec_command_end`, `patch_apply_end`, …) and the Codex ≥ 0.160 `item_completed` format (`item.type = UserMessage | CommandExecution | FileChange | WebSearch | McpToolCall | SubAgentActivity | ContextCompaction | …`). Codex subagents are rollouts whose `session_meta` has `thread_source: 'subagent'` with `parent_thread_id`, `agent_nickname`, `agent_path`.
- **Telemetry side-channel:** providers also emit `kind: 'telemetry'` events (tool results, skill bodies, token counts, compaction markers) that update accounting only — never status/activity/lifecycle.
- **Ports:** server HTTP + WebSocket `localhost:4444` (`ws://localhost:4444/ws`), client Vite `localhost:4445`. Dashboard URL is http://localhost:4445. Configurable via `server/.env` / `client/.env` (`PORT`, `CLIENT_PORT`, `VITE_SERVER_URL`, `VITE_WS_URL`, `CLIENT_URL`). Default bind is localhost only; `AGENT_QUEST_LAN=1` enables LAN viewing (README warns it exposes tool calls, file paths and command output to the LAN).

#### 1.3 Tech stack and rendering

- **Server:** Bun (≥ 1.1 required) + Hono, TypeScript strict. **Client:** React 19 + **Phaser 4 “Caladan”**. Full-screen Phaser canvas renders the village; React overlay panels (Party Bar, Activity Feed, Detail Panel, Minimap, Top Bar) sit on top via a ref-based bridge (`useRef` + `useEffect` + EventEmitter). Phaser ↔ React selection is synchronised (`hero:clicked` / `selection:changed`, per v0.0.2 notes).
- **2D pixel art, not WebGL-dom / not 3D.** Phaser renders via its WebGL/Canvas pipeline; v0.2.0 made rendering Retina-crisp (canvas at physical pixels, backing store capped at 3×). Pan & zoom with touch support. No Three.js/Babylon/Godot in this project.
- **Art:** bundled **Tiny Swords by Pixel Frog, CC0 1.0**, under `client/public/assets/themes/tiny-swords-cc0/` (hero spritesheets, buildings, terrain, decorations). `bun run check:assets` verifies the pack; the app blocks at boot with a missing-assets screen if files are deleted. CC0 is the key forkability fact: the shipped art can be replaced/redistributed without the non-commercial restrictions that encumber Star-Office-UI or Pixel Agents’ original tileset (see §2).

#### 1.4 Characters and subagents

From README + v0.3.0/v0.2.0 release notes (live, https://github.com/FulAppiOS/Agent-Quest/releases):

- **Hero class is by role, not rotation:** main sessions are always **knights/warriors**; subagents are **pawns — smaller, in the parent’s colour**; ultra-mode / Workflow-orchestrated agents are **archers**. Subagents discovered before their parent adopt the parent’s `heroColor` later.
- **Badges/labels:** name + per-hero colour (server-side deduplication so same-named agents don’t collide), `CLAUDE` / `CODEX` source badges, model badges (`OPUS`, `SONNET`, `HAIKU`, and from v0.2.0 `FABLE` for `claude-fable-5` with auto-derived badges/colours for unknown families like Mythos), Xcode badges (v0.4.0), a **`SUBAGENT · <role>` badge** on subagents and a **`⚑ N` badge** on parents counting their subagents, and a **✦ skill tag** while a skill’s instructions apply.
- **Party Bar:** subagents **nest under their parent**; shows active/idle sorted by status priority, status dot, avatars up to 66 px; a collapsible “Completed” section keeps finished sessions reviewable (v0.1.0). Parent Detail Panel has a **Delegation** section: context processed by its subagents vs. what they returned vs. how much stayed out of the parent context.
- **Ultra-mode:** v0.2.0 discovered Workflow subagents under `subagents/workflows/wf_<runId>/` that previously never appeared on the map, with names from transcript-adjacent metadata (`Explore: <task>` style) instead of truncated prompts.

#### 1.5 Context gauges, skills & plugins telemetry (the 0.3.x headline)

README “Context, skills & subagents” table + v0.3.0 notes (live releases URL above; site summary at https://www.fulvioscichilone.it/agent-quest/):

- **Context window:** a bar above each hero (**green → amber → orange → red, pulsing from 90 %**), a Top Bar `Ctx` stat for the hottest main conversation with a dropdown comparing every live agent, and a Detail Panel Context section (tokens in use / window, peak, compaction count). Optional “context nearly full” alert at **85 %**, re-arming below 70 %; bars/alert can be toggled in Settings; subagents never fire notifications. Sources: Claude `message.usage` on assistant lines; Codex `token_count` events (which also report window size).
- **The Claude window-size problem, handled explicitly:** Claude Code never writes window size to its logs, so Agent Quest resolves it as: env override `AGENT_QUEST_CLAUDE_CONTEXT_WINDOW` → `[1m]` suffix in the configured model in `settings.json` → infer 1 M once usage exceeds 200 k → default 200 k. Codex reports `model_context_window` itself.
- **Skills & plugins:** Detail Panel lists every loaded skill (load count, ~tokens injected) and every plugin/MCP server (calls, ~result tokens). Sources: Claude `attributionSkill`, the `Skill` tool and the injected skill body, MCP tool names/result sizes; Codex `SKILL.md` reads and `McpToolCall` items. A separate inventory (`server/src/skills/skill-inventory.ts`) measures installed `SKILL.md` files (user skills + latest cached plugin versions, `~/.codex/skills`).
- **Fleet view & export:** the ✦ Top Bar button opens Skills & Plugins fleet view (fleet-wide skill/plugin tables, every agent’s context) with **Export for AI review in Markdown or JSON**, per-fleet and per-hero — explicitly meant to be pasted into another AI session for review.
- **Estimation caveat (project’s own):** figures marked `~` are estimates (`Math.ceil(chars / 4)`); “context kept outside the main conversation is not a measurement of money saved.” Session Report cards (v0.1.0) show token *counts*, deliberately no dollar cost “to avoid per-model price drift.”

#### 1.6 Tile map editor, day/night, weather

- **Map editor:** built-in tile map editor for customising village layout (README feature list, live). v0.0.1 notes (live releases): **5 slots, terrain / decorations / paths / NPCs, load the shipped template or start from scratch, switch the active map on the fly.** `agentquest update` preserves local map edits. The editor still renders at CSS resolution (v0.2.0 notes say Retina treatment is future work). Site shows the editor at https://www.fulvioscichilone.it/agent-quest/ (“The built-in map editor”).
- **Day/night & weather:** v0.0.1 dashboard included **day/night cycle, rain + lightning**, minimap, pan & zoom. Site caption: “The village after dark, with weather effects” (https://www.fulvioscichilone.it/agent-quest/). *Could not verify from primary text whether the cycle follows the real local clock or an in-app cycle, nor whether weather is tied to real-world weather vs. ambient/random — the sources say “effects” only. Flagged in Could Not Verify.*

#### 1.7 Install / run

README (live, https://github.com/FulAppiOS/Agent-Quest):

```bash
# Manual (macOS, Linux, Windows via WSL2)
git clone https://github.com/FulAppiOS/Agent-Quest.git
cd Agent-Quest
bun install
bun start        # browser opens http://localhost:4445
```

- macOS-only one-line installer: `curl -fsSL https://raw.githubusercontent.com/FulAppiOS/Agent-Quest/main/install.sh | bash` — installs Bun if missing, clones to `~/agent-quest`, creates a global `agentquest` command (`agentquest` = start, `agentquest update` = `git pull --ff-only && bun install` preserving map edits). Optional Node 20+/npm command forms exist.
- Requirements: Bun ≥ 1.1 and a Claude Code or Codex install *with session logs*; without sessions the dashboard opens but “the village stays empty.”
- Release notifications (v0.3.1+): dashboard checks GitHub for the latest stable release on open, banner with “Remind me tomorrow” (24 h, persisted), never auto-installs.
- Platform matrix (README, live): Claude Code ✓ macOS / ✓ Linux / ✓ Windows (WSL2 recommended); Codex ✓ macOS, **not yet verified** on Linux/Windows; Xcode agents macOS only. Native Windows “should also work” after a cross-platform shell fix but is not exercised.

#### 1.8 License, maintenance, roadmap, limitations

- **License: MIT** © Fulvio Scichilone (README/LICENSE, live). Art is separately CC0 (see §1.3) — so a fork inherits *both* code and shipped art cleanly, which is rarer than it sounds in this category.
- **Maintenance (verified live 2026-11-13):** 185 stars, 34 forks, 2 watchers, 139 commits, 3 open PRs, 8 tags. Release cadence: v0.0.1 22 Apr 2026 → v0.0.2 23 Apr → v0.0.3 24 Apr (Codex) → v0.1.0 3 Jun → v0.2.0 12 Jun → v0.3.0 15 Oct → v0.3.1 16 Oct → **v0.4.0 29 Oct 2026 (latest, Xcode sessions)**. Latest commit on `main` (“feat: show ASCII-art banner on startup”) was “last week” relative to 13 Nov. This is an actively maintained solo project, not an abandoned demo. The author’s own site still advertising 0.3.1 is a minor docs-lag signal.
- **Roadmap:** *No explicit public roadmap document was found* in README, site, or releases. Direction is inferable only from shipped releases (Xcode support, ultra-mode, context/skills depth). Do not attribute a stated roadmap beyond that.
- **Stated limitations:** Codex unverified off macOS; status can lag the 2–3 s poll (v0.0.2 notes historically cited ~10 s state-derivation lag, since improved with hook path); Claude context window is inferred, not read; token figures marked `~` are estimates; LAN mode exposes session details; Xcode logs older than 3 h are skipped on startup and Xcode may not create folders until the first prompt.

### 2. Gamified 2D Alternatives

**Pixel Agents — the category originator and leader.** By Pablo De Lucca, now `pixel-agents-hq/pixel-agents`. Verified live 2026-11-13 (https://github.com/pixel-agents-hq/pixel-agents): **10.2k stars, 1.7k forks, 235 commits, v1.6.0 latest (3 days prior), MIT, 24 contributors.** Two forms from one codebase: VS Code extension (Marketplace + Open VSX; ~96,331 installs per https://www.pluginstats.com/plugins/vscode/pablodelucca.pixel-agents, index) and standalone CLI (`npx pixel-agents`, Node 20+, browser office, embedded terminal, phone access via Tailscale). Each Claude Code terminal = one character (typing when writing, reading when searching, speech bubble + optional chime when it needs input/permission or finishes). **Data: hooks mode (default)** — a hook script receives `SessionStart`, `PreToolUse`, `PermissionRequest`, `Stop`, discovers running Pixel Agents servers and sends authenticated events; **heuristic mode (fallback)** infers status from JSONL under `~/.claude/projects/`; transcripts are also read in hooks mode for details. Usage/context bars are opt-in and work by *wrapping your `statusLine` command* (yours keeps running; turning off restores it). Features Agent Quest lacks: office layout editor with named Areas + JSON import/export, **character wardrobe/pixel editor** (bodies, hair, clothes, accessories, recolouring; roster export as PNG/config/zip), pets, procedural 8-bit “office radio,” recent-conversation resume. Rendering: React 19 + **Canvas 2D** webview, BFS pathfinding, character FSM (idle → walk → type/read) — per README extracts at https://www.mdskills.ai/skills/pixel-agents?tab=source (index). **Providers: Claude Code only in the shipping product; Codex, Gemini, Cursor “on the roadmap”** (live README). Known limits (project’s own): agent↔terminal sync can desync on rapid open/close/restore; JSONL gives no clear waiting/finished signal (hence hooks mode). Art caveat: the office tileset in the Marketplace build is Donarg’s $2 itch.io tileset, *not* in the repo — themeability is high via external asset directories, but bundled-art licensing is messier than Agent Quest’s CC0. History/coverage: Fast Company, http://www.fastcompany.com/91497413/this-charming-pixel-art-game-solves-one-of-ai-codings-most-annoying-ux-problems (index). A curated list notes it “spawned 15+ forks within months”: https://github.com/shahtab123/awesome-living-interfaces/blob/HEAD/README.md (index) — variants include pixel-agents-gemini, pixel-agents-codex, pixel-agents-cursor (VSIX fork), opencode-pixel-office (OpenCode + Claude, mobile via QR), and a JetBrains “Pixel Office” port with 80+ furniture items and alien/cat/zoo themes.

**whatsmyagentdoing — broadest coverage, 2D + 3D (see also §3).** By Charonyuu. Index/live-index (https://github.com/Charonyuu/whatsmyagentdoing): **401 stars, 67 forks, 90 commits, 11 releases, MIT, created 23 Oct 2026.** Installed as a skill (`/myoffice`) into `~/.claude/skills/myoffice` or `~/.codex/skills/myoffice`, or run directly (`node bin/cli.js`); Node 18+, no `npm install`. **Data: read-only log tailing, no hooks, no config, no API keys** (one optional `--install-hook` adds a Claude hook so permission prompts show “Waiting for you”); pushes over **Server-Sent Events**, default port **4545**, bound to 127.0.0.1. Providers via local session stores: Claude Code, Codex, Gemini CLI, Antigravity CLI, Copilot CLI, OMP (oh-my-pi), Pi, Kimi Code, DeepSeek Harness, Grok Build, OpenCode and Hermes (last two via read-only SQLite, needing Node 22.13+). Isometric office: working agents sit at cubicles (thought bubble = thinking, typing + scrolling code = tool running, standing/waving = needs you); recently finished agents wander off-duty (pantry, couch, arcade, nap). Status table, context-full % per card (amber 60 %, red 85 %), “No progress” flags (tool > 10 min, thinking silent > 8 min), Today panel (turns/tokens by agent/project), `--private`, `--json` snapshot, `--demo`. **2D = hand-drawn zero-dependency canvas; 3D = Three.js (§3).** Themeability is a headline feature: downloadable scene packs (§3). Limits (project’s own): logs are written per finished block, not per token; Claude Code doesn’t log permission prompts; log formats are not public APIs and may degrade.

**AI Office (tfavareto/ai-office) — 2D default, 3D optional, and *two-way*.** Index (https://github.com/tfavareto/ai-office): **MIT, created 19 Oct 2026, 84 commits, only 2 stars — very new, treat adoption as nil.** Claude Code (CLI + desktop) + Codex, macOS + Linux, Node 22+, server at http://127.0.0.1:4848. Mapping: project = room (own colour/desks), session = person at a desk, **subagent = colleague who joins the meeting room and leaves when done**; statuses Working / Needs you / Waiting for shell (cyan light + timer) / Idle / Done, always with text as well as colour. Reads disk logs for state, *plus* a Claude Code plugin whose hooks talk to the local server — and unlike almost every other entry here it **acts back**: approve/deny tools from the office, steer agents with messages, hire agents at empty desks, inbox triage (“Approve all similar”), task board, approval rules, usage/forecasts, and a **day replay at up to 1800×** (“Watch the day”). That control surface is its real contribution to the field.

**Agent Mission Control (glglak).** Index (https://github.com/glglak/agent-mission-control): 3 stars, 17 commits, created 17 Mar 2026; *no license was stated in the fetched repo details — do not assume MIT.* Next.js dashboard (:3700) + Fastify telemetry bridge (:4700) + SQLite + WebSocket. **Data: pure hooks** — HTTP hooks in `~/.claude/settings.json` POST `SessionStart/SessionEnd/PreToolUse/PostToolUse/PostToolUseFailure/SubagentStart/SubagentStop/Stop/Notification/TaskCompleted` to `/api/collect/claude-code`. Pixel office with zones (Dev Area, QA Lab, Planning, Review, Coffee Shop), speech bubbles, **sub-agent connection lines, communication beams, session replay, anomaly detection** (stuck loop = same tool+file 5+ times, failure rate > 30 %, silent agents), scrum panel, “fired agents float to Agent Heaven.” Designed for Claude Code *Teams*. Small project, but its analytics/replay ideas are ahead of the leaders.

**Claude Office Visualizer (paulrobello/claude-office).** Via EveryDev/DeepWiki (index): https://www.everydev.ai/tools/claude-office-visualizer and https://deepwiki.com/paulrobello/claude-office/3-system-architecture — ~406 stars / 81 forks as of May 2026, **MIT**, Next.js + **PixiJS (WebGL 2D)** + Zustand/XState frontend (:3000), FastAPI + SQLite backend (:8000), **hooks layer** capturing session/tool/subagent/compaction events → state machine → WebSocket. Boss + employee agents, multi-mode whiteboard, conversation history, and v0.15 **multi-floor buildings** (each floor its own office/agents); also ships an `opencode-plugin/` mapping OpenCode lifecycle events to the same API. Last push May 2026 per EveryDev — maintenance slowing.

**Star-Office-UI (ringhyacinth) — huge, but a different animal.** Index (https://github.com/ringhyacinth/Star-Office-UI): **7,573 stars** in the GitHub snapshot (7.6k elsewhere), Flask + **Phaser**, http://127.0.0.1:19000, CN/EN/JP, desktop-pet mode, AI-generated backgrounds. Crucially its data is **self-reported OpenClaw state**: the agent itself calls `set_state.py` per rules in `SOUL.md` to flip among 6 states (idle/writing/researching/executing/syncing/error) — it does *not* observe Claude Code/Codex sessions. **Licence split: code MIT, art assets non-commercial learning only** (LimeZu) — a fork trap. FollowAgents reports no commits since 11 Mar 2026: https://followagents.com/en/agents/star-office-ui (index).

**Agentopolis (CodeBlackwell) — agents + codebase-as-city.** Via PyPI (index): https://pypi.org/project/agentopolis/0.7.0/ (later 0.25.0 listing also indexed). Python 3.11+, FastAPI/uvicorn, **port 4242**, plain-canvas Habbo-style isometric, original pixel art. Two views on one page: the **dispatch floor** (main session checks in at reception, subagents walk in when spawned, tool calls send workers to stations — CRT terminal for `Bash`, archive shelf for `Read/Grep`, workshop for `Edit/Write`, phone booth for web tools) and the **skyline**, where the codebase itself is a city seeded from git history (footprint = LOC, floors = cross-component centrality, window glow = recency; edits add scaffolding, a commit drops scaffolds and adds floors). **Data: Claude Code hooks via `curl -m 1 … || true` on 8 events** (`SessionStart, UserPromptSubmit, Notification, Pre/PostToolUse, Stop, SubagentStop, SessionEnd`) → `POST /hook` → normalise → **SSE** → canvas; hooks attach transiently by default (removed on exit) or persistently via `agentopolis attach` (backup saved). Plus `agentopolis movie` replaying any repo’s git history as a growing-city timelapse. *License not verified from fetched text — flagged.*

**“Agent Quest” Companion (Hin-Nattapat) — the name collision.** VS Code Marketplace (index): https://marketplace.visualstudio.com/items?itemName=NattaP.agent-quest ; design doc: https://github.com/hin-nattapat/agent-quest/blob/HEAD/docs/PRODUCT.md . A **single shared hero** (not one character per session) in a VS Code bottom panel: level, class, XP, streak, deeds (“boss fights, loot, level-ups”), current zone — ambient, glanceable, explicitly “not an app to operate.” Engine (Bun + `jq`) installed via Homebrew (`brew install agent-quest`, `aq setup`) wires hooks for **Claude Code, Codex and Cursor** and writes `~/.agentrpg/state.json`, which the read-only panel renders. MIT per the listing. Interesting as a *progression* layer, not a fleet view.

**Also noted (brief, index-level):** **Claw Empire** (GreenSheep01201, Apache-2.0 per secondary sources) — local-first pixel-office *orchestrator/company simulator* for Claude/Codex/Gemini/OpenCode/Kimi/Copilot/Antigravity with Kanban and worktrees; it runs agents rather than passively watching external sessions. **BagIdea Office** (https://bagidea.github.io/bagidea-office/pitch/, index) — **Godot 4 HD-2D** office living behind your desktop icons (Node zero-dep daemon, Rust shell); every character is a real headless Claude Code session the system itself dispatches via a Director agent (`DELEGATE:` lines), with Codex as a delegate — an orchestrator with a game skin, and the only Godot entry found. **Pixel Agents forks for JetBrains/Cursor/Gemini/OpenCode** — see Pixel Agents above.

### 3. Gamified 3D Alternatives

The honest headline: **there is no mature, 3D-first, passive tracker of real Claude Code/Codex sessions.** What exists:

| Project | 3D tech | What it actually is |
|---|---|---|
| **whatsmyagentdoing — High mode** | **Three.js** (~180 KB gz), Kenney CC0 *Mini Characters* + *Furniture Kit* (meshopt, ~1.3 MB, lazy-loaded only in High), real-time lighting/shadows, drag-rotate/scroll-zoom | Same live log-fed office as its 2D mode. **Downloadable scene packs** (Christmas Cabin, Neon Arcade, Haunted Hollow, Pirate Cove, Café, Space Station, Castle; ~100–290 KB each), data-only, verified against a pinned SHA-256 before install into `~/.whatsmyagentdoing/scenes`. This is the model to copy for themeable 3D. Source: https://github.com/Charonyuu/whatsmyagentdoing (index) |
| **AI Office — 3D mode** | Renderer not named in fetched README beyond “lit 3D tower with real-time shadows, glass, city traffic and 3D characters” — *do not assume Three.js* | One-click switch in Settings → General → Visual; same building/people/UI, only rendering changes. 2D is hand-made pixel art. Source: https://github.com/tfavareto/ai-office (index) |
| **AGENTVILLE** (hackathon, Devpost) | **React Three Fiber** voxel client, Fastify + WebSocket, zod protocol, Claude Agent SDK (Haiku triage / Sonnet coding), TimescaleDB | You place buildings, agents run out and *build a real web app*; characters are driven by real tool calls (walk into a building to read, scaffolding rises while editing, magnifier for tests). A builder-world, not a monitor of your existing sessions. https://devpost.com/software/agentville-su8epi (index) |
| **The Delegation** | Three.js **WebGPU**, React + Zustand, Blender assets; Gemini 3 Flash / Claude Sonnet 4.6 per social posts | 3D office where a *simulated* team (Creative Agency / Music Studio / Game Dev presets) claims desks, delegates and pings when blocked, from a typed brief. Found only via social/video posts (e.g. https://www.facebook.com/reel/1811789856164888/, index); **no primary repo was verified, and nothing found shows it reading real Claude Code/Codex session logs.** Classify as demo/simulator, not a tracker. |
| **Agent Quest / Pixel Agents 3D experiments** | — | A Pixel Agents fork commit adds a “3D voxel spike (Three.js, isometric + first/third person)” (https://github.com/davrux/pixel-agents/commit/1f13bd381df6cc458c7b8b386e56a7227679d6ab, index) — evidence of demand, not a shipped product. Agent Quest itself is 2D only. |

Adjacent, not coding-agent trackers: **AI Town** (generative-agent village, cited by Pixel Agents’ maker as inspiration) and **harishkotra/agent-office** (Phaser + Colyseus, autonomous local-LLM agents) simulate agents rather than observe real CLI sessions — excluded from the comparison table for that reason.

### 4. Plain Observability / Non-Game Agent Monitors

*Included only where genuinely used to watch live subagent/task activity in Claude Code/Codex sessions, per the mission.*

**Built-in: Claude Code Agent View + `claude agents --json`.** Anthropic, Research Preview shipped 11 May 2026 in v2.1.139 (https://claude.com/resources/articles/agent-view-in-claude-code, index): `claude agents` opens a roster of **background** sessions (working / needs input / done), `/bg` or `claude --bg` backgrounds a session, each in its own worktree, run by a supervisor process (`~/.claude/daemon/roster.json`, `~/.claude/jobs/<id>/`, `~/.claude/daemon.log`). Limits that created the third-party market: interactive sessions in other terminals don’t appear until backgrounded, and **subagents/teammates are not separate rows** (analysis: https://dev.to/bargan/what-claude-codes-agent-view-lists-what-it-leaves-out-and-how-to-see-the-rest-1d5m, index). `claude agents --json` *does* include interactive sessions and is scriptable. Codex has an analogous `codex agents` dashboard (mentioned in the same comparisons; not deep-verified here).

**ccdeck (Bargan Constantin) — the strongest plain dashboard.** AGPL-3.0, `npx ccdeck`, http://127.0.0.1:4317 (guides: https://dev.to/bargan/one-dashboard-for-every-claude-code-and-codex-session-installing-and-configuring-ccdeck-3md2 and ccdeck.dev, index; described at v3.49–3.82 in Oct–Nov 2026 sources). One tree/canvas of every Claude + Codex session: sessions waiting on you rise to the top sorted by wait time, every subagent and tool call live, cost/quota alongside. **Plumbing is the lesson: Claude via 10–12 report-only hook entries** in `~/.claude/settings.json` (a `hook.js` that POSTs to the deck, always exits 0, writes nothing back, so it *cannot* approve/deny); **Codex via rollout tailing, nothing installed.** Its author is explicit that Codex rollouts record no approval-wait, so Codex “waiting” cannot be shown honestly. Caveat: ccdeck sends its own usage reports to `api.ccdeck.dev` from first start (opt out `AGENTS_DECK_NO_REPORTS=1`) — read its “What it touches” before adopting.

**disler / IndyDevDan — claude-code-hooks-multi-agent-observability (the reference architecture).** Hooks (Python) → HTTP POST → Bun server → **SQLite (WAL)** → WebSocket → Vue 3 + Tailwind client; captures all hook event types, session colour-coding, chat/transcript viewer, live pulse. ~1,289 stars per awesome-list indexing: https://awesome.ecosyste.ms/projects/github.com%2Fdisler%2Fclaude-code-hooks-multi-agent-observability (index). Most later hook dashboards copy this shape.

**Agents Observe (simple10).** Installed as a Claude Code plugin: hooks across the full lifecycle (tool, subagent start/stop, task, permission) → `observe_cli.mjs` → Dockerised SQLite API server → React dashboard at http://localhost:4981, with filtering/search, **parent/subagent hierarchy, full session replay, per-model token stats**. Index: https://awesome.ecosyste.ms/projects/github.com%2Fsimple10%2Fagents-observe . Last push 1 Apr 2026 in that snapshot — check activity before relying on it.

**AgentPeek (TranHuuHoang).** Index (https://github.com/TranHuuHoang/agentpeek): “Real-time observability for Claude Code agent teams.” Hooks auto-installed into `~/.claude/settings.json` append JSON via `jq` to `/tmp/agentpeek.jsonl`; server tails the file; dashboard at http://localhost:8099 shows a **left-to-right directed spawn graph** (name, type, est. cost, duration, errors; stuck agents get amber borders) and a **Gantt timeline**. Created 17 Mar 2026. Licence/activity beyond that not verified.

**Agent Watch for Claude Code (VS Code).** Marketplace (index): https://marketplace.visualstudio.com/items?itemName=joseluoliva.subagent-monitor — sidebar cards per subagent (type, task, status, 500 ms elapsed timer), session history, *zero server*: hooks on the Task tool append to `~/.claude/agent-events.jsonl` (capped ~1 MB, rotated) and the extension tails it. The minimal viable subagent tracker.

**Usage-only monitors (not task trackers — for completeness):** ccusage (parses JSONL for daily/session cost; de-facto standard), Claude-Code-Usage-Monitor (live terminal burn-rate), ccstatusline / claude-hud (statusLine JSON → context %, cost, rate limits). Comparison: https://caprock.dev/guides/claude-code-usage-monitors/ (index). They answer “how much have I spent?” not “what is each subagent doing?”

**Production LLM tracing (different job):** Langfuse (MIT core, self-hostable, OTel ingestion, trace trees + live evals), LangSmith (proprietary SaaS, LangChain/LangGraph-native trace trees), Arize Phoenix (Elastic License 2.0, OpenTelemetry/OpenInference, notebook/local-first). All three trace *applications you instrument* (SDK/OTel spans); **none watches your local Claude Code/Codex CLI sessions or their subagents out of the box.** Include them only if the reader later needs fleet/API-level analytics — via Claude Code’s OTel export (§6.4), not as mission control. Comparison background: https://www.respan.ai/articles/ai-observability-tools (index).

**Orchestrators with monitoring (they run the agents):** Orca (Stably AI, MIT, desktop + mobile, worktree-per-agent for 30+ CLIs; secondary sources report ~90k+ stars — *not live-verified here, treat with caution*), Conductor (proprietary, macOS, Claude/Codex/Cursor/OpenCode; **joining Meta — cloud shuts down 20 Nov 2026, local app winds down early 2027**), Caprock (Apache-2.0, runs + records terminal-started sessions), Traycer / T3 Code (MIT, parallel workspaces), claude-squad and Gas Town (tmux-based). Roundup: https://caprock.dev/guides/coding-agent-desktop-apps/ and https://traycer.ai/compare/best-parallel-coding-agent-tools (index). Their state comes from sessions *they* supervise (often terminal-output pattern matching), so they don’t drop onto an existing fleet of terminals the way log/hook trackers do.

### 5. Comparison Table

#### Gamified 2D

| Tool | Maker / licence | Watches | Data tap | Rendering | Subagents | Themeable? | Activity (2026-11-13) |
|---|---|---|---|---|---|---|---|
| **Agent Quest** | Fulvio Scichilone / **MIT** (+ CC0 art) | Claude Code, Codex (+ Xcode) | JSONL/rollout poll 2–3 s + optional Claude `postToolUse`/`Stop` hooks; Bun WS :4444→:4445 | Phaser 4, React 19, 2D pixel village | Pawns in parent colour, nested, delegation accounting; archers for workflows | **High:** tile editor (5 slots), CC0 sprite pack replaceable | 185★, v0.4.0 29 Oct 2026, active |
| **Pixel Agents** | Pablo De Lucca / **MIT** (tileset excl.) | Claude Code only (others roadmap) | **Hooks-first** + JSONL fallback; statusLine wrap for usage | Canvas 2D, React 19 webview; VS Code + standalone | Separate characters linked to parent (per project docs) | **High:** layout editor, Areas, wardrobe/pixel editor, external assets | 10.2k★, v1.6.0, very active |
| **whatsmyagentdoing** (2D mode) | Charonyuu / **MIT** | **12+ CLIs** | Read-only log/SQLite tailing, SSE :4545; optional permission hook | Zero-dep hand-drawn canvas, isometric office | Provider-badged persons, subagent filter | Medium in 2D; **high via 3D scene packs** | 401★, 11 releases, created Oct 2026, active |
| **AI Office** | tfavareto / **MIT** | Claude Code, Codex | Disk logs + plugin hooks; **two-way** (approve/steer/hire) | Pixel office :4848 (renderer n/s) | Meeting-room colleagues | Rooms/decor/levels, i18n EN/PT/ES | 2★, Oct 2026, brand new |
| **Agent Mission Control** | glglak / *licence n/v* | Claude Code (Teams) | HTTP hooks → Fastify/SQLite/WS :4700→:3700 | Next.js pixel canvas | Connection lines | Zones fixed; scrum/analytics focus | 3★, Mar 2026, small |
| **Claude Office** | Paul Robello / **MIT** | Claude Code, OpenCode | Hooks → FastAPI/SQLite/WS :8000→:3000 | Next.js + **PixiJS WebGL** | Boss/employees, multi-floor | Multi-floor layouts, whiteboard modes | ~406★ (May), slowing |
| **Star-Office-UI** | Ring Hyacinth et al. / code **MIT**, **art non-commercial** | OpenClaw (self-reported), *not* Claude/Codex logs | Agent calls `set_state.py`; Flask :19000 | Phaser | Multi-agent guests | AI-generated backgrounds; art licence blocks commercial forks | 7.6k★, last commit Mar 2026 |
| **Agentopolis** | CodeBlackwell / *licence n/v* | Claude Code | Hooks (`curl`) → POST /hook → SSE :4242 | Plain canvas, isometric | Walk-in workers at stations | Codebase-city zoning manifest (`.agentopolis.json`) | PyPI to 0.25.0, Jun 2026 |
| **Agent Quest Companion** (Hin) | Hin-Nattapat / **MIT** | Claude, Codex, Cursor → *one hero* | Hooks (Bun+`jq`) → `~/.agentrpg/state.json` | VS Code panel, pixel RPG | No fleet; progression only | Retro RPG skin, single character | Marketplace listing, 2026 |

#### Gamified 3D

| Tool | Maker / licence | Watches | Data tap | Rendering | Notes |
|---|---|---|---|---|---|
| **whatsmyagentdoing — High** | Charonyuu / MIT (Kenney CC0 models) | Same 12+ CLIs | Same log tailing | **Three.js**, lighting/shadows | Scene packs (7) SHA-256-verified, data-only — best theme model |
| **AI Office — 3D** | tfavareto / MIT | Claude, Codex | Same logs+hooks | 3D tower, engine n/s | One-click 2D↔3D, same state |
| **AGENTVILLE** | Hackathon team / n/v | Its *own* Claude Agent SDK agents | SDK events, WS | React Three Fiber voxel | Builds real apps; not a passive tracker |
| **The Delegation** | Alvaro Cintas et al. / n/v | Its *own* simulated team | Internal (Gemini/Claude APIs) | Three.js WebGPU | Demo/simulator; repo & log-tap **not verified** |

#### Plain observability

| Tool | Licence | Watches | Data tap | View | Subagent-level? |
|---|---|---|---|---|---|
| **Claude Agent View** (built-in) | Proprietary (in Claude Code) | Claude background sessions | Supervisor (`roster.json`, `jobs/`) + `claude agents --json` | Terminal roster | **No** — sessions only |
| **ccdeck** | AGPL-3.0 | Claude + Codex | Claude hooks (report-only POST) + Codex rollout tail | Web tree :4317, waiting-first | **Yes** (Claude); Codex waits not recorded |
| **disler observability** | OSS (check repo) | Claude | Hooks → Bun/SQLite WAL/WS → Vue | Web dashboard, transcript viewer | Yes |
| **Agents Observe** | OSS (check repo) | Claude (+Codex claim) | Plugin hooks → SQLite → React :4981 | Filter/search, **replay** | Yes, hierarchy |
| **AgentPeek** | n/v | Claude teams | Hooks → `jq` → `/tmp/agentpeek.jsonl` tail :8099 | Spawn **graph + Gantt** | Yes |
| **Agent Watch** (VS Code) | n/v | Claude subagents | Task hooks → `~/.claude/agent-events.jsonl` tail | Sidebar cards | Yes, subagents only |
| **Langfuse / LangSmith / Phoenix** | MIT core / proprietary / Elastic 2.0 | Instrumented LLM *apps* | SDK / OTel / OpenInference | Trace trees, evals | Only if app instruments them; **not CLI sessions** |

*n/v = not verified from a primary source in this pass. n/s = not stated in fetched source.*

### 6. Data Plumbing — How Tools Tap Claude Code, Codex & Others (Priority 3)

#### 6.1 Claude Code hooks — the richest tap

Official reference (index, crawled 2026-11-13): https://code.claude.com/docs/en/hooks — hooks are user-defined **shell commands, HTTP endpoints, MCP tool calls, LLM prompts, or subagents** fired at lifecycle points, in terminal, IDE, Desktop and cloud sessions alike. Input is JSON (stdin for command hooks, POST body for HTTP hooks); handlers can return decisions (e.g. `PreToolUse` can block). Configured in `~/.claude/settings.json` (global), `.claude/settings.json` (project), managed policy, plugin `hooks/hooks.json`, and skill/subagent frontmatter. Secondary counts put the current total at **33 events** (https://agentmgr.app/guides/claude-code-hooks-reference.html, index, checked against official docs Sep 2026).

The events that matter for tracking:

| Cadence | Events | Tracker value |
|---|---|---|
| Per session | `SessionStart`, `SessionEnd` (+ `Setup`) | Hero spawns / leaves; `SessionStart` matcher = startup/resume/clear/compact/fork |
| Per turn | `UserPromptSubmit`, `UserPromptExpansion`, `Stop`, `StopFailure` | Working → waiting/done; `Stop` is the reliable turn-end (Agent Quest’s opt-in hook; Pixel Agents/ccdeck use it) |
| Per tool call | `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `PostToolBatch` | Current activity + file/command; matcher = tool name (`Bash`, `Edit|Write`, `mcp__.*`) |
| Permission | `PermissionRequest`, `PermissionDenied`, `Notification` (types incl. `permission_prompt`, `idle_prompt`, `agent_needs_input`, `agent_completed`) | The *only* trustworthy “needs you” signal — transcripts don’t record prompts |
| **Subagents** | **`SubagentStart`, `SubagentStop`** (matcher = agent type: `general-purpose`, `Explore`, `Plan`, custom/plugin names); `TeammateIdle` for agent teams | Spawn/despawn with type; `SubagentStop` input carries `agent_id`, `agent_type`, `agent_transcript_path`, `last_assistant_message` |
| Tasks | `TaskCreated`, `TaskCompleted` | Board/checklist state |
| Context | `PreCompact`, `PostCompact`, `InstructionsLoaded` | Compaction counts; which CLAUDE.md/rules loaded |
| Other | `ConfigChange`, `CwdChanged`, `FileChanged`, `WorktreeCreate/Remove`, `Pre/PostModelSwitch`, `Elicitation/ElicitationResult` (MCP user input), `MessageDisplay` | Worktree/model/MCP colour |

**Subagent granularity note (official):** hooks defined in settings/plugins also run *inside* subagents; when a subagent calls a tool, `PreToolUse`/`PostToolUse` fire the same hooks with **`agent_id` and `agent_type`** in the input — so a hook collector can attribute every tool call to its subagent, not just bracket its lifetime. This is the gold standard: disler, Agents Observe, Agent Mission Control, ccdeck and Pixel Agents (hooks mode) are all built on it, typically as hook → HTTP POST → local server → SQLite/file → WebSocket → UI.

#### 6.2 Codex — rollouts first, hooks now real

- **Rollout files (what most trackers use):** `$CODEX_HOME/sessions/YYYY/MM/DD/rollout-<timestamp>-<thread-uuid>.jsonl` (default `~/.codex/sessions/…`; archived sessions move to `archived_sessions/`), plus a SQLite index (`~/.codex/state_5.sqlite`, `threads` table). Each line is `{timestamp, type, payload}`. Two formats: legacy per-kind events and, from Codex ≥ 0.160, `item_completed` with typed items. `session_meta` identifies subagent threads (`thread_source: 'subagent'`, `parent_thread_id`, …) and `token_count` events carry usage + `model_context_window`. Sources: Agent Quest CLAUDE.md/README (live/index) and https://medium.com/@chinchillemd/what-codex-cli-writes-to-disk-for-every-session-and-how-to-follow-it-live-432cd5989561 (index, checked against codex-cli 0.162.1). **Gaps (documented by ccdeck’s author): rollouts record no approval-wait and no session-close event** — a waiting Codex agent looks identical to a running one in the file.
- **Hooks (newer — do not repeat the old “Codex has no hooks” claim):** OpenAI’s Codex hooks (docs: https://developers.openai.com/codex/hooks → https://learn.chatgpt.com/docs/hooks) first shipped in **v0.114 (Mar 2026)** behind a flag and are **now enabled by default**, configured in `~/.codex/hooks.json` or `[hooks]` in `config.toml`, command handlers only, JSON on stdin, with a **trust-review step** (new/changed hooks don’t run until trusted via `/hooks`). Event set mirrors Claude’s: `SessionStart`, `UserPromptSubmit`, `PreToolUse` (Bash, apply_patch, MCP), `PostToolUse`, `PermissionRequest`, `SubagentStart/SubagentStop`, `PreCompact/PostCompact`, `Stop`, `SessionEnd`. Caveats from implementer notes: Codex `PreToolUse` coverage historically centred on shell (apply_patch fixed later), there is no `PostToolUseFailure`, async hooks are not supported, and subagent hooks fire with the *parent’s* `session_id` plus the subagent’s own `transcript_path`/`agent_id`. Sources: https://symposium.dev/design/agent-details/codex-cli.html and https://github.com/spacesheep-dev/sessionpipe/blob/HEAD/spec/ADAPTERS.md (index). Agent Quest, whatsmyagentdoing and ccdeck all still read Codex via rollouts — hooks are the untapped upgrade path.
- **Legacy `notify`:** `notify = ["…"]` in `config.toml` spawns a program on **`agent-turn-complete`** only, with a JSON argument — a turn-end sentinel, nothing more (https://github.com/r3b1s/tmux-pilot/blob/HEAD/docs/harness-specific-notes/openai-codex.md, index). `tui.notifications` can additionally filter `approval-requested`.
- **App-server:** `codex app-server` (stdio JSON-RPC, thread/turn/item events) is how some orchestrators (e.g. Realmkeeper notes, index) drive Codex with live status including waiting states that rollouts lack — heavier to integrate than file tailing, and it suits tools that *launch* Codex rather than observe external terminals. Codex also ships its own `codex agents` session dashboard and `codex exec --json` event streaming for headless runs.

#### 6.3 Transcript / log files — the zero-install tap

- **Claude:** `~/.claude/projects/<project-slug>/<session-id>.jsonl` (full transcript: messages, tool calls/results, `message.usage`), subagent transcripts under the session’s `subagents/` folder (plus Workflow runs under `subagents/workflows/wf_<runId>/`), and `~/.claude/sessions/*.json` pid/session entries used by some tools for liveness. Multi-install users have several `~/.claude*` roots — Agent Quest and whatsmyagentdoing both handle `CLAUDE_CONFIG_DIR` / multiple roots. Transcripts are plaintext, local, and (per community research) auto-deleted after `cleanupPeriodDays` (default 30). This is the tap for Agent Quest, whatsmyagentdoing, Pixel Agents (fallback), ccusage, and most usage tools. **Granularity: excellent retrospectively (subagent files exist), weaker live** — no permission-prompt records, turn-end must be inferred, and files flush per block, not per token.
- **Others (from whatsmyagentdoing’s README, index — the best single adapter catalogue found):** Gemini CLI `~/.gemini/tmp/<project>/chats/session-*.jsonl`; Copilot CLI `~/.copilot/session-state/<session>/events.jsonl`; Kimi `~/.kimi-code/sessions/…/wire.jsonl`; Grok `~/.grok/sessions/…/updates.jsonl`; Pi `~/.pi/agent/sessions/…/*.jsonl`; **OpenCode and Hermes keep sessions in a single SQLite DB** (`~/.local/share/opencode/opencode.db`; Hermes `state.db`) read read-only via `node:sqlite`. Antigravity’s desktop store is undocumented/binary — unsupported by whatsmyagentdoing; its CLI writes `transcript.jsonl` and *is* supported.
- **Pattern:** every serious multi-provider tracker is a set of per-CLI *adapters* normalising to one internal `AgentState`/event model (Agent Quest’s `SessionParser` per format; sessionpipe’s adapter spec; whatsmyagentdoing’s per-store tailers). Budget for format drift: Codex has already changed rollout shape once, Claude’s JSONL is not a public API.

#### 6.4 StatusLine, Agent View JSON, OpenTelemetry, MCP, WebSockets

- **StatusLine (Claude, main-session only):** a `statusLine` command in `settings.json` receives a JSON object on stdin on every update — `model`, `cwd`/`workspace`, `cost.total_*`, **`context_window.used_percentage / remaining_percentage / context_window_size`**, `exceeds_200k_tokens`, and (Pro/Max) `rate_limits.five_hour / seven_day` with reset times. Runs locally, zero tokens. Docs: https://code.claude.com/docs/en/statusline (via extracts, index). Pixel Agents *wraps* the statusLine to get plan-limit bars (the only place Claude reports them); cc-cockpit chains it similarly. **No subagent breakdown** — it describes the foreground session only.
- **Supervisor JSON:** `claude agents --json` prints active sessions (interactive + background) as a JSON array — the cleanest official “list my sessions” for scripts/status bars, but with Agent View’s blind spot for subagent rows. Background-session state also lives in `~/.claude/daemon/roster.json` + `~/.claude/jobs/<id>/`.
- **OpenTelemetry (Claude, fleet analytics):** enable with `CLAUDE_CODE_ENABLE_TELEMETRY=1` + `OTEL_METRICS_EXPORTER` / `OTEL_LOGS_EXPORTER` (`otlp`/`prometheus`/`console`) to an OTLP endpoint (gRPC 4317 / HTTP 4318); metrics every 60 s, logs every 5 s by default. Metrics: `claude_code.session.count`, `claude_code.cost.usage`, `claude_code.token.usage` (input/output/cacheRead/cacheCreation), lines/commits/PRs, `claude_code.code_edit_tool.decision` — tagged by `model`, **`query_source` (`main` | `subagent` | `auxiliary`)**, `agent.name`, `skill.name`, `plugin.name`, `mcp_server.name`. Events via logs exporter: `api_request` (cost/duration/tokens), `api_error`, `tool_result`, `tool_decision`, `user_prompt`. Beta tracing (`CLAUDE_CODE_ENHANCED_TELEMETRY_BETA=1`) emits per-prompt traces. Content stays out unless `OTEL_LOG_USER_PROMPTS=1` / `OTEL_LOG_TOOL_DETAILS=1` / `OTEL_LOG_TOOL_CONTENT=1`. Docs via https://dev.to/dash0/monitoring-claude-code-usage-and-costs-with-opentelemetry-1nga and https://code.claude.com/docs/en/monitoring-usage (index). **Granularity: subagent-attributed aggregates, not live per-subagent state** — right for Grafana/Langfuse, wrong for characters. No equivalent documented OTel export was found for Codex CLI in this pass.
- **MCP:** not a telemetry tap per se. MCP appears in the data as *content* — `mcp__<server>__<tool>` tool names in transcripts/hooks/OTel, `Elicitation` hooks when an MCP server asks the user for input — and as a *hook handler type* (a hook can call an MCP tool). No tracker found uses an MCP server as its ingestion channel for Claude/Codex session activity. (Several orchestrators expose MCP so *other* agents can delegate to them — the reverse direction.)
- **Local servers / transport (the trackers’ own plumbing):** the recurring shape is hook-or-tailer → **local HTTP collector** (Hono/Fastify/FastAPI/Bun, ports: Agent Quest 4444, AMC 4700, Claude Office 8000, ccdeck 4317) → store (SQLite WAL for concurrent sessions is the repeated lesson; or in-memory/file tail) → **WebSocket** (Agent Quest, Pixel Agents, disler, AMC, Claude Office) or **SSE** (whatsmyagentdoing, Agentopolis) → browser. Everything binds 127.0.0.1 by default; LAN/phone access is opt-in (Agent Quest `AGENT_QUEST_LAN=1`, Pixel Agents via Tailscale, whatsmyagentdoing via Document Picture-in-Picture pop-out).

#### 6.5 Granularity summary

| Mechanism | Subagent-level? | Waiting/permission? | Latency | Install cost |
|---|---|---|---|---|
| Claude hooks (with `agent_id`, SubagentStart/Stop) | **Yes — per tool call** | **Yes** (`PermissionRequest`/`Notification`) | Real-time | Edit settings + trust; restart sessions |
| Claude transcripts JSONL | Yes (separate files) | No (inferred only) | ~1–3 s poll/flush | None, read-only |
| Codex hooks | Yes (`agent_id`, own transcript_path) | Yes (`PermissionRequest`) | Real-time | hooks.json + trust review |
| Codex rollouts | Yes (subagent threads) | **No** | ~1–3 s | None, read-only |
| StatusLine JSON | No (foreground session) | No | Per render | statusLine command |
| `claude agents --json` / supervisor | No (sessions only) | Yes (needs-input state) | On demand | None (built-in) |
| OpenTelemetry | Aggregates by subagent (`query_source`, `agent.name`) | No | 5–60 s export | Collector required |
| Codex `notify` | No | Turn-complete only | Real-time | config.toml line |

### 7. Synthesis — Patterns, Gaps, and What to Borrow When Forking Agent Quest

**Recurring design patterns**

1. **Spatial metaphor = status at a glance.** Every gamified winner maps *activity → place* (buildings, zones, desks/stations) and *state → posture/bubble/badge* (typing, thought bubble, waving, “?”/⚑/✦). The map is a pre-attentive dashboard: you notice the crowded Arena or the hero waving at the Tavern before you read any text.
2. **One character per session, smaller/linked characters per subagent.** Agent Quest’s pawn-in-parent-colour, Pixel Agents’ linked spawns, AMC’s connection lines, ai-office’s meeting-room visitors — all solve the same “which of these 14 things is a child of which?” problem spatially.
3. **Read-only first, local-only, 127.0.0.1.** The trusted tools install nothing (log tailing) or install a *provably inert* hook (ccdeck’s exit-0 hook is the exemplar). The two exceptions that act back (ai-office approvals, BagIdea/Claw Empire orchestration) are explicit about it.
4. **A plain view hiding behind the game.** Party bars, list views, trees, Gantt charts and detail panels do the real triage; the world does the noticing. whatsmyagentdoing (office/list/JSON), ccdeck (tree) and Agent Quest (party bar + feed + detail) all ship both.
5. **Adapter normalisation.** Per-CLI parsers → one `AgentState` → one renderer. Provider breadth is now a competitive axis (1 → 2 → 12 CLIs in a year).
6. **Attention economics.** “Needs you, sorted by wait time,” no-progress flags, finish/wait notifications (desktop + sound + toast) recur everywhere — the scarce resource being managed is the human, not tokens.

**What nobody does well yet (gaps)**

- **Codex waiting/approval state.** Rollouts don’t record it; Codex hooks now expose `PermissionRequest`, but *no major tracker found uses Codex hooks yet*. First mover wins this.
- **Cross-provider subagent trees in one world.** Claude subagents, Codex subagent threads, OpenCode/Hermes children, and agent-team teammates each have different identity models; nobody renders a unified, nested, multi-provider family tree with delegation accounting as well as Agent Quest does for its two providers — and even Agent Quest stops at two providers (+ Xcode).
- **3D that earns its keep.** Existing 3D modes are the 2D office with depth. Nobody uses 3D for what it’s good at here: *scale* (floors/buildings for dozens of agents — Claude Office’s multi-floor idea, in 2D), or *history* (Agentopolis’s codebase-city is the lone attempt to spatialise the repo itself).
- **History, replay and post-mortems.** Only AMC (session replay + anomaly detection), ai-office (day replay) and Agents Observe (replay) treat yesterday’s fleet as first-class. Most villages vanish when the session ends.
- **Cost honesty.** Estimates (chars÷4, list-price math) are everywhere; plan-window truth lives only in StatusLine JSON / vendor quotas. Nobody combines live character state + official rate-limit windows + per-subagent cost attribution in one view. OTel has the attribution (`agent.name`, `skill.name`) but no live UI consumes it for this.
- **Scale beyond ~10 agents.** Speech bubbles and wandering heroes saturate fast; there is no good “100 subagents” view (aggregation, minimap heat, collapsing families) in any tool found. The reader’s “large crews” use case is precisely the unserved one.
- **Mobile/ambient surfaces.** Only Pixel Agents (Tailscale phone office), whatsmyagentdoing (PiP pop-out) and Agent Quest (LAN mode) seriously address the second-screen/phone glance; nobody ships a native watch/menubar ambient mode for fleet state (usage tools do menubar for *quotas* only).

**What a new entrant forking Agent Quest (MIT) should borrow**

From **Agent Quest itself, keep:** the provider/parser/`AgentStateManager` separation; the CC0 art base (swap sprites, keep the licence clean); context gauges + delegation accounting (its unique depth); the tile editor; fleet Skills & Plugins export-for-AI-review.

Borrow:

1. **Hooks-first ingestion** (Pixel Agents, ccdeck): add Claude hooks as the primary path (JSONL as fallback/auto-heal), and be **first to use Codex hooks** for `PermissionRequest`/`SubagentStart` — that fixes both latency and the Codex waiting blind spot. Copy ccdeck’s inert-hook discipline (exit 0, no stdout, no decisions) to stay trusted as a monitor.
2. **Provider adapters + SQLite stores** (whatsmyagentdoing): Gemini/Copilot/OpenCode/Hermes/Grok adapters behind the existing parser interface; its per-store path catalogue is the checklist.
3. **3D as scene packs, not a rewrite** (whatsmyagentdoing): keep Phaser 2D as default; add a Three.js view fed by the *same* `AgentState` stream, with data-only, hash-verified theme packs (Castle already matches Agent Quest’s fantasy skin).
4. **Attention triage UI** (ccdeck, Agent View): a “Needs you” queue sorted by wait time above the village, plus no-progress flags (whatsmyagentdoing’s 10 min tool / 8 min thinking rules).
5. **Replay + anomalies** (AMC, ai-office): persist the event stream (SQLite WAL, per disler) and add day/session replay and stuck-loop detection — cheap once events are stored, and it turns a toy into a debugging tool.
6. **Character customisation** (Pixel Agents): wardrobe/roster editor and external asset directories — the community/longevity feature; Agent Quest’s editor customises the *map*, Pixel Agents’ customises the *people*, users want both.
7. **Optional two-way control, clearly gated** (ai-office): approve/deny and steer-from-the-village as an opt-in plugin, off by default, never conflated with the read-only monitor.
8. **Scale affordances (new work):** family collapsing (one pawn-cluster per parent past N agents), a heat minimap, and a list/tree toggle that mirrors the world 1:1 — the gap the fork is best placed to own for large multi-agent crews.

---

## Could not verify

- **Agent Quest day/night & weather mechanics:** sources confirm a day/night cycle and rain + lightning exist, but not whether they follow the real local clock / real weather vs. an ambient cycle. No explicit public **roadmap** document was found for Agent Quest.
- **Agent Quest issues detail:** the repo header (3 open PRs) was verified live; individual issue contents were not read in this pass.
- **The Delegation:** no primary GitHub repo was verified; all detail comes from social/video posts. It is classified as a simulator/demo, not a session tracker, on that basis.
- **Licences not verified from primary text:** Agent Mission Control, Agentopolis, AgentPeek, Agents Observe, disler dashboard — check each repo’s LICENSE before any reuse.
- **Orca star count (~90k+)** comes from secondary comparisons, not a live repo read; treat as reported, not verified. Conductor wind-down dates come from its changelog via Caprock’s guide (index).
- **Codex OTel:** no documented Codex CLI OpenTelemetry export was found; absence is reported as “not found in this pass,” not as proof none exists.
- **AI Office 3D renderer** is not named in its fetched README; do not cite it as Three.js.
- Pixel Agents’ current README (live) describes hooks + heuristic modes; some older secondary claims (e.g. “JSONL only,” “Windows-only testing”) are outdated and were not used.

## Sources

**Agent Quest (primary)**
- Maker site — https://www.fulvioscichilone.it/agent-quest/ (index, read 2026-11-13; still lists 0.3.1)
- Repo / README — https://github.com/FulAppiOS/Agent-Quest (verified live 2026-11-13: 185★, 34 forks, 139 commits, MIT)
- Releases v0.0.1–v0.4.0 — https://github.com/FulAppiOS/Agent-Quest/releases (verified live 2026-11-13; v0.4.0, 29 Oct 2026, latest)
- Architecture (CLAUDE.md) — https://github.com/FulAppiOS/Agent-Quest/blob/main/CLAUDE.md (index)
- Product Hunt — https://www.producthunt.com/products/agent-quest?embed=true&utm_source=badge-featured&utm_medium=badge&utm_campaign=badge-agent-quest (index)

**Gamified alternatives (primary first)**
- Pixel Agents — https://github.com/pixel-agents-hq/pixel-agents (verified live 2026-11-13: 10.2k★, v1.6.0, MIT); installs https://www.pluginstats.com/plugins/vscode/pablodelucca.pixel-agents (index); README extract https://www.mdskills.ai/skills/pixel-agents?tab=source (index); Fast Company http://www.fastcompany.com/91497413/this-charming-pixel-art-game-solves-one-of-ai-codings-most-annoying-ux-problems (index); fork census https://github.com/shahtab123/awesome-living-interfaces/blob/HEAD/README.md (index)
- whatsmyagentdoing — https://github.com/Charonyuu/whatsmyagentdoing (index: 401★, MIT, port 4545, 2D canvas + Three.js, scene packs)
- AI Office — https://github.com/tfavareto/ai-office (index: MIT, :4848, 2D + optional 3D, two-way plugin)
- Agent Mission Control — https://github.com/glglak/agent-mission-control (index: hooks → Fastify :4700 → Next.js :3700)
- Claude Office — https://www.everydev.ai/tools/claude-office-visualizer ; https://deepwiki.com/paulrobello/claude-office/3-system-architecture (index)
- Star-Office-UI — https://github.com/ringhyacinth/Star-Office-UI (index: 7,573★, Flask + Phaser, code MIT / art non-commercial); activity https://followagents.com/en/agents/star-office-ui (index)
- Agentopolis — https://pypi.org/project/agentopolis/0.7.0/ (index: FastAPI :4242, hooks → SSE, codebase city)
- Agent Quest Companion (Hin-Nattapat, different project) — https://marketplace.visualstudio.com/items?itemName=NattaP.agent-quest ; https://github.com/hin-nattapat/agent-quest/blob/HEAD/docs/PRODUCT.md (index)
- BagIdea Office — https://bagidea.github.io/bagidea-office/pitch/ (index: Godot 4 HD-2D orchestrator)
- AGENTVILLE — https://devpost.com/software/agentville-su8epi (index: React Three Fiber, Claude Agent SDK)
- The Delegation (social only) — https://www.facebook.com/reel/1811789856164888/ (index)

**Plain observability**
- Claude Agent View (official) — https://claude.com/resources/articles/agent-view-in-claude-code (index, 11 May 2026); limits analysis https://dev.to/bargan/what-claude-codes-agent-view-lists-what-it-leaves-out-and-how-to-see-the-rest-1d5m (index)
- ccdeck — https://dev.to/bargan/one-dashboard-for-every-claude-code-and-codex-session-installing-and-configuring-ccdeck-3md2 (index, AGPL-3.0, :4317, hooks + rollout tailing)
- disler observability — https://awesome.ecosyste.ms/projects/github.com%2Fdisler%2Fclaude-code-hooks-multi-agent-observability (index)
- Agents Observe — https://awesome.ecosyste.ms/projects/github.com%2Fsimple10%2Fagents-observe (index, :4981, replay)
- AgentPeek — https://github.com/TranHuuHoang/agentpeek (index, :8099, graph + Gantt)
- Agent Watch (VS Code) — https://marketplace.visualstudio.com/items?itemName=joseluoliva.subagent-monitor (index)
- Usage monitors compared — https://caprock.dev/guides/claude-code-usage-monitors/ (index)
- Orchestrator roundups — https://caprock.dev/guides/coding-agent-desktop-apps/ ; https://traycer.ai/compare/best-parallel-coding-agent-tools (index)
- Production tracing comparison — https://www.respan.ai/articles/ai-observability-tools (index: Langfuse MIT, LangSmith SaaS, Phoenix Elastic 2.0)

**Data plumbing (primary first)**
- Claude Code hooks reference (official) — https://code.claude.com/docs/en/hooks (index, crawled 2026-11-13: full event table, SubagentStart/Stop, agent_id/agent_type)
- Claude Code hooks census — https://agentmgr.app/guides/claude-code-hooks-reference.html (index: 33 events, checked vs official docs)
- Claude Code monitoring / OTel — https://dev.to/dash0/monitoring-claude-code-usage-and-costs-with-opentelemetry-1nga ; https://dev.to/nixonperinchery/claude-code-opentelemetry-track-cost-and-tool-use-c83 (index, from official monitoring docs)
- Codex hooks (official docs, via implementer extracts) — https://developers.openai.com/codex/hooks (cited in https://github.com/wu-hongjun/codex-delegation-plugin/blob/HEAD/references/documentation-codex/hooks.md, index); implementation notes https://symposium.dev/design/agent-details/codex-cli.html ; https://github.com/spacesheep-dev/sessionpipe/blob/HEAD/spec/ADAPTERS.md (index)
- Codex rollouts / notify — https://medium.com/@chinchillemd/what-codex-cli-writes-to-disk-for-every-session-and-how-to-follow-it-live-432cd5989561 ; https://github.com/r3b1s/tmux-pilot/blob/HEAD/docs/harness-specific-notes/openai-codex.md (index)
