import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAuth } from './stores/auth'
import './assets/main.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)

// Re-validate the cached token before the first navigation completes. A
// 401 from /auth/me triggers the interceptor's logout-and-redirect path; a
// 200 refreshes the user state from the server.
const auth = useAuth()
auth.refreshMe().finally(() => {
  app.mount('#app')
})
