<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { employeesApi } from '@/api/employees'
import { useNotification } from '@/stores/notification'
import { Roles, type RoleName } from '@/types'

const router  = useRouter()
const notif   = useNotification()

const firstName  = ref('')
const lastName   = ref('')
const patronymic = ref('')
const email      = ref('')
const password   = ref('')
const role       = ref<RoleName>(Roles.Employee)
const saving     = ref(false)
const errors     = ref<string[]>([])

// Mirror of Identity's default password policy. Hardcoded for now — could be
// fetched from the server if the rules ever diverge, but for this task
// the defaults are stable.
const passwordRules = [
  'at least 6 characters',
  'an uppercase letter (A-Z)',
  'a lowercase letter (a-z)',
  'a digit (0-9)',
  'a special character (e.g. !@#$%)',
]

const roleOptions: { value: RoleName, label: string }[] = [
  { value: Roles.Director,       label: 'Director' },
  { value: Roles.ProjectManager, label: 'Project Manager' },
  { value: Roles.Employee,       label: 'Employee' },
]

function validate(): boolean {
  errors.value = []
  if (!firstName.value.trim())  errors.value.push('First name is required.')
  if (!lastName.value.trim())   errors.value.push('Last name is required.')
  if (!email.value.trim())      errors.value.push('Email is required.')
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value)) errors.value.push('Invalid email format.')
  if (!password.value) {
    errors.value.push('Password is required.')
  } else {
    if (password.value.length < 6)          errors.value.push('Password must be at least 6 characters.')
    if (!/[A-Z]/.test(password.value))      errors.value.push('Password must contain an uppercase letter (A-Z).')
    if (!/[a-z]/.test(password.value))      errors.value.push('Password must contain a lowercase letter (a-z).')
    if (!/[0-9]/.test(password.value))      errors.value.push('Password must contain a digit (0-9).')
    if (!/[^a-zA-Z0-9]/.test(password.value)) errors.value.push('Password must contain a special character (e.g. !@#$%).')
  }
  return !errors.value.length
}

async function submit() {
  if (!validate()) return
  saving.value = true
  try {
    const r = await employeesApi.create({
      firstName: firstName.value.trim(),
      lastName:  lastName.value.trim(),
      patronymic: patronymic.value.trim() || undefined,
      email: email.value.trim(),
      password: password.value,
      role: role.value,
    })
    notif.show('Employee created.')
    router.push(`/employees/${r.id}`)
  } catch (e: any) {
    // Server-side password / email policy failures arrive here as
    // DomainValidationException → ProblemDetails → Error.message. Surface
    // them directly so the user sees exactly why it was rejected.
    notif.show(e.message, 'error')
  }
  finally { saving.value = false }
}
</script>

<template>
  <nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
      <li class="breadcrumb-item"><RouterLink to="/employees">Employees</RouterLink></li>
      <li class="breadcrumb-item active">Add Employee</li>
    </ol>
  </nav>

  <div class="row justify-content-center">
    <div class="col-lg-6">
      <div class="card">
        <div class="card-header"><i class="bi bi-person-plus me-2"></i>Add Employee</div>
        <div class="card-body">
          <div v-if="errors.length" class="alert alert-danger mb-3">
            <div v-for="e in errors" :key="e">{{ e }}</div>
          </div>

          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">Last Name *</label>
              <input v-model="lastName" type="text" class="form-control" />
            </div>
            <div class="col-md-6">
              <label class="form-label">First Name *</label>
              <input v-model="firstName" type="text" class="form-control" />
            </div>
            <div class="col-md-6">
              <label class="form-label">Patronymic <span class="text-muted fw-normal">(optional)</span></label>
              <input v-model="patronymic" type="text" class="form-control" />
            </div>
            <div class="col-12">
              <label class="form-label">Email *</label>
              <input v-model="email" type="email" class="form-control" />
            </div>
            <div class="col-12">
              <label class="form-label">Password *</label>
              <input v-model="password" type="password" class="form-control" autocomplete="new-password" />
              <div class="form-text">
                Password must contain:
                <ul class="mb-0 ps-3">
                  <li v-for="rule in passwordRules" :key="rule">{{ rule }}</li>
                </ul>
              </div>
            </div>
            <div class="col-md-6">
              <label class="form-label">Role *</label>
              <select v-model="role" class="form-select">
                <option v-for="opt in roleOptions" :key="opt.value" :value="opt.value">
                  {{ opt.label }}
                </option>
              </select>
            </div>
          </div>

          <div class="d-flex gap-2 mt-4">
            <button type="button" class="btn btn-primary" :disabled="saving" @click="submit">
              <span v-if="saving" class="spinner-border spinner-border-sm me-1"></span>
              <i v-else class="bi bi-check-lg me-1"></i>Create Employee
            </button>
            <RouterLink to="/employees" class="btn btn-outline-secondary">Cancel</RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
