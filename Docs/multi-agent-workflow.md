# Multi-Agent Workflow

## Roles and ownership

| Role | Responsibilities | Primary owned paths |
| --- | --- | --- |
| Lead / Architect | Overall architecture, integration, compatibility API, architectural conflicts, Dreamcast/Simulant viability, and architecture documentation. | `Docs/`; review of `Game/Compatibility/` |
| Gameplay | Engine-neutral gameplay systems: player, movement, interactions, inventory, enemies, puzzles, doors, events, game state, and save/load. | `Game/Core/`, `Game/Gameplay/`, `Game/Data/` |
| Unity | Unity development/runtime integration, Unity backend implementation when required, PC visualization/debugging, and editor tooling. | `Unity/` |
| Simulant / Dreamcast | Simulant and KallistiOS integration, Dreamcast runtime constraints, and platform capability validation. | `Simulant/` |

## Branches and integration

- `main` is the stable Lead/integration branch.
- Specialists work in isolated Codex worktrees on focused branches. Preferred names are `codex/gameplay-*`, `codex/unity-*`, and `codex/simulant-*`; retain and report an automatically generated name if Codex creates a different one.
- Specialists make focused, reviewable commits. The Lead reviews and integrates their work.
- `Game/Compatibility/` is shared architectural territory: Lead review is required before integration.

## Working agreement

- Inspect existing code and documentation before introducing abstractions. Add compatibility abstractions only for actual game requirements.
- Keep game code engine-neutral. Unity is the PC development/simulation environment, not the game's architectural foundation; Simulant/Dreamcast viability is a first-class constraint.
- Record significant architectural decisions under `Docs/`. Task-to-task messages are coordination only; durable decisions belong in the repository.
- The environment supports four concurrent agents total: Lead, Gameplay, Unity, and Simulant/Dreamcast. Do not add Art or QA roles until the first vertical slice requires them.
