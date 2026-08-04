# Security Module Knowledge Base

## Module Overview
The Security module handles Authentication, Authorization (RBAC), and overrides.

## Configurable Business Policies
- **Concurrent Sessions**: Defines if a user can login to multiple terminals at once.

## Business Rules

### SEC-01: Authentication
- **Module**: Security
- **Priority**: Critical
- **Rule Type**: Security
- **Configurable**: No
- **Source**: `AuthController.cs`
- **Applies To**: All APIs
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: All API endpoints require a valid JWT.

### SEC-02: Terminal Binding
- **Module**: Security
- **Priority**: High
- **Rule Type**: Security
- **Configurable**: No
- **Source**: `AuthController.cs`
- **Applies To**: POS Login
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Login returns a `TerminalId`.

### SEC-03: Token Refresh
- **Module**: Security
- **Priority**: High
- **Rule Type**: Security
- **Configurable**: No
- **Source**: `AuthController.cs`
- **Applies To**: Token issuance
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Refresh tokens are bound to a device fingerprint.

### SEC-04: Manager Override
- **Module**: Security
- **Priority**: High
- **Rule Type**: Security
- **Configurable**: Yes
- **Source**: Config
- **Applies To**: Restricted Ops
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: Specific operations require a `SupervisorOverridePin`.

### SEC-05: PIN Management
- **Module**: Security
- **Priority**: High
- **Rule Type**: Security
- **Configurable**: No
- **Source**: `AuthController.cs`
- **Applies To**: Users
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Only Admin/Manager can change another user's PIN.

## Expected Behaviour
- Login issues short-lived JWT and HTTP-only refresh token.
- Logout revokes refresh token.
