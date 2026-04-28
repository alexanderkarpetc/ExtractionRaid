# Unity Editor via MCP

This project uses **MCP for Unity** (`mcpforunityserver==9.6.8`) to let Claude Code drive the Unity Editor live. When the bridge is up, prefer these tools over reading `.unity`/`.prefab`/`.asset` files as text.

## 1) Preconditions

- Unity Editor for this project is **open**.
- The MCP for Unity bridge is listening on `127.0.0.1:6400`.
- Tools surface as `mcp__unityMCP__*` in the model's inventory.

**Health check before any non-trivial action:**

```bash
lsof -nP -iTCP:6400 -sTCP:LISTEN   # must show one Unity process
```

If the bridge is down, **stop and ask the user to start Unity / the bridge** — do not retry, do not fall back to scraping `.unity` files as text.

## 2) Tool catalog (42 tools)

### Read-only / inspection
| Tool | Purpose |
|---|---|
| `read_console` | Last N Unity console entries. First call before any debugging. |
| `find_gameobjects` | Search by name / tag / layer / component / path / id. Returns instance IDs only. |
| `unity_reflect` | Runtime reflection — types, methods, fields, generics. |
| `unity_docs` | Unity scripting reference lookup. |
| `find_in_file` | Regex/string search inside a single script file. |
| `validate_script` | Syntax-check a C# file before applying edits. |
| `get_sha` | Content hash of a script (use for optimistic locking on edits). |
| `debug_request_context` | Last MCP request payload — for debugging the bridge itself. |
| `manage_script_capabilities` | Discover available script edit operations. |
| `manage_tools` | Discover/inspect installed MCP tools. |

### Scene / GameObject CRUD
| Tool | Purpose |
|---|---|
| `manage_scene` | Open / save / new / list scenes. |
| `manage_gameobject` | Create, modify, delete, parent, transform GameObjects. |
| `manage_components` | Add / remove / get / set component fields. |
| `manage_prefabs` | Open / save / close prefab stage; create prefab from instance. |

### Assets & content
| Tool | Purpose |
|---|---|
| `manage_asset` | Import, move, delete, refresh project assets. |
| `manage_material`, `manage_shader`, `manage_texture` | Render-pipeline assets. |
| `manage_animation`, `manage_vfx`, `manage_ui` | Animator / VFX Graph / UGUI / UI Toolkit. |
| `manage_camera`, `manage_physics`, `manage_graphics` | Editor systems. |
| `manage_packages` | UPM operations. |
| `manage_scriptable_object` | Read / write SO assets (DevCheats lives here). |
| `manage_probuilder` | ProBuilder geometry. |
| `manage_profiler` | Profiler captures. |
| `manage_build` | Build pipeline. |

### Scripts (C# code edits)
| Tool | Purpose |
|---|---|
| `create_script`, `delete_script` | File-level script ops. |
| `manage_script` | High-level script operations. |
| `apply_text_edits`, `script_apply_edits` | Surgical edits with ranges or AST anchors. |

### Execution / control
| Tool | Purpose |
|---|---|
| `manage_editor` | Play / pause / stop, tags & layers, undo/redo, deploy/restore MCP package, telemetry status. |
| `execute_menu_item` | Click any Editor menu item by path (e.g. `Window/Dev Cheats`). |
| `execute_code` | Run arbitrary C# in the Editor — escape hatch for anything not covered above. |
| `batch_execute` | Sequence multiple MCP calls atomically. |
| `refresh_unity` | Force `AssetDatabase.Refresh()`. |
| `set_active_instance` | Pick which Unity Editor to target (multi-project). |

### Tests
| Tool | Purpose |
|---|---|
| `run_tests` | **Async.** Returns `job_id` immediately. |
| `get_test_job` | Poll `job_id` for status / results. |

## 3) Common patterns

### Health check → console → action
```
read_console (last 10, no stack) → spot recent errors → decide next step
```
Do this **before** opening scenes or running tests. Cheap, catches half the issues.

### Finding GameObjects (gotchas!)
- ❌ `by_path "/"` returns 0 — root-path query is **not supported**.
- ❌ `by_name "*"` returns 0 — **no wildcards**.
- ✅ `by_component "Transform"` with `include_inactive: true` → all GOs in scene.
- ✅ `by_component "Camera"` / `"Light"` / `"PlayerView"` → typed lookups.
- ✅ `by_name "ExactName"` is exact match (case-sensitive).
- ✅ `by_path "/Player/Hand/WeaponSocket"` works for known paths.

