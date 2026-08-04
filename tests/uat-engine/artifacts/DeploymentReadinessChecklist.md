# Deployment Readiness Checklist

Use this checklist to ensure the AI QA Platform is fully prepared to execute in a CI/CD pipeline targeting the real ERP.

## 1. Environment Configuration
- [x] Defined `LOCAL`, `UAT`, and `PROD` profiles.
- [x] Extracted ERP URL to environment variables.
- [x] Extracted API Base URL to environment variables.
- [x] Extracted Database Connection String to environment variables.
- [x] Extracted Cashier Credentials securely.

## 2. Plugins & Telemetry
- [x] `BrowserPlugin` configured for Headless execution in CI.
- [x] `DatabasePlugin` network rules allow connection from CI runner to PostgreSQL.
- [x] `EvidencePlugin` bound to `artifacts/evidence` directory for upload upon CI failure.

## 3. Scenarios
- [x] The core `CashSaleScenario` refactored from mocked assertions to actual DOM assertions.
- [x] Dependencies between Scenarios removed (stateless runs).
- [x] Teardown hooks correctly clear database states.

## 4. AI Triage Engine
- [x] Triage Output correctly serialized to `.json`.
- [x] AI Models successfully mock/generate human-readable explanations.
- [x] Downstream bug-trackers ready to ingest `DeveloperReport.md`.

## 5. Security
- [x] No credentials hardcoded in codebase.
- [x] No PII exposed in `AITriageExplanationInput`.
