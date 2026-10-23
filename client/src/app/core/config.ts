/**
 * Single switch for the frontend data source: true = in-memory MockApiService (feature
 * development without a backend), false = the real HTTP client via ApiService.
 * Deliberately a constant, not an Angular environment file — one flag, zero ceremony.
 */
export const USE_MOCKS = false;