After `find_gameobjects`, fetch full data via the resource URI:
```
mcpforunity://scene/gameobject/{instanceId}
mcpforunity://scene/gameobject/{instanceId}/components
```

### Running EditMode tests
```
1. run_tests(mode="EditMode", assembly_names=["ExtractionRaid.EditMode.Tests"],
              category_names=["ArmorSystem"], include_failed_tests=true)
   → returns { job_id: "..." }
2. Poll get_test_job(job_id) every 3-5s until status == "Completed".
3. Read failures from result payload.
```

For targeted runs use `test_names` (full method paths) or `category_names` (test category attributes). Avoid full prog — slow and dilutes signal.

### Editing C# scripts
1. `validate_script` (or rely on hash) → confirm baseline.
2. `get_sha` → record before-hash.
3. `apply_text_edits` (preferred) or `script_apply_edits` for AST-anchored edits.
4. `validate_script` again → confirm syntax.
5. `read_console` after Unity recompiles → catch compile errors.

For pure file-level work (whole-file rewrite, new file), prefer the standard `Write`/`Edit` tools — MCP edit tools shine when you need editor-aware behavior (anchored edits, recompile signal).

### Inspecting DevCheats sections
DevCheats SOs live at `Assets/Resources/Configs/DevCheats/*.asset`.
Use `manage_scriptable_object` to read/write fields without opening Unity manually.

### Running arbitrary C# (last resort)
`execute_code` evaluates a snippet in the Editor process — useful when no dedicated tool fits. Treat it as a power tool: keep snippets minimal, never modify project assets without explicit user permission.

## 4) Read-only vs. modifying — safety stance

Modifying actions on this project's state require **explicit user confirmation** in the chat:
- `manage_editor`: `play`, `pause`, `stop`, `deploy_package`, `restore_package`, `add_tag`, `add_layer`, `undo`, `redo`
- Any `manage_*` action other than `get`/`list`
- `execute_code`, `execute_menu_item` (when the menu item mutates state)
- `apply_text_edits`, `script_apply_edits`, `create_script`, `delete_script`
- `run_tests` (this enters PlayMode for PlayMode tests — confirm scope)

Pure read-only tools (`read_console`, `find_gameobjects`, `unity_reflect`, `unity_docs`, resource URIs, `get_sha`, `validate_script`, `get_test_job`, `telemetry_status`) can run without ceremony.

## 5) When NOT to use MCP

- **No Unity instance running** → don't try, ask the user.
- **Scene file diff for a PR** → text diff of `.unity` is faster and version-control-aware.
- **Bulk grep across the whole repo** → use `Grep` / `rg`, not `find_in_file`.
- **CI / headless contexts** → MCP requires the Editor; tests in CI go through `Unity -batchmode` outside this flow.
- **Documentation lookups for non-Unity APIs** → `WebFetch` / `WebSearch`.

## 6) Subagent contract

When spawning a subagent (Task tool, custom agent) that should use Unity MCP, the parent **must** include in the prompt:

> "Unity Editor is open and MCP bridge is on 127.0.0.1:6400. Use `mcp__unityMCP__*` tools (see `docs/ai/unity-mcp.md`). Verify with `read_console` first; if the bridge is unreachable, abort and report — do not fall back to reading scene files as text."

Subagents inherit MCP tools but not this conversation's context. Spell out which tools they can call, and whether modifying actions are allowed.

## 7) Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `mcp__unityMCP__*` tools missing | Claude Desktop didn't reload `claude_desktop_config.json` | Cmd-Q Claude.app fully and relaunch (file-watch is not implemented). |
| Tool call hangs / times out | Bridge process down or wrong port | `lsof -nP -iTCP:6400 -sTCP:LISTEN`; restart MCP for Unity window inside the Editor. |
| `find_gameobjects` returns 0 for `/` or `*` | Wildcards / root path unsupported | Use `by_component "Transform"` with `include_inactive: true`. |
| Edits succeed but no recompile | Forgot `refresh_unity` | Call `refresh_unity`, then `read_console` to catch compile errors. |
| Multiple Editors open | Wrong instance targeted | `set_active_instance(name_or_hash)`. |

## 8) Versioning

- Server: `mcpforunityserver==9.6.8` (pinned in `~/Library/Application Support/Claude/claude_desktop_config.json`).
- Unity: 6000.3.10f1 (Unity 6.3, URP).
- Bridge package: installed inside the Unity project (managed via `manage_editor deploy_package` / `restore_package`).

When upgrading the server pin, run a sanity check (`read_console` + `find_gameobjects by_component Camera` + `manage_editor telemetry_status`) before doing real work.
