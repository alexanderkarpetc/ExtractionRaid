# Testing and Feature Workflow

Use this file for test tasks, implementation flow, and change acceptance.

## 1) Testing strategy

### Unit-level tests (EditMode)
Prefer testing managers with:
- synthetic state
- fake adapters / ports
- deterministic RNG
- event buffers

Managers should be testable without scenes.

### Integration tests (PlayMode)
Use PlayMode tests only for Unity integration, such as:
- navmesh validation
- bullet collisions
- spawn / extraction triggers
- launch flow wiring

## 2) How to add a feature

When implementing a feature:
1. define the affected state (`RaidState`, `LevelState`, `PlayerProfileState`)
2. define required dependencies and expose them through context ports if needed
3. implement logic in a stateless manager function
4. emit domain events for VFX/SFX/UI where needed
5. update presenter/view to visualize results or route callbacks
6. add or update unit tests
7. add PlayMode coverage only where Unity integration matters

## 3) Claude Code working style in this repo

For implementation tasks:
- read `CLAUDE.md` first
- read only the relevant extra doc for the current task
- propose a file-level plan before editing
- keep changes small and localized
- avoid broad refactors unless explicitly requested
- avoid new frameworks unless explicitly requested
- maintain naming and folder conventions
- prefer ports + adapters over direct Unity API usage in logic

## 4) Definition of done

A change should:
- compile
- follow the architecture contract
- include test updates where applicable
- avoid hidden dependencies
- stay minimal and focused
- avoid unrelated formatting churn
- include a regression check when relevant
