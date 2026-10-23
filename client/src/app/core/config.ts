/**
 * Single switch for the frontend data source. The feature tracks build against the mock
 * API (true); the integration stage flips this to false and the real HTTP client takes over.
 * Deliberately a constant, not an Angular environment file — one flag, zero ceremony.
 */
export const USE_MOCKS = true;
