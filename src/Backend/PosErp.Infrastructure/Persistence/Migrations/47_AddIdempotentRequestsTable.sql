-- Migration 47: AddIdempotentRequestsTable
-- Description: Create idempotent_requests table for ClientRequestToken idempotency tracking and cleanup.

CREATE TABLE IF NOT EXISTS idempotent_requests (
    client_request_token UUID PRIMARY KEY,
    status TEXT NOT NULL,
    response_payload TEXT,
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    tenant_id UUID NOT NULL
);

-- Index to optimize nightly background worker cleanup deletion queries
CREATE INDEX IF NOT EXISTS IX_idempotent_requests_created_at ON idempotent_requests(created_at);
