# Core Engine Architecture

## Overview
The UAT Core Engine serves as the central nervous system of the AI Quality Assurance Platform. It provides strict isolation between the "What" (Business Scenarios) and the "How" (Playwright, PostgreSQL, AI).

## Core Principles
1. **Zero External Dependencies**: The core engine knows nothing about web browsers, databases, or API clients. It deals purely in Typescript Interfaces.
2. **Event-Driven**: All communication between modules (e.g., Evidence collection triggered by a Rule Failure) happens via the `EventBus`.
3. **Dependency Injection**: The `DependencyContainer` manages object lifecycles (Singleton, Scoped, Transient), ensuring plugins and rules can request interfaces without coupling to implementations.

## Module Map
- `config/`: Layered configuration management (Defaults -> JSON -> Env).
- `context/`: `TestContext` passed to all plugins and scenarios, acting as a facade for the DI container.
- `di/`: Custom lightweight IoC container.
- `events/`: Pub/Sub EventBus with optional Event Replay support for late-binding plugins.
- `exceptions/`: Structured exception taxonomy.
- `lifecycle/`: Boot and teardown orchestration.
- `plugins/`: Dynamic plugin discovery and health checks.
- `telemetry/`: Real-time tracking of memory and timing.
- `interfaces/`: Defines the explicit boundaries (`IBrowser`, `IDatabaseSession`, `IHttpClient`) that the engine relies upon.

## Plugin Ecosystem (Phase 3A)
The engine strictly prohibits direct references to `playwright`, `pg`, or `axios`. These are wrapped inside plugins:
- **BrowserPlugin**: Manages `chromium.launch()`, Contexts, and Page lifecycles.
- **DatabasePlugin**: Encapsulates `pg.Pool`, pooling connections and restricting execution to read-only views or abstracted transactions.
- **ApiPlugin**: Wraps `axios`, appending auth tokens automatically and intercepting responses to fire `ApiCalled` events over the EventBus.
- **EvidencePlugin**: Collects artifacts (Screenshots, HAR files) and routes them into the `artifacts/` folder when a `ScenarioFailed` or `RuleFailed` event fires.

## Scenario Runtime Framework (Phase 3C)
The `src/runtime/` subsystem orchestrates the execution of business scenarios via abstract templates.
- **Scenario Metadata**: Strictly typed `IScenarioMetadata` defines Preconditions, Dependencies, Required Resources (`browser`, `database`), and Capabilities.
- **Scenario Registry**: Maintains the topological `DependencyGraph` of scenarios to prevent execution until dependencies resolve.
- **Scheduler**: Analyzes `ScenarioResource` locks (e.g. `Exclusive` vs `Parallel`) to prevent conflicting resource usage.
- **ScenarioResult**: Emits strongly typed artifacts and an `analysisContext` field reserved for future AI Root Cause Analysis.
- **Architecture Validation**: Tests enforce that no Playwright APIs, ERP SQL queries, or specific business scenarios are imported into the engine/runtime directories.

## Business Validation Engine (Phase 4)
The `src/rules/` subsystem is the deterministic validation core of the QA platform.
- **Pure Functions**: The engine holds strict guarantees against side-effects. It receives everything it needs via the `IEvaluationContext` (Snapshots, Artifacts, Knowledge References).
- **Rule Composition**: Utilizes the composite pattern (`AllRule`, `AnyRule`, `NotRule`, `XorRule`) to combine basic validators into complex policies dynamically.
- **Rule Pipeline**: Preprocessors and Postprocessors mutate or enrich context before and after the core rule evaluation.
- **ValidationResult**: Every rule must return a strictly typed `IValidationResult` along with a detailed `IRuleExplanation` (Inputs, Expected, Actual, Difference, Reason) and a `Confidence` score for AI triage.
- **Architecture Validation**: Tests block imports from `runtime`, `playwright`, `database (pg)`, and HTTP clients (`axios`), ensuring pure domain validation.

## ERP Rule Library (Phase 5)
The concrete implementations of the Business Validation Engine live in `src/rules/library/`.
- **Atomic Rules & Invariants**: Rules like `DebitEqualsCreditRule` and `InventoryNonNegativeRule` map directly to the Phase 1 Knowledge Base (`knowledgeRuleId`). They declare strict `preconditions` (requiring specific DB snapshots) and domain `owner` tags (e.g. `Finance`, `Inventory`).
- **Rule Packs**: Groupings of atomic rules tailored to specific business processes (e.g., `CashSalePack`). They inject all relevant invariants dynamically to prevent duplicated logic.
- **Automated Documentation**: A script parses the `RuleRegistry` and cross-references it with the `knowledge/` Markdown files to generate the `RuleCoverageReport.md`, `RuleCatalog.md`, and Mermaid `RuleDependencyGraph.md`.

## Business Workflow Library (Phase 5.5)
The `src/workflows/` subsystem acts as the declarative blueprint bridging Business Rules (Phase 5) to future UI Automation (Phase 6).
- **Abstract Declarations**: Workflows define *intent* without implementing any UI automation. They strictly define Inputs, Outputs, Success Criteria, Personas, and Preconditions.
- **Data Profiles & Personas**: Strong typings exist for test data dimensions (e.g. `Loyalty Customer`, `GST Product`) and actors (`StoreManager`, `Cashier`).
- **Variants & Failure Paths**: Every workflow explicitly declares acceptable variants (e.g. `Cash`, `UPI`, `Mixed`) and modeled failure paths (e.g. `Payment Declined`, `Negative Stock`), ensuring UAT isn't just "happy path" testing.
- **Workflow Registry & Artifacts**: A dedicated script generates a `WorkflowCatalog.md`, `WorkflowDependencyGraph.md`, `WorkflowRiskMatrix.md`, `WorkflowCapabilityReport.md`, and `WorkflowCoverageReport.md`.
- **Architectural Enforcement**: Tests actively block words like `page`, `locator`, `click`, and `playwright` from ever appearing inside `src/workflows/`.

## Interaction Engine (Phase 6A)
The `src/interaction/` subsystem acts as the sole bridge between UAT scenarios and the UI automation runner (BrowserPlugin), completely isolating Playwright.
- **Intent-Based API**: Exposes semantic methods like `navigate()`, `setValue()`, and `choose()` operating exclusively on an `ElementId` enum, completely hiding CSS/XPath.
- **Event-Driven Evidence**: Emits standard events (`InteractionStarted`, `InteractionSucceeded`, `InteractionFailed`) to the `EventBus`. The `EvidencePlugin` uses these to trigger automatic screenshots without direct calls.
- **Auto-Wait & Retry**: Handles UI synchronization internally (Visibility, Enabled) tracking exact metrics (`durationMs`, `retries`, `executionTimeMs`).

## Screen Library (Phase 6B)
The `src/screens/` subsystem models the application UI surface area.
- **Component Composition**: Screens (`LoginScreen`, `POSScreen`) are not procedural. They are composed of standard `UIComponents` (`UIForm`, `UITable`, `UIDialog`, `UISearch`) constructed via dependency injection.
- **Zero Logic**: Screens do not calculate business rules, execute workflows, or import Playwright. They only translate high-level screen capabilities (e.g. `scanBarcode()`) into generic `InteractionEngine` calls.

## Business Scenario Orchestration (Phase 6C)
The `src/scenarios/` subsystem is the final capstone combining all preceding layers.
- **Strict Orchestration Boundary**: Scenarios contain zero business rules, zero UI assertions (`expect()`), zero direct Playwright calls, and zero SQL queries. They exclusively call Semantic operations on injected Screens.
- **BusinessScenarioBase Lifecycle**: Utilizes Inversion of Control to manage setup, validation, and teardown. It automatically injects the correct Persona and Data Profile, and seamlessly calls the Phase 4 Validation Engine and Phase 5 Rule Packs during its `teardown()` hook without developer intervention.
- **Policies & Context**: Every scenario defines explicit policies (`ValidationPolicy`, `CleanupPolicy`, `EvidencePolicy`, `RetryPolicy`). The output is an `ExtendedScenarioResult` containing execution metrics, timeline traces, and root cause hints.
- **Artifacts**: Continuously generates a `ScenarioCatalog.md`, Mermaid `ScenarioDependencyGraph.md`, `ScenarioCoverageReport.md`, and `ScenarioReplay.json` bridging abstract Workflows to executable code.

## ERP Vertical Capabilities (Phase 7)
Phase 7 expanded the knowledge base across the 8 core ERP domains: Sales, Returns, Purchasing, Inventory, Loyalty, GST, Finance, and Reports.
- **Vertical Alignment**: Every capability traverses the entire frozen architecture stack: `Capability Metadata -> Workflows -> Scenarios -> Screens -> Rule Packs -> Invariants`.
- **Registry & Docs**: The `CapabilityRegistry` maintains metadata (Owner, Risk, Priority, Exit Criteria) and generates the `CapabilityCoverageReport.md` and overarching `CapabilityDependencyGraph.md`.
- **Architectural Strictness**: No changes were made to the core engine. Broad ERP domain logic was expanded entirely within the abstract, Playwright-free boundaries constructed in Phases 4-6.

