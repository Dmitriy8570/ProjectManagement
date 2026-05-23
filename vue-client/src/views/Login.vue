<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '@/stores/auth'

const route   = useRoute()
const router  = useRouter()
const auth    = useAuth()

const email    = ref('')
const password = ref('')
const submitting = ref(false)
const errorMsg   = ref('')

async function submit() {
  errorMsg.value = ''
  if (!email.value.trim() || !password.value) {
    errorMsg.value = 'Email and password are required.'
    return
  }

  submitting.value = true
  try {
    await auth.login(email.value.trim(), password.value)
    // Validate-after-login returnUrl: only local paths are honored, anything
    // off-site is treated as malicious and we fall back to /projects.
    const returnUrl = (route.query.returnUrl as string | undefined) ?? '/projects'
    const safe = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/projects'
    router.push(safe)
  } catch (e: any) {
    errorMsg.value = e?.message || 'Invalid credentials.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="d-flex justify-content-center align-items-center" style="min-height:80vh;">
    <div class="card" style="width:100%;max-width:420px;">
      <div class="card-header">
        <i class="bi bi-box-arrow-in-right me-2"></i>Sign in
      </div>
      <div class="card-body">
        <div v-if="errorMsg" class="alert alert-danger py-2 mb-3">
          <i class="bi bi-exclamation-triangle me-1"></i>{{ errorMsg }}
        </div>

        <form @submit.prevent="submit">
          <div class="mb-3">
            <label class="form-label">Email</label>
            <input
              v-model="email"
              type="email"
              class="form-control"
              autocomplete="email"
              autofocus
            />
          </div>
          <div class="mb-3">
            <label class="form-label">Password</label>
            <input
              v-model="password"
              type="password"
              class="form-control"
              autocomplete="current-password"
            />
          </div>
          <button type="submit" class="btn btn-primary w-100" :disabled="submitting">
            <span v-if="submitting" class="spinner-border spinner-border-sm me-1"></span>
            <i v-else class="bi bi-box-arrow-in-right me-1"></i>Sign in
          </button>
        </form>
      </div>
    </div>
  </div>
</template>
