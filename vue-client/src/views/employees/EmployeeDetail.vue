<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { employeesApi } from '@/api/employees'
import { tasksApi } from '@/api/tasks'
import { useNotification } from '@/stores/notification'
import type { EmployeeDto, EmployeeProjectsDto, ProjectTaskDto, ProjectTaskStatus } from '@/types'

const route  = useRoute()
const router = useRouter()
const notif  = useNotification()
const id     = Number(route.params.id)

const employee      = ref<EmployeeDto | null>(null)
const projects      = ref<EmployeeProjectsDto>({ managedProjects: [], participantProjects: [] })
const assignedTasks = ref<ProjectTaskDto[]>([])
const loading       = ref(true)

function taskStatusBadge(s: ProjectTaskStatus) {
  return s === 'Done' ? 'bg-success' : s === 'InProgress' ? 'bg-primary' : 'bg-secondary'
}
function taskStatusLabel(s: ProjectTaskStatus) {
  return s === 'Done' ? 'Done' : s === 'InProgress' ? 'In Progress' : 'To Do'
}

async function load() {
  loading.value = true
  try {
    const [emp, proj, tasks] = await Promise.all([
      employeesApi.getById(id),
      employeesApi.getProjects(id),
      tasksApi.list({ assigneeId: id, pageSize: 50 }),
    ])
    employee.value      = emp
    projects.value      = proj
    assignedTasks.value = tasks.items
  } catch { router.push('/employees') }
  finally { loading.value = false }
}

async function del() {
  if (!employee.value) return
  if (!confirm(`Delete employee ${employee.value.fullName}?`)) return
  try {
    await employeesApi.delete(id)
    notif.show(`${employee.value.fullName} deleted.`)
    router.push('/employees')
  } catch (e: any) { notif.show(e.message, 'error') }
}

onMounted(load)
</script>

<template>
  <div v-if="loading" class="text-center py-5"><div class="spinner-border"></div></div>
  <template v-else-if="employee">
    <nav aria-label="breadcrumb" class="mb-3">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/employees">Employees</RouterLink></li>
        <li class="breadcrumb-item active">{{ employee.fullName }}</li>
      </ol>
    </nav>

    <div class="pm-page-header">
      <h2>{{ employee.fullName }}</h2>
      <div class="d-flex gap-2">
        <RouterLink :to="`/employees/${id}/edit`" class="btn btn-outline-secondary">
          <i class="bi bi-pencil me-1"></i>Edit
        </RouterLink>
        <button class="btn btn-outline-danger" @click="del">
          <i class="bi bi-trash me-1"></i>Delete
        </button>
      </div>
    </div>

    <div class="row g-4">
      <!-- Info card -->
      <div class="col-md-4">
        <div class="card">
          <div class="card-header"><i class="bi bi-person me-2"></i>Personal Information</div>
          <div class="card-body">
            <dl class="row mb-0">
              <dt class="col-5 text-muted">Last Name</dt>
              <dd class="col-7">{{ employee.lastName }}</dd>
              <dt class="col-5 text-muted">First Name</dt>
              <dd class="col-7">{{ employee.firstName }}</dd>
              <template v-if="employee.patronymic">
                <dt class="col-5 text-muted">Patronymic</dt>
                <dd class="col-7">{{ employee.patronymic }}</dd>
              </template>
              <dt class="col-5 text-muted">Email</dt>
              <dd class="col-7 text-break">
                <a :href="`mailto:${employee.email}`" class="text-decoration-none">{{ employee.email }}</a>
              </dd>
            </dl>
          </div>
        </div>
      </div>

      <!-- Managed projects -->
      <div class="col-md-4">
        <div class="card h-100">
          <div class="card-header d-flex align-items-center justify-content-between">
            <span><i class="bi bi-person-badge me-2"></i>Projects as Manager</span>
            <span class="badge bg-secondary">{{ projects.managedProjects.length }}</span>
          </div>
          <div class="card-body">
            <p v-if="!projects.managedProjects.length" class="text-muted small mb-0">No managed projects.</p>
            <ul v-else class="list-group list-group-flush">
              <li v-for="p in projects.managedProjects" :key="p.id"
                  class="list-group-item px-0 py-1">
                <RouterLink :to="`/projects/${p.id}`" class="text-decoration-none small">
                  <i class="bi bi-folder me-2 text-muted"></i>{{ p.name }}
                </RouterLink>
              </li>
            </ul>
          </div>
        </div>
      </div>

      <!-- Participant projects -->
      <div class="col-md-4">
        <div class="card h-100">
          <div class="card-header d-flex align-items-center justify-content-between">
            <span><i class="bi bi-people me-2"></i>Projects as Participant</span>
            <span class="badge bg-secondary">{{ projects.participantProjects.length }}</span>
          </div>
          <div class="card-body">
            <p v-if="!projects.participantProjects.length" class="text-muted small mb-0">Not participating in any projects.</p>
            <ul v-else class="list-group list-group-flush">
              <li v-for="p in projects.participantProjects" :key="p.id"
                  class="list-group-item px-0 py-1">
                <RouterLink :to="`/projects/${p.id}`" class="text-decoration-none small">
                  <i class="bi bi-folder me-2 text-muted"></i>{{ p.name }}
                </RouterLink>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>

    <!-- Assigned Tasks -->
    <div class="card mt-4">
      <div class="card-header d-flex align-items-center justify-content-between">
        <span><i class="bi bi-list-check me-2"></i>Assigned Tasks</span>
        <span class="badge bg-secondary">{{ assignedTasks.length }}</span>
      </div>
      <div class="card-body p-0">
        <div v-if="!assignedTasks.length" class="text-muted small px-3 py-3 mb-0">No tasks assigned.</div>
        <div v-else class="table-responsive">
          <table class="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th>Task</th>
                <th>Project</th>
                <th class="text-center">Status</th>
                <th class="text-center">Priority</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in assignedTasks" :key="t.id">
                <td>
                  <RouterLink :to="`/tasks/${t.id}`" class="fw-semibold text-decoration-none">
                    {{ t.name }}
                  </RouterLink>
                </td>
                <td class="small">
                  <RouterLink :to="`/projects/${t.projectId}`" class="text-decoration-none">
                    {{ t.projectName }}
                  </RouterLink>
                </td>
                <td class="text-center">
                  <span class="badge" :class="taskStatusBadge(t.status)">{{ taskStatusLabel(t.status) }}</span>
                </td>
                <td class="text-center">
                  <span class="badge bg-secondary">{{ t.priority }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </template>
</template>
