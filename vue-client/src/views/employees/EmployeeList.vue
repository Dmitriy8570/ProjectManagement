<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { employeesApi } from '@/api/employees'
import { useNotification } from '@/stores/notification'
import type { EmployeeDto } from '@/types'

const router = useRouter()
const notif  = useNotification()

const employees    = ref<EmployeeDto[]>([])
const loading      = ref(false)
const searchTerm   = ref('')
const searchResults= ref<EmployeeDto[]>([])
const showDropdown = ref(false)
let   timer        = 0

async function load() {
  loading.value = true
  try { employees.value = await employeesApi.search('', 100) }
  catch (e: any) { notif.show(e.message, 'error') }
  finally { loading.value = false }
}

async function search(term: string) {
  try {
    searchResults.value = await employeesApi.search(term, 10)
    showDropdown.value  = true
  } catch { showDropdown.value = false }
}
function onFocus() { search(searchTerm.value) }
function onInput() { clearTimeout(timer); timer = window.setTimeout(() => search(searchTerm.value), 220) }
function onBlur()  { setTimeout(() => { showDropdown.value = false }, 180) }
function goTo(id: number) { searchTerm.value = ''; showDropdown.value = false; router.push(`/employees/${id}`) }

async function del(e: EmployeeDto) {
  if (!confirm(`Delete employee ${e.fullName}?`)) return
  try {
    await employeesApi.delete(e.id)
    notif.show(`${e.fullName} deleted.`)
    load()
  } catch (err: any) { notif.show(err.message, 'error') }
}

function initials(e: EmployeeDto) {
  return ((e.firstName[0] || '') + (e.lastName[0] || '')).toUpperCase()
}

onMounted(load)
</script>

<template>
  <div class="pm-page-header">
    <h2><i class="bi bi-people"></i>Employees
      <span class="badge bg-secondary ms-2 fs-6">{{ employees.length }}</span>
    </h2>
    <RouterLink to="/employees/create" class="btn btn-primary">
      <i class="bi bi-person-plus me-1"></i>Add Employee
    </RouterLink>
  </div>

  <!-- Quick search -->
  <div class="card mb-4">
    <div class="card-body py-2">
      <div class="autocomplete-wrapper" style="max-width:400px;">
        <input
          v-model="searchTerm"
          type="text"
          class="form-control"
          placeholder="Search employees by name…"
          @focus="onFocus"
          @input="onInput"
          @blur="onBlur"
        />
        <div v-if="showDropdown" class="autocomplete-dropdown" style="display:block;">
          <div v-if="!searchResults.length" class="autocomplete-item no-results">No employees found</div>
          <div
            v-for="e in searchResults" :key="e.id"
            class="autocomplete-item"
            @mousedown.prevent="goTo(e.id)"
          >{{ e.fullName }}</div>
        </div>
      </div>
    </div>
  </div>

  <div v-if="loading" class="text-center py-5 text-muted">
    <div class="spinner-border"></div>
  </div>
  <div v-else-if="!employees.length" class="text-center py-5 text-muted">
    <i class="bi bi-people d-block fs-1 mb-2 opacity-25"></i>No employees yet.
  </div>
  <div v-else class="row g-3">
    <div v-for="emp in employees" :key="emp.id" class="col-md-6 col-lg-4">
      <div class="card h-100">
        <div class="card-body">
          <div class="d-flex align-items-center gap-3 mb-3">
            <div
              style="width:42px;height:42px;border-radius:50%;background:var(--accent-light);color:var(--accent);
                     display:flex;align-items:center;justify-content:center;font-weight:700;font-size:.875rem;flex-shrink:0;"
            >{{ initials(emp) }}</div>
            <div class="min-w-0">
              <RouterLink :to="`/employees/${emp.id}`" class="fw-semibold text-decoration-none d-block text-truncate">
                {{ emp.fullName }}
              </RouterLink>
              <div class="text-muted small text-truncate">{{ emp.email }}</div>
            </div>
          </div>
        </div>
        <div class="card-footer d-flex gap-2">
          <RouterLink :to="`/employees/${emp.id}/edit`" class="btn btn-sm btn-outline-secondary flex-fill">
            <i class="bi bi-pencil me-1"></i>Edit
          </RouterLink>
          <button class="btn btn-sm btn-outline-danger" @click="del(emp)">
            <i class="bi bi-trash"></i>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
