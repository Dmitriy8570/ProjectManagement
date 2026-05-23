import axios, { AxiosHeaders } from 'axios'
import { TOKEN_KEY, USER_KEY } from '@/stores/auth-storage'

const client = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' }
})

// Attach the bearer token (if any) to every request. Use AxiosHeaders.set()
// explicitly so the header object's internal map is updated reliably
// regardless of what type axios hands us.
client.interceptors.request.use(cfg => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    const headers = AxiosHeaders.from(cfg.headers as any)
    headers.set('Authorization', `Bearer ${token}`)
    cfg.headers = headers
  }
  return cfg
})

client.interceptors.response.use(
  r => r,
  err => {
    // 401 means the token is missing/expired/invalid — clear the cached
    // session and bounce to /login. Hard navigation (not router.push) so
    // any in-flight state is wiped and the next page boots clean.
    if (err.response?.status === 401) {
      const path = window.location.pathname
      if (!path.startsWith('/login')) {
        localStorage.removeItem(TOKEN_KEY)
        localStorage.removeItem(USER_KEY)
        const returnUrl = encodeURIComponent(path + window.location.search)
        window.location.href = `/login?returnUrl=${returnUrl}`
      }
    }

    const data = err.response?.data
    const msg = data?.detail || data?.title || err.message || 'Unknown error'
    return Promise.reject(new Error(msg))
  }
)

export default client