## Autonomous Execution Repository (Phase 7.5)
The `src/repository/` subsystem serves as a passive data lake that captures historical execution telemetry to support AI-driven triage.
- **Data Boundaries**: The repository layer is strictly forbidden from importing Scenarios, Workflows, or Rules. It only consumes plain `ExecutionRecord` and `FailureRecord` payloads from the engine.
- **Immutable Persistence**: To prevent history tampering, repositories like `ExecutionRepository` and `FailureRepository` write immutable timestamped JSON artifacts. SQL dependencies are avoided at this stage to keep the reporting pipeline decoupled and portable.
- **Failure Fingerprinting**: The `AnalyticsEngine` deterministically hashes failures based on the Workflow + Scenario + Capability + Evidence signatures, allowing it to group seemingly isolated failures into systemic **Failure Clusters**.
- **Automated Dashboards**: The `ReportingEngine` produces `ExecutionDashboard.md`, `FailureTrendReport.md`, and an `AITrainingDataset.json` for Phase 8 ingestion.

## AI Deterministic Defect Triage (Phase 8A)
The `src/ai/` subsystem introduces autonomous intelligence into the pipeline by consuming the historical execution repositories. Before invoking non-deterministic LLMs, the platform runs a strict deterministic heuristic pipeline.
- **Stateless Pipeline**: The `DefectTriageEngine` takes a singular `AITriageInput` contract and pipes it through a normalization layer followed by specific Analyzers (`RegressionAnalyzer`, `OwnershipAnalyzer`, `ReleaseRiskAnalyzer`).
- **Explainability**: Every finding returned by an analyzer is wrapped in an `Explainability` object detailing the `decision`, the supporting `evidence`, the `reason`, and the `deterministic confidence` score.
- **Root Cause Hypotheses**: The heuristic pipeline deterministically calculates potential root causes ordered by `Probability × Evidence Strength × Historical Support`.
- **Architectural Isolation**: The AI subsystem is forbidden from importing Scenarios, Workflows, Rules, UI APIs (Playwright), or SQL drivers. It only evaluates raw telemetry data to generate `AITriageReport.md` and `AITriageOutput.json`.

## LLM Triage Explanation (Phase 8B)
The `src/ai/llm/` subsystem consumes the output of the Deterministic Triage engine to generate human-readable explanations tailored for specific personas (Executive, Developer, QA, Release Manager).
- **Prompt Guardrails**: The `PromptBuilder` enforces strict guardrails. The LLM is explicitly instructed to **never** change the deterministic Priority, Owner, Release Risk, or Findings. It acts solely as a translator/explainer.
- **Mock Client Pattern**: The `ILLMClient` interface allows swapping AI providers. The current `MockLLMClient` simulates structured JSON output, ensuring architectural pathways are tested without live API dependencies.
- **Reporting Generator**: Outputs the 4 styled markdown reports (`ExecutiveReport.md`, etc.) and a final `AITriageExplanation.json`.
- **Strict Isolation**: The LLM layer is forbidden from importing the Repositories, Rules, Workflows, or Scenarios. It is completely disconnected from the execution context and relies purely on the `AITriageExplanationInput`.

## Real ERP Integration (Phase 9)
The final validation phase where the AI QA Platform connects directly to the real Apple Supermarket POS & ERP System environments.
- **Environment Agnostic**: The `Environment` config securely extracts `UAT`, `PROD`, and `LOCAL` URLs, connection strings, and credentials dynamically from the OS layer, avoiding hardcoding.
- **Plugin Instantiation**: The concrete plugins (`BrowserPlugin`, `DatabasePlugin`, `ApiPlugin`, `EvidencePlugin`) are fully mapped to the external UI and DB dependencies (Playwright / PostgreSQL).
- **Automated Handshake**: A scenario execution failure now natively triggers the `DefectTriageEngine` and `ExplanationBuilder` synchronously, proving the holistic E2E flow.

## Operational Quality Validation (Phase 10)
The platform shifts from construction to operation, enabling continuous validation against the real POS & ERP ecosystem.
- **Nightly Regression Orchestration**: The `NightlyRegression` runner executes the full integration suite (Cash Sale, Returns, Purchasing, Inventory, Finance) sequentially, acting as the master CI trigger.
- **Autonomous Defect Backlog**: The `DefectBacklogBuilder` parses the `ExecutionRepository` data natively and automatically generates a prioritized `DefectBacklog.md` (P0, P1, P2, P3).
- **Quality KPI Dashboards**: The platform calculates operational metrics (Precision, Recall, F1, FPR, FNR) and determines Release Readiness strictly based on architectural P0 blocks, fulfilling the ultimate objective of the AI QA Platform.
