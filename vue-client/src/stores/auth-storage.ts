// Storage keys shared between the axios interceptor and the Pinia auth
// store. Pulled into its own file (no imports!) to break the dependency
// cycle: client.ts ↔ stores/auth.ts ↔ api/auth.ts ↔ client.ts. With the
// cycle, `TOKEN_KEY` arrived as `undefined` in client.ts at module-eval
// time, so the interceptor read the wrong localStorage key and the
// bearer token never reached the API → 401 on every authed request.

export const TOKEN_KEY = 'pm.token'
export const USER_KEY  = 'pm.user'
