<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { tasksApi } from '@/api/tasks'
import { projectsApi } from '@/api/projects'
import { employeesApi } from '@/api/employees'
import { useNotification } from '@/stores/notification'
import AutocompleteInput from '@/components/AutocompleteInput.vue'
import type { ProjectDto, ProjectTaskStatus, EmployeeDto } from '@/types'

const route  = useRoute()
const router = useRouter()
const notif  = useNotification()

// The form supports two entry points: /projects/:id/tasks/create (preselected
// project, locked field) and /tasks/create (project picker visible).
const preselectedProjectId = computed(() => {
  const id = Number(route.params.projectId)
  return Number.isFinite(id) && id > 0 ? id : null
})

const projects = ref<ProjectDto[]>([])
const project  = ref<ProjectDto | null>(null)
const errors   = ref<string[]>([])
const saving   = ref(false)

const form = reactive({
  name: '', comment: '', priority: 0,
  status: 'ToDo' as ProjectTaskStatus,
  projectId: 0,
  authorId: 0, authorName: '',
  assigneeId: 0, assigneeName: '',
})

// The assignee autocomplete is constrained to the project's team (PM + members) —
// the domain rejects anything else, so surfacing it client-side avoids the
// round-trip and gives a friendlier UX than a 400 error.
const projectMembers = computed<EmployeeDto[]>(() => {
  if (!project.value) return []
  return [project.value.projectManager, ...project.value.employees]
})

// Search function passed to the assignee AutocompleteInput. Filters the
// project members list locally (no network call) so the field looks and
// behaves identically to the author picker.
function searchAssignees(term: string): Promise<EmployeeDto[]> {
  const q = term.trim().toLowerCase()
  const all = projectMembers.value
  const filtered = q
    ? all.filter(e => e.fullName.toLowerCase().includes(q))
    : all
  return Promise.resolve(filtered)
}

async function loadProjectDetails(id: number) {
  if (!id) { project.value = null; return }
  try { project.value = await projectsApi.getById(id) }
  catch { project.value = null }
}

async function onProjectChange() {
  // Clear the previously chosen assignee — the new project has a different team.
  form.assigneeId = 0; form.assigneeName = ''
  await loadProjectDetails(form.projectId)
}

function validate(): boolean {
  errors.value = []
  if (!form.name.trim())   errors.value.push('Task name is required.')
  if (!form.projectId)     errors.value.push('Project is required.')
  if (!form.authorId)      errors.value.push('Author is required.')
  if (!form.assigneeId)    errors.value.push('Assignee is required.')
  if (form.priority < 0)   errors.value.push('Priority must be non-negative.')
  return !errors.value.length
}

async function submit() {
  if (!validate()) return
  saving.value = true
  try {
    const result = await tasksApi.create({
      name:       form.name,
      comment:    form.comment || undefined,
      priority:   form.priority,
      status:     form.status,
      projectId:  form.projectId,
      authorId:   form.authorId,
      assigneeId: form.assigneeId,
    })
    notif.show(`Task «${form.name}» created.`)
    router.push(`/tasks/${result.id}`)
  } catch (e: any) { notif.show(e.message, 'error') }
  finally { saving.value = false }
}

onMounted(async () => {
  // Pull a generous page of projects for the selector. 100 covers any
  // realistic catalog; beyond that the user should be opening a project first.
  try {
    const r = await projectsApi.list({ pageSize: 100, sortBy: 'Name' })
    projects.value = r.items
  } catch { projects.value = [] }

  if (preselectedProjectId.value) {
    form.projectId = preselectedProjectId.value
    await loadProjectDetails(form.projectId)
  }
})
</script>

<template>
  <nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
      <li class="breadcrumb-item"><RouterLink to="/tasks">Tasks</RouterLink></li>
      <li class="breadcrumb-item active">New Task</li>
    </ol>
  </nav>

  <div class="row justify-content-center">
    <div class="col-lg-8">
      <div class="card">
        <div class="card-header"><i class="bi bi-plus-circle me-2"></i>Create New Task</div>
        <div class="card-body">
          <div v-if="errors.length" class="alert alert-warning py-2 mb-3">
            <div v-for="e in errors" :key="e">{{ e }}</div>
          </div>

          <div class="row g-3">
            <div class="col-12">
              <label class="form-label">Project *</label>
              <select v-model.number="form.projectId" class="form-select"
                      :disabled="preselectedProjectId !== null"
                      @change="onProjectChange">
                <option :value="0" disabled>Select a project…</option>
                <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }}</option>
              </select>
            </div>

            <div class="col-12">
              <label class="form-label">Task Name *</label>
              <input v-model="form.name" type="text" class="form-control" placeholder="e.g. Migrate to .NET 10" />
            </div>

            <div class="col-md-4">
              <label class="form-label">Priority</label>
              <input v-model.number="form.priority" type="number" min="0" class="form-control" />
            </div>

            <div class="col-md-4">
              <label class="form-label">Status</label>
              <select v-model="form.status" class="form-select">
                <option value="ToDo">To Do</option>
                <option value="InProgress">In Progress</option>
                <option value="Done">Done</option>
              </select>
            </div>

            <div class="col-12">
              <label class="form-label">Author *</label>
              <AutocompleteInput
                v-model="form.authorId"
                v-model:modelName="form.authorName"
                :searchFn="(t) => employeesApi.search(t, 10)"
              />
            </div>

            <div class="col-12">
              <label class="form-label">Assignee *</label>
              <p v-if="!form.projectId" class="text-muted small mb-1">Select a project first.</p>
              <AutocompleteInput
                v-else
                :key="form.projectId"
                v-model="form.assigneeId"
                v-model:modelName="form.assigneeName"
                :searchFn="searchAssignees"
                placeholder="Search team members…"
              />
              <div class="form-text">Assignee must be a member of the selected project.</div>
            </div>

            <div class="col-12">
              <label class="form-label">Comment</label>
              <textarea v-model="form.comment" rows="3" class="form-control"
                        placeholder="Optional notes about the task…"></textarea>
            </div>
          </div>

          <div class="d-flex gap-2 mt-4">
            <button type="button" class="btn btn-primary" :disabled="saving" @click="submit">
              <span v-if="saving" class="spinner-border spinner-border-sm me-1"></span>
              <i v-else class="bi bi-check-lg me-1"></i>Create Task
            </button>
            <RouterLink to="/tasks" class="btn btn-outline-secondary">Cancel</RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
