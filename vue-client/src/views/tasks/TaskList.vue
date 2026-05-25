<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { tasksApi } from '@/api/tasks'
import { projectsApi } from '@/api/projects'
import { useNotification } from '@/stores/notification'
import type { ProjectTaskDto, ProjectTaskStatus, ProjectDto } from '@/types'

const router = useRouter()
const route  = useRoute()
const notif  = useNotification()

// When the list is rendered under /projects/:id/tasks the project context is
// fixed; otherwise this is the global task list across all projects.
const fixedProjectId = computed(() => {
  const id = Number(route.params.projectId)
  return Number.isFinite(id) && id > 0 ? id : null
})

const tasks       = ref<ProjectTaskDto[]>([])
const total       = ref(0)
const loading     = ref(false)
const filterOpen  = ref(false)
// Full project info is fetched when scoped so the context banner can show
// dates and manager — these are the cheapest signals that the user is inside
// a single project rather than the global task view.
const project     = ref<ProjectDto | null>(null)
const projectName = computed(() => project.value?.name ?? '')

function fmtDate(s: string) {
  if (!s) return '—'
  const d = new Date(s)
  return `${String(d.getDate()).padStart(2,'0')}.${String(d.getMonth()+1).padStart(2,'0')}.${d.getFullYear()}`
}

function statusLabel(s: ProjectTaskStatus) {
  return s === 'Done' ? 'Done'
       : s === 'InProgress' ? 'In Progress'
       : 'To Do'
}

const filter = reactive({
  nameSearch: '',
  status: '' as '' | ProjectTaskStatus,
  minPriority: null as number | null,
  maxPriority: null as number | null,
  sortBy: 'Priority' as 'Name' | 'Priority' | 'Status',
  descending: true,
  page: 1,
  pageSize: 10,
})

const totalPages = computed(() => Math.ceil(total.value / filter.pageSize) || 1)

async function load() {
  loading.value = true
  try {
    const r = await tasksApi.list({
      projectId:   fixedProjectId.value ?? undefined,
      nameSearch:  filter.nameSearch || undefined,
      status:      filter.status || undefined,
      minPriority: filter.minPriority ?? undefined,
      maxPriority: filter.maxPriority ?? undefined,
      sortBy:      filter.sortBy,
      descending:  filter.descending,
      page:        filter.page,
      pageSize:    filter.pageSize,
    })
    tasks.value = r.items
    total.value = r.totalCount
  } catch (e: any) { notif.show(e.message, 'error') }
  finally { loading.value = false }
}

function applyFilters() { filter.page = 1; filterOpen.value = false; load() }
function clearFilters() {
  Object.assign(filter, { nameSearch: '', status: '', minPriority: null, maxPriority: null, sortBy: 'Priority', descending: true, page: 1 })
  filterOpen.value = false; load()
}
function goPage(p: number) { filter.page = p; load() }

async function deleteTask(t: ProjectTaskDto) {
  if (!confirm(`Delete task «${t.name}»? This cannot be undone.`)) return
  try { await tasksApi.delete(t.id); notif.show('Task deleted.'); load() }
  catch (e: any) { notif.show(e.message, 'error') }
}

async function quickStatus(t: ProjectTaskDto, status: ProjectTaskStatus) {
  try { await tasksApi.changeStatus(t.id, status); t.status = status; notif.show('Status updated.') }
  catch (e: any) { notif.show(e.message, 'error') }
}

function statusBadgeClass(s: ProjectTaskStatus) {
  return s === 'Done' ? 'bg-success'
       : s === 'InProgress' ? 'bg-primary'
       : 'bg-secondary'
}

const createPath = computed(() =>
  fixedProjectId.value ? `/projects/${fixedProjectId.value}/tasks/create` : '/tasks/create')

onMounted(async () => {
  // When scoped to a project, fetch the project so the context banner can
  // render dates/manager and link back.
  if (fixedProjectId.value) {
    try {
      project.value = await projectsApi.getById(fixedProjectId.value)
    } catch { router.push('/projects'); return }
  }
  await load()
})
</script>

