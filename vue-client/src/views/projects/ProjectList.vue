<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { projectsApi } from '@/api/projects'
import { useNotification } from '@/stores/notification'
import { useAuth } from '@/stores/auth'
import { Roles, type ProjectDto } from '@/types'

const router = useRouter()
const notif  = useNotification()
const auth   = useAuth()

// Hide everything that requires write rights this user doesn't have. The API
// re-enforces the same rules, so even a hand-crafted request can't slip
// past — these are pure UX trims.
const isDirector = computed(() => auth.hasRole(Roles.Director))
const isPm       = computed(() => auth.hasRole(Roles.ProjectManager))
function canEditRow(p: ProjectDto) {
  return isDirector.value || (isPm.value && p.projectManager.id === auth.employeeId)
}

const projects   = ref<ProjectDto[]>([])
const total      = ref(0)
const loading    = ref(false)
const filterOpen = ref(false)

const filter = reactive({
  nameSearch: '', startDateFrom: '', startDateTo: '',
  minPriority: null as number | null, maxPriority: null as number | null,
  sortBy: 'Name', descending: false, page: 1, pageSize: 10,
})

const totalPages = computed(() => Math.ceil(total.value / filter.pageSize) || 1)

async function load() {
  loading.value = true
  try {
    const r = await projectsApi.list({
      nameSearch:    filter.nameSearch   || undefined,
      startDateFrom: filter.startDateFrom || undefined,
      startDateTo:   filter.startDateTo   || undefined,
      minPriority:   filter.minPriority   ?? undefined,
      maxPriority:   filter.maxPriority   ?? undefined,
      sortBy:        filter.sortBy,
      descending:    filter.descending,
      page:          filter.page,
      pageSize:      filter.pageSize,
    })
    projects.value = r.items
    total.value    = r.totalCount
  } catch (e: any) { notif.show(e.message, 'error') }
  finally { loading.value = false }
}

function applyFilters() { filter.page = 1; filterOpen.value = false; load() }
function clearFilters()  {
  Object.assign(filter, { nameSearch: '', startDateFrom: '', startDateTo: '', minPriority: null, maxPriority: null, sortBy: 'Name', descending: false, page: 1 })
  filterOpen.value = false; load()
}

function goPage(p: number) { filter.page = p; load() }

async function deleteProject(p: ProjectDto) {
  if (!confirm(`Delete project «${p.name}»? This cannot be undone.`)) return
  try {
    await projectsApi.delete(p.id)
    notif.show('Project deleted.')
    load()
  } catch (e: any) { notif.show(e.message, 'error') }
}

