import client from './client'
import type { CurrentUserDto, LoginResponse } from '@/types'

export const authApi = {
  login: (email: string, password: string) =>
    client.post<LoginResponse>('/auth/login', { email, password }).then(r => r.data),

  /**
   * Re-fetches the current identity from the server using the cached bearer
   * token — used on app boot to rehydrate the auth store if the token is
   * still valid, or to bounce the user to /login if it isn't.
   */
  me: () =>
    client.get<CurrentUserDto>('/auth/me').then(r => r.data),

  /**
   * Server-side this is a no-op for the bearer scheme (nothing to revoke);
   * still issued for symmetry and to leave a place for future
   * refresh-token cleanup.
   */
  logout: () =>
    client.post('/auth/logout'),
}
