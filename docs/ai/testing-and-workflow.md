# Testing and Feature Workflow

Use this file for test tasks, implementation flow, and change acceptance.

## 1) Testing strategy

### Unit-level tests (EditMode)
Prefer testing systems with:
- synthetic state
- fake adapters / ports
- deterministic RNG
- event buffers

Systems should be testable without scenes.

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
3. implement logic in a stateless system function
4. emit domain events for VFX/SFX/UI where needed
5. update presenter/view to visualize results or route callbacks
6. add or update unit tests
7. add PlayMode coverage only where Unity integration matters

