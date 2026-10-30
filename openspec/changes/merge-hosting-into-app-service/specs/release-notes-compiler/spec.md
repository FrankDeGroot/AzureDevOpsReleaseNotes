# Spec Delta

## ADDED Requirements

### Requirement: Authenticated compilation outside local Development
Outside local Development, `POST /api/releases/compile` SHALL require a Microsoft Entra identity validated by App Service Easy Auth for the configured tenant and API audience. Unauthenticated requests SHALL return HTTP 401 without compilation or persistence. Function keys SHALL NOT substitute for an authenticated identity.

#### Scenario: Authenticated pipeline trigger
- **WHEN** a caller supplies a valid bearer token accepted by Easy Auth and valid compilation metadata
- **THEN** compilation runs and returns the existing HTTP 200 release summary

#### Scenario: Anonymous Azure request
- **WHEN** a caller posts to the Azure compile endpoint without an authenticated identity
- **THEN** the response is HTTP 401 rather than a sign-in redirect
- **AND** the request does not query Azure DevOps or write a release document

#### Scenario: Legacy Function key
- **WHEN** a caller supplies only `x-functions-key` or a `code` query value outside local Development
- **THEN** compilation is rejected with HTTP 401

#### Scenario: Untrusted identity input
- **WHEN** a caller attempts to supply identity headers without platform-validated authentication
- **THEN** the request is not accepted as authenticated and compilation does not run

#### Scenario: Missing production authentication configuration
- **WHEN** a non-Development deployment lacks the required Easy Auth trust configuration
- **THEN** compilation remains unavailable rather than becoming anonymous

### Requirement: Anonymous compilation is limited to local Development
A locally running host explicitly configured for Development SHALL permit anonymous compilation using the existing request validation and domain behavior. The local bypass SHALL NOT apply to an Azure-hosted app or a non-Development host.

#### Scenario: Local development request
- **WHEN** a developer posts valid build metadata to the local Development host without an Entra token
- **THEN** compilation can succeed using the developer's configured outbound Azure DevOps credentials

#### Scenario: Local development validation
- **WHEN** an anonymous request to the local Development host contains malformed JSON or missing required metadata
- **THEN** the endpoint returns the existing HTTP 400 JSON error response

#### Scenario: Azure configured as Development
- **WHEN** an Azure-hosted instance is accidentally configured with the Development environment name
- **THEN** the local anonymous bypass does not grant compile access