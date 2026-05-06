CREATE TABLE inbox_items (
    id              TEXT NOT NULL PRIMARY KEY,
    content         TEXT NOT NULL,
    source          TEXT NULL,
    captured_at_utc TEXT NOT NULL,
    created_at_utc  TEXT NOT NULL
);

CREATE INDEX ix_inbox_items_captured_at
    ON inbox_items (captured_at_utc DESC, id DESC);
