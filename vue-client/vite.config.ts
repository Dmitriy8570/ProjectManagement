import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  server: {
    port: 5173,
    proxy: {
      // Target is the HTTPS port directly. The HTTP port (5266) would
      // trigger `UseHttpsRedirection` on the API, which returns a 307 to
      // https://localhost:7283 — and the browser strips the Authorization
      // header on cross-origin redirects, killing JWT auth. Going straight
      // to HTTPS keeps the whole hop server-side, so the browser only ever
      // sees same-origin localhost:5173.
      //
      // `secure: false` accepts the ASP.NET dev cert without a CA chain.
      '/api': {
        target: 'https://localhost:7283',
        changeOrigin: true,
        secure: false
      }
    }
  }
})