<template>
  <Teleport to="body">
    <div v-if="filterOpen" class="filter-backdrop" @click="filterOpen = false"></div>
    <div class="filter-drawer" :class="{ open: filterOpen }">
      <div class="d-flex align-items-center justify-content-between p-3 border-bottom">
        <h6 class="mb-0 fw-semibold"><i class="bi bi-sliders me-2"></i>Filters</h6>
        <button type="button" class="btn-close" @click="filterOpen = false"></button>
      </div>
      <div class="p-3 flex-1 overflow-y-auto" style="overflow-y:auto;">
        <div class="mb-3">
          <label class="form-label">Task Name</label>
          <input v-model="filter.nameSearch" type="text" class="form-control" placeholder="Search…" />
        </div>
        <div class="mb-3">
          <label class="form-label">Status</label>
          <select v-model="filter.status" class="form-select">
            <option value="">Any</option>
            <option value="ToDo">To Do</option>
            <option value="InProgress">In Progress</option>
            <option value="Done">Done</option>
          </select>
        </div>
        <div class="row mb-3">
          <div class="col">
            <label class="form-label">Min Priority</label>
            <input v-model.number="filter.minPriority" type="number" min="0" class="form-control" />
          </div>
          <div class="col">
            <label class="form-label">Max Priority</label>
            <input v-model.number="filter.maxPriority" type="number" min="0" class="form-control" />
          </div>
        </div>
        <div class="mb-3">
          <label class="form-label">Sort By</label>
          <select v-model="filter.sortBy" class="form-select">
            <option value="Priority">Priority</option>
            <option value="Name">Name</option>
            <option value="Status">Status</option>
          </select>
        </div>
        <div class="mb-3 form-check">
          <input v-model="filter.descending" type="checkbox" class="form-check-input" id="task-desc-check" />
          <label class="form-check-label" for="task-desc-check">Descending</label>
        </div>
        <div class="mb-3">
          <label class="form-label">Page Size</label>
          <select v-model.number="filter.pageSize" class="form-select">
            <option :value="5">5</option>
            <option :value="10">10</option>
            <option :value="25">25</option>
            <option :value="50">50</option>
          </select>
        </div>
      </div>
      <div class="p-3 border-top d-flex flex-column gap-2">
        <button class="btn btn-primary" @click="applyFilters"><i class="bi bi-check-lg me-1"></i>Apply</button>
        <button class="btn btn-outline-secondary" @click="clearFilters"><i class="bi bi-x-lg me-1"></i>Clear</button>
      </div>
    </div>
  </Teleport>

  <nav v-if="fixedProjectId" aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
      <li class="breadcrumb-item"><RouterLink to="/projects">Projects</RouterLink></li>
      <li class="breadcrumb-item"><RouterLink :to="`/projects/${fixedProjectId}`">{{ projectName }}</RouterLink></li>
      <li class="breadcrumb-item active">Tasks</li>
    </ol>
  </nav>

  <!-- Project context banner: a clear visual cue that the list below is
       scoped to one project rather than every task in the system. -->
  <div v-if="fixedProjectId && project" class="card border-primary mb-3 pm-project-context">
    <div class="card-body py-3">
      <div class="d-flex align-items-center justify-content-between flex-wrap gap-2">
        <div class="d-flex align-items-center gap-3">
          <div class="pm-project-context-icon">
            <i class="bi bi-folder2-open"></i>
          </div>
          <div>
            <div class="small text-uppercase text-primary fw-semibold" style="letter-spacing:.08em;">
              Tasks in Project
            </div>
            <h4 class="mb-1">
              <RouterLink :to="`/projects/${fixedProjectId}`" class="text-decoration-none">
                {{ project.name }}
              </RouterLink>
            </h4>
            <div class="small text-muted">
              <i class="bi bi-person-badge me-1"></i>{{ project.projectManager.fullName }}
              <span class="mx-2">·</span>
              <i class="bi bi-calendar3 me-1"></i>{{ fmtDate(project.startDate) }} — {{ fmtDate(project.endDate) }}
            </div>
          </div>
        </div>
        <RouterLink :to="`/projects/${fixedProjectId}`" class="btn btn-sm btn-outline-primary">
          <i class="bi bi-arrow-left me-1"></i>Back to Project
        </RouterLink>
      </div>
    </div>
  </div>

  <div class="pm-page-header">
    <h2>
      <i class="bi bi-list-check"></i>
      <template v-if="fixedProjectId">Project Tasks</template>
      <template v-else>All Tasks</template>
      <span class="badge bg-secondary ms-2 fs-6">{{ total }}</span>
    </h2>
    <div class="d-flex gap-2">
      <button class="btn btn-outline-secondary" @click="filterOpen = true">
        <i class="bi bi-sliders me-1"></i>Filters
      </button>
      <RouterLink :to="createPath" class="btn btn-primary">
        <i class="bi bi-plus-lg me-1"></i>New Task
      </RouterLink>
    </div>
  </div>

  <div class="card">
    <div class="card-body p-0">
      <div v-if="loading" class="text-center py-5 text-muted">
        <div class="spinner-border spinner-border-sm me-2"></div>Loading…
      </div>
      <div v-else-if="!tasks.length" class="text-center py-5 text-muted">
        <i class="bi bi-clipboard-x d-block fs-1 mb-2 opacity-25"></i>No tasks found.
      </div>
      <div v-else class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead>
            <tr>
              <th>Task</th>
              <th v-if="!fixedProjectId">Project</th>
              <th>Assignee</th>
              <th>Author</th>
              <th>Status</th>
              <th>Priority</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in tasks" :key="t.id">
              <td>
                <RouterLink :to="`/tasks/${t.id}`" class="fw-semibold text-decoration-none">
                  {{ t.name }}
                </RouterLink>
              </td>
              <td v-if="!fixedProjectId">
                <RouterLink :to="`/projects/${t.projectId}`" class="text-decoration-none small">
                  {{ t.projectName }}
                </RouterLink>
              </td>
              <td>
                <RouterLink :to="`/employees/${t.assignee.id}`" class="text-decoration-none small">
                  {{ t.assignee.fullName }}
                </RouterLink>
              </td>
              <td class="small text-muted">{{ t.author.fullName }}</td>
              <td>
                <div class="dropdown">
                  <button class="badge border-0" :class="statusBadgeClass(t.status)" data-bs-toggle="dropdown">
                    {{ statusLabel(t.status) }}
                  </button>
                  <ul class="dropdown-menu">
                    <li><button class="dropdown-item" @click="quickStatus(t, 'ToDo')">To Do</button></li>
                    <li><button class="dropdown-item" @click="quickStatus(t, 'InProgress')">In Progress</button></li>
                    <li><button class="dropdown-item" @click="quickStatus(t, 'Done')">Done</button></li>
                  </ul>
                </div>
              </td>
              <td><span class="badge bg-secondary">{{ t.priority }}</span></td>
              <td class="text-end">
                <RouterLink :to="`/tasks/${t.id}/edit`" class="btn btn-sm btn-outline-secondary me-1">
                  <i class="bi bi-pencil"></i>
                </RouterLink>
                <button class="btn btn-sm btn-outline-danger" @click="deleteTask(t)">
                  <i class="bi bi-trash"></i>
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-if="totalPages > 1" class="card-footer d-flex align-items-center justify-content-between">
      <span class="small text-muted">
        Showing {{ (filter.page - 1) * filter.pageSize + 1 }}–{{ Math.min(filter.page * filter.pageSize, total) }} of {{ total }}
      </span>
      <nav>
        <ul class="pagination pagination-sm mb-0">
          <li class="page-item" :class="{ disabled: filter.page === 1 }">
            <button class="page-link" @click="goPage(filter.page - 1)">‹</button>
          </li>
          <li v-for="p in totalPages" :key="p" class="page-item" :class="{ active: p === filter.page }">
            <button class="page-link" @click="goPage(p)">{{ p }}</button>
          </li>
          <li class="page-item" :class="{ disabled: filter.page === totalPages }">
            <button class="page-link" @click="goPage(filter.page + 1)">›</button>
          </li>
        </ul>
      </nav>
    </div>
  </div>
</template>
