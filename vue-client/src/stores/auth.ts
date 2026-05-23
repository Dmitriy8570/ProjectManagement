import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '@/api/auth'
import { TOKEN_KEY, USER_KEY } from '@/stores/auth-storage'
import type { CurrentUserDto, RoleName } from '@/types'

function readUser(): CurrentUserDto | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null
  try { return JSON.parse(raw) } catch { return null }
}

/**
 * Caches the JWT and the user identity in localStorage so a hard reload
 * doesn't drop the session. The bearer token is also read directly out of
 * localStorage by the axios interceptor — keep the key in sync if renaming.
 */
export const useAuth = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const user  = ref<CurrentUserDto | null>(readUser())

  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const employeeId      = computed(() => user.value?.employeeId ?? null)
  const roles           = computed(() => user.value?.roles ?? [])

  function hasRole(role: RoleName)        { return roles.value.includes(role) }
  function hasAnyRole(allowed: RoleName[]) { return allowed.some(r => roles.value.includes(r)) }

  async function login(email: string, password: string) {
    const r = await authApi.login(email, password)
    token.value = r.token
    user.value  = r.user
    localStorage.setItem(TOKEN_KEY, r.token)
    localStorage.setItem(USER_KEY, JSON.stringify(r.user))
  }

  /**
   * Verifies the cached token by asking the server who we are. Called on
   * app boot — a stale token returns 401, the interceptor clears state and
   * routes to /login.
   */
  async function refreshMe() {
    if (!token.value) return
    try {
      const me = await authApi.me()
      user.value = me
      localStorage.setItem(USER_KEY, JSON.stringify(me))
    } catch {
      // The interceptor handles the 401 cleanup — nothing to do here.
    }
  }

  /** Clears local state. Doesn't call the server (bearer logout is a no-op). */
  function logout() {
    token.value = null
    user.value  = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }

  return {
    token, user, isAuthenticated, employeeId, roles,
    hasRole, hasAnyRole, login, refreshMe, logout,
  }
})
