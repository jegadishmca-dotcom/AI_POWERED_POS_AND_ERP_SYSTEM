# Implementation Notes: Phase 2

## Dependency Injection (DI)
- Implemented a custom, reflection-free DI container to keep the engine lightweight. 
- Avoided `tsyringe` or `inversify` to ensure zero third-party lock-in for the core.
- Supports `createScope()` which will be crucial later for isolating variables per-scenario (e.g., scoping the `TestContext` to a single Playwright worker thread).

## Event Bus & Replay
- The `EventBus` stores an `eventHistory` array. 
- *Why?* Plugins might register slightly late, or an AI analysis plugin might need to review all events leading up to a crash. `replayEvents()` allows a late subscriber to catch up.

## Plugin Loader
- Built with a manifest-first approach (`IPluginManifest`).
- Explicitly enforces a `healthCheck()` during the boot sequence. If a PostgreSQL plugin can't connect to the DB, the entire engine halts at boot rather than failing 400 scenarios later.

## Telemetry
- Uses native Node.js `process.memoryUsage()` and `performance.now()`.
- Snapshots are taken `after_boot` and `after_teardown` to detect severe memory leaks across test suites.
