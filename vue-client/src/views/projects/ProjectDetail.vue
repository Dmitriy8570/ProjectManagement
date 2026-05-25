<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { projectsApi } from '@/api/projects'
import { employeesApi } from '@/api/employees'
import { documentsApi } from '@/api/documents'
import { useNotification } from '@/stores/notification'
import type { ProjectDto, EmployeeDto, ProjectDocumentDto } from '@/types'
import FileDropZone from '@/components/FileDropZone.vue'

const route  = useRoute()
const router = useRouter()
const notif  = useNotification()
const id     = Number(route.params.id)

const project   = ref<ProjectDto | null>(null)
const documents = ref<ProjectDocumentDto[]>([])
const loading   = ref(true)
const uploading = ref(false)

// Team assign autocomplete
const assignTerm    = ref('')
const assignResults = ref<EmployeeDto[]>([])
const assignShow    = ref(false)
const assignSel     = ref<EmployeeDto | null>(null)
const assigning     = ref(false)
let   assignTimer   = 0

async function load() {
  loading.value = true
  try {
    const [p, docs] = await Promise.all([
      projectsApi.getById(id),
      documentsApi.list(id),
    ])
    project.value   = p
    documents.value = docs
  } catch { router.push('/projects') }
  finally { loading.value = false }
}

async function del() {
  if (!project.value) return
  if (!confirm(`Delete project «${project.value.name}»? This cannot be undone.`)) return
  try { await projectsApi.delete(id); notif.show('Project deleted.'); router.push('/projects') }
  catch (e: any) { notif.show(e.message, 'error') }
}

async function unassign(empId: number) {
  if (!confirm('Remove this employee from the project?')) return
  try { await projectsApi.unassign(id, empId); notif.show('Employee removed.'); load() }
  catch (e: any) { notif.show(e.message, 'error') }
}

async function searchAssign(term: string) {
  try { assignResults.value = await employeesApi.search(term, 10); assignShow.value = true }
  catch { assignShow.value = false }
}
function onAssignFocus() { searchAssign(assignTerm.value) }
function onAssignInput() { clearTimeout(assignTimer); assignTimer = window.setTimeout(() => searchAssign(assignTerm.value), 220) }
function onAssignBlur()  { setTimeout(() => { assignShow.value = false }, 180) }
function selectAssign(e: EmployeeDto) { assignSel.value = e; assignTerm.value = ''; assignShow.value = false }
function clearAssign() { assignSel.value = null }
async function doAssign() {
  if (!assignSel.value) return
  assigning.value = true
  try { await projectsApi.assign(id, assignSel.value.id); notif.show(`${assignSel.value.fullName} assigned.`); assignSel.value = null; load() }
  catch (e: any) { notif.show(e.message, 'error') }
  finally { assigning.value = false }
}

// Documents
const pendingFiles = ref<File[]>([])

async function uploadDocs() {
  if (!pendingFiles.value.length) return
  uploading.value = true
  let ok = 0
  const errors: string[] = []
  for (const f of pendingFiles.value) {
    try { await documentsApi.upload(id, f); ok++ }
    catch (e: any) { errors.push(`${f.name}: ${e.message}`) }
  }
  if (ok) notif.show(`${ok} file${ok > 1 ? 's' : ''} uploaded.`)
  if (errors.length) notif.show(errors.join(' | '), 'error')
  pendingFiles.value = []
  await documentsApi.list(id).then(d => { documents.value = d })
  uploading.value = false
}

async function deleteDoc(doc: ProjectDocumentDto) {
  if (!confirm(`Delete «${doc.fileName}»?`)) return
  try { await documentsApi.delete(doc.id); notif.show('Document deleted.'); documents.value = documents.value.filter(d => d.id !== doc.id) }
  catch (e: any) { notif.show(e.message, 'error') }
}

function fmtDate(s: string) {
  if (!s) return '—'
  const d = new Date(s)
  return `${String(d.getDate()).padStart(2,'0')}.${String(d.getMonth()+1).padStart(2,'0')}.${d.getFullYear()}`
}
function fmtKb(b: number) { return (b / 1024).toFixed(1) }

onMounted(load)
</script>

