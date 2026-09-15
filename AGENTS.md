# Dreamcast Horror — Agent Instructions

## Project Direction

- The final target platform is the Sega Dreamcast.
- Simulant is the primary target runtime and the guaranteed backend this project is designed toward.
- Unity is the PC development and simulation environment.
- Spiral Game Engine is **not** a project dependency. If it becomes available, it may be evaluated later as an optional alternative backend.
- Keep the game engine-independent wherever reasonably practical.

## Architecture

- The game talks to a small engine compatibility API. Unity implements that API initially; Simulant will eventually implement the same API.
- Do not create the complete compatibility API in advance. Introduce abstractions only in response to demonstrated game requirements.
- Keep engine-neutral game code clearly separate from backend and integration code.
- Unity-specific functionality belongs behind the Unity compatibility/integration layer; it must not become fundamental game architecture.
- Do not let game logic directly depend on Unity APIs.
- Prefer engine-neutral data formats whenever practical.
- Treat Blender files and workflows as the canonical source for 3D assets.
- Do not make Unity-only systems fundamental to gameplay architecture.

## Portability and Dreamcast Constraints

- Account for Dreamcast constraints from the start: RAM, VRAM, polygon count, texture memory, lighting, animation, audio, loading, and related hardware limits.
- The PC/Unity build may use higher-quality rendering and assets where appropriate, but underlying game design and data must remain portable.
- Do not make Unity Terrain, HDRP-specific systems, Shader Graph, Timeline, NavMesh, Physics, Animator Controllers, Addressables, or other Unity-specific systems required by the underlying design unless we explicitly decide they can be replaced by the Simulant backend.
- Unity-specific code is permitted within the Unity backend/integration layer.
- Never assume a Unity feature has a Simulant equivalent. Verify important assumptions against Simulant before relying on them.

## Development Philosophy

- Build a small vertical slice before committing to large-scale architecture.
- Prefer simple, understandable systems over elaborate frameworks.
- Avoid premature abstraction and over-engineering.
- Validate important architectural assumptions against Simulant before building substantial content around them.
- Do not add dependencies or frameworks merely for convenience when they would substantially complicate the eventual Simulant/Dreamcast implementation.
- Document important architectural decisions when they are made.

## Agent Working Rules

- Read and follow this file before modifying the repository.
- Inspect existing code and documentation before creating architecture.
- Keep changes focused, minimal, and reviewable. Do not rewrite or restructure unrelated systems.
- Do not silently make major architectural decisions; explain them in the task response.
- If a requested feature could create a Unity dependency, flag it and propose a portable approach.
- If Simulant capabilities are uncertain, state the uncertainty; do not invent an API or claim parity.
- Multiple agents may work in this repository. Avoid unnecessarily modifying files owned by, or actively being changed in, another task.
- Treat the repository as the source of truth for project architecture and implementation.