function fmtDate(s: string) {
  if (!s) return '—'
  const d = new Date(s)
  return d.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

// Quick search autocomplete
const searchTerm    = ref('')
const searchResults = ref<ProjectDto[]>([])
const showSearch    = ref(false)
let   searchTimer   = 0

async function onSearchInput() {
  clearTimeout(searchTimer)
  searchTimer = window.setTimeout(async () => {
    if (!searchTerm.value.trim()) { showSearch.value = false; return }
    try {
      const r = await projectsApi.list({ nameSearch: searchTerm.value, pageSize: 8, sortBy: 'Name' })
      searchResults.value = r.items
      showSearch.value = true
    } catch { showSearch.value = false }
  }, 220)
}
function onSearchFocus() { if (searchTerm.value) onSearchInput() }
function onSearchBlur()  { setTimeout(() => { showSearch.value = false }, 180) }
function goToProject(id: number) { searchTerm.value = ''; showSearch.value = false; router.push(`/projects/${id}`) }

function doSearch() {
  showSearch.value = false
  filter.nameSearch = searchTerm.value.trim()
  filter.page = 1
  load()
}

onMounted(load)
</script>

<template>
  <!-- Filter backdrop + drawer -->
  <Teleport to="body">
    <div v-if="filterOpen" class="filter-backdrop" @click="filterOpen = false"></div>
    <div class="filter-drawer" :class="{ open: filterOpen }">
      <div class="d-flex align-items-center justify-content-between p-3 border-bottom">
        <h6 class="mb-0 fw-semibold"><i class="bi bi-sliders me-2"></i>Filters</h6>
        <button type="button" class="btn-close" @click="filterOpen = false"></button>
      </div>
      <div class="p-3 flex-1 overflow-y-auto" style="overflow-y:auto;">
        <div class="mb-3">
          <label class="form-label">Project Name</label>
          <input v-model="filter.nameSearch" type="text" class="form-control" placeholder="Search…" />
        </div>
        <div class="mb-3">
          <label class="form-label">Start Date From</label>
          <input v-model="filter.startDateFrom" type="date" class="form-control" />
        </div>
        <div class="mb-3">
          <label class="form-label">Start Date To</label>
          <input v-model="filter.startDateTo" type="date" class="form-control" />
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
            <option value="Name">Name</option>
            <option value="StartDate">Start Date</option>
            <option value="EndDate">End Date</option>
            <option value="Priority">Priority</option>
          </select>
        </div>
        <div class="mb-3 form-check">
          <input v-model="filter.descending" type="checkbox" class="form-check-input" id="desc-check" />
          <label class="form-check-label" for="desc-check">Descending</label>
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

  <div class="pm-page-header">
    <h2><i class="bi bi-folder2-open"></i>Projects
      <span class="badge bg-secondary ms-2 fs-6">{{ total }}</span>
    </h2>
    <div class="d-flex gap-2">
      <button class="btn btn-outline-secondary" @click="filterOpen = true">
        <i class="bi bi-sliders me-1"></i>Filters
      </button>
      <RouterLink v-if="isDirector" to="/projects/create" class="btn btn-primary">
        <i class="bi bi-plus-lg me-1"></i>New Project
      </RouterLink>
    </div>
  </div>

  <!-- Quick search -->
  <div class="card mb-3">
    <div class="card-body py-2">
      <div class="d-flex gap-2" style="max-width:500px;">
        <div class="autocomplete-wrapper flex-grow-1">
          <input
            v-model="searchTerm"
            type="text"
            class="form-control"
            placeholder="Search projects by name…"
            @input="onSearchInput"
            @focus="onSearchFocus"
            @blur="onSearchBlur"
            @keydown.enter="doSearch"
          />
          <div v-if="showSearch" class="autocomplete-dropdown" style="display:block;">
            <div v-if="!searchResults.length" class="autocomplete-item no-results">No projects found</div>
            <div
              v-for="p in searchResults"
              :key="p.id"
              class="autocomplete-item"
              @mousedown.prevent="goToProject(p.id)"
            >{{ p.name }}</div>
          </div>
        </div>
        <button class="btn btn-primary" @click="doSearch">
          <i class="bi bi-search me-1"></i>Search
        </button>
      </div>
    </div>
  </div>

  <!-- Table -->
  <div class="card">
    <div class="card-body p-0">
      <div v-if="loading" class="text-center py-5 text-muted">
        <div class="spinner-border spinner-border-sm me-2"></div>Loading…
      </div>
      <div v-else-if="!projects.length" class="text-center py-5 text-muted">
        <i class="bi bi-folder-x d-block fs-1 mb-2 opacity-25"></i>No projects found.
      </div>
      <div v-else class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead>
            <tr>
              <th>Name</th>
              <th>Customer</th>
              <th>Manager</th>
              <th>Dates</th>
              <th>Priority</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in projects" :key="p.id">
              <td>
                <RouterLink :to="`/projects/${p.id}`" class="fw-semibold text-decoration-none">
                  {{ p.name }}
                </RouterLink>
              </td>
              <td class="text-muted">{{ p.customerCompany }}</td>
              <td>
                <RouterLink :to="`/employees/${p.projectManager.id}`" class="text-decoration-none small">
                  {{ p.projectManager.fullName }}
                </RouterLink>
              </td>
              <td class="small text-muted">{{ fmtDate(p.startDate) }} — {{ fmtDate(p.endDate) }}</td>
              <td><span class="badge bg-secondary">{{ p.priority }}</span></td>
              <td class="text-end">
                <RouterLink
                  v-if="canEditRow(p)"
                  :to="`/projects/${p.id}/edit`"
                  class="btn btn-sm btn-outline-secondary me-1">
                  <i class="bi bi-pencil"></i>
                </RouterLink>
                <button
                  v-if="isDirector"
                  class="btn btn-sm btn-outline-danger"
                  @click="deleteProject(p)">
                  <i class="bi bi-trash"></i>
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Pagination -->
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
