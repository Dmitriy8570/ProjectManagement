<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { employeesApi } from '@/api/employees'
import { useNotification } from '@/stores/notification'

const router  = useRouter()
const notif   = useNotification()

const firstName  = ref('')
const lastName   = ref('')
const patronymic = ref('')
const email      = ref('')
const saving     = ref(false)
const errors     = ref<string[]>([])

function validate(): boolean {
  errors.value = []
  if (!firstName.value.trim())  errors.value.push('First name is required.')
  if (!lastName.value.trim())   errors.value.push('Last name is required.')
  if (!email.value.trim())      errors.value.push('Email is required.')
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value)) errors.value.push('Invalid email format.')
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
    })
    notif.show(`Employee created.`)
    router.push(`/employees/${r.id}`)
  } catch (e: any) { notif.show(e.message, 'error') }
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