<template>
  <div v-if="loading" class="text-center py-5 text-muted">
    <div class="spinner-border"></div>
  </div>
  <template v-else-if="project">
    <nav aria-label="breadcrumb" class="mb-3">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/projects">Projects</RouterLink></li>
        <li class="breadcrumb-item active">{{ project.name }}</li>
      </ol>
    </nav>

    <div class="pm-page-header">
      <h2>{{ project.name }}</h2>
      <div class="d-flex gap-2">
        <RouterLink :to="`/projects/${id}/tasks`" class="btn btn-outline-primary">
          <i class="bi bi-list-check me-1"></i>Tasks
        </RouterLink>
        <RouterLink :to="`/projects/${id}/edit`" class="btn btn-outline-secondary">
          <i class="bi bi-pencil me-1"></i>Edit
        </RouterLink>
        <button class="btn btn-outline-danger" @click="del">
          <i class="bi bi-trash me-1"></i>Delete
        </button>
      </div>
    </div>

    <div class="row g-4">
      <!-- Info -->
      <div class="col-lg-7">
        <div class="card h-100">
          <div class="card-header"><i class="bi bi-info-circle me-2"></i>Project Details</div>
          <div class="card-body">
            <dl class="row mb-0">
              <dt class="col-sm-4 text-muted">Customer</dt>
              <dd class="col-sm-8">{{ project.customerCompany }}</dd>
              <dt class="col-sm-4 text-muted">Executor</dt>
              <dd class="col-sm-8">{{ project.executingCompany }}</dd>
              <dt class="col-sm-4 text-muted">Start Date</dt>
              <dd class="col-sm-8">{{ fmtDate(project.startDate) }}</dd>
              <dt class="col-sm-4 text-muted">End Date</dt>
              <dd class="col-sm-8">{{ fmtDate(project.endDate) }}</dd>
              <dt class="col-sm-4 text-muted">Priority</dt>
              <dd class="col-sm-8"><span class="badge bg-secondary fs-6">{{ project.priority }}</span></dd>
              <dt class="col-sm-4 text-muted">Manager</dt>
              <dd class="col-sm-8">
                <RouterLink :to="`/employees/${project.projectManager.id}`" class="text-decoration-none">
                  <i class="bi bi-person-badge me-1"></i>{{ project.projectManager.fullName }}
                </RouterLink>
              </dd>
            </dl>
          </div>
        </div>
      </div>

      <!-- Team -->
      <div class="col-lg-5">
        <div class="card h-100">
          <div class="card-header d-flex align-items-center justify-content-between">
            <span><i class="bi bi-people me-2"></i>Team Members</span>
            <span class="badge bg-secondary">{{ project.employees.length }}</span>
          </div>
          <div class="card-body">
            <ul v-if="project.employees.length" class="list-group list-group-flush mb-3">
              <li v-for="emp in project.employees" :key="emp.id"
                  class="list-group-item d-flex justify-content-between align-items-center px-0">
                <RouterLink :to="`/employees/${emp.id}`" class="text-decoration-none">
                  <i class="bi bi-person me-2 text-muted"></i>{{ emp.fullName }}
                </RouterLink>
                <button class="btn btn-sm btn-link text-danger p-0" @click="unassign(emp.id)">
                  <i class="bi bi-x-circle"></i>
                </button>
              </li>
            </ul>
            <p v-else class="text-muted small mb-3">No team members yet.</p>

            <!-- Assign -->
            <div>
              <label class="form-label small text-muted">Add Employee</label>
              <div class="autocomplete-wrapper">
                <input
                  v-model="assignTerm"
                  type="text"
                  class="form-control form-control-sm"
                  placeholder="Search by name…"
                  autocomplete="off"
                  @focus="onAssignFocus"
                  @input="onAssignInput"
                  @blur="onAssignBlur"
                />
                <div v-if="assignShow" class="autocomplete-dropdown" style="display:block;">
                  <div v-if="!assignResults.length" class="autocomplete-item no-results">No employees found</div>
                  <div
                    v-for="e in assignResults" :key="e.id"
                    class="autocomplete-item"
                    @mousedown.prevent="selectAssign(e)"
                  >{{ e.fullName }}</div>
                </div>
              </div>
              <div v-if="assignSel" class="d-flex align-items-center gap-2 mt-2">
                <span class="badge bg-info text-dark">{{ assignSel.fullName }}</span>
                <button type="button" class="btn btn-link btn-sm text-muted p-0" @click="clearAssign">remove</button>
              </div>
              <button
                class="btn btn-sm btn-primary mt-2"
                :disabled="!assignSel || assigning"
                @click="doAssign"
              >
                <i class="bi bi-person-plus me-1"></i>Assign
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Documents -->
    <div class="card mt-4">
      <div class="card-header d-flex align-items-center justify-content-between">
        <span><i class="bi bi-file-earmark-text me-2"></i>Documents</span>
        <span class="badge bg-secondary">{{ documents.length }}</span>
      </div>
      <div class="card-body">
        <ul v-if="documents.length" class="list-group list-group-flush mb-3">
          <li v-for="doc in documents" :key="doc.id"
              class="list-group-item d-flex justify-content-between align-items-center px-0">
            <div>
              <i class="bi bi-file-earmark me-2 text-muted"></i>
              <a :href="documentsApi.downloadUrl(doc.id)" target="_blank" class="text-decoration-none">
                {{ doc.fileName }}
              </a>
              <span class="text-muted small ms-2">
                ({{ fmtKb(doc.sizeBytes) }} KB)
              </span>
            </div>
            <button class="btn btn-sm btn-link text-danger p-0" @click="deleteDoc(doc)">
              <i class="bi bi-trash"></i>
            </button>
          </li>
        </ul>
        <p v-else class="text-muted small mb-3">No documents uploaded yet.</p>

        <FileDropZone @update:files="pendingFiles = $event" />

        <div v-if="pendingFiles.length" class="mt-3">
          <button class="btn btn-sm btn-primary" :disabled="uploading" @click="uploadDocs">
            <span v-if="uploading" class="spinner-border spinner-border-sm me-1"></span>
            <i v-else class="bi bi-upload me-1"></i>Upload {{ pendingFiles.length }} file{{ pendingFiles.length > 1 ? 's' : '' }}
          </button>
        </div>
      </div>
    </div>
  </template>
</template>
