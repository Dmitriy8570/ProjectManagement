<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { tasksApi } from '@/api/tasks'
import { projectsApi } from '@/api/projects'
import { useNotification } from '@/stores/notification'
import type { ProjectDto, ProjectTaskStatus, EmployeeDto } from '@/types'

const route  = useRoute()
const router = useRouter()
const notif  = useNotification()
const id     = Number(route.params.id)

const name       = ref('')
const comment    = ref('')
const priority   = ref(0)
const status     = ref<ProjectTaskStatus>('ToDo')
const assigneeId = ref(0)
const taskName   = ref('')
const projectId  = ref(0)
const project    = ref<ProjectDto | null>(null)
const saving     = ref(false)
const errors     = ref<string[]>([])

// The assignee dropdown is constrained to the task's project members; the
// server enforces the same invariant.
const projectMembers = computed<EmployeeDto[]>(() => {
  if (!project.value) return []
  return [project.value.projectManager, ...project.value.employees]
})

async function load() {
  try {
    const t = await tasksApi.getById(id)
    taskName.value   = t.name
    name.value       = t.name
    comment.value    = t.comment
    priority.value   = t.priority
    status.value     = t.status
    assigneeId.value = t.assignee.id
    projectId.value  = t.projectId
    project.value    = await projectsApi.getById(t.projectId)
  } catch { router.push('/tasks') }
}

function validate(): boolean {
  errors.value = []
  if (!name.value.trim()) errors.value.push('Task name is required.')
  if (!assigneeId.value)  errors.value.push('Assignee is required.')
  if (priority.value < 0) errors.value.push('Priority must be non-negative.')
  return !errors.value.length
}

async function save() {
  if (!validate()) return
  saving.value = true
  try {
    await tasksApi.update(id, {
      name: name.value,
      comment: comment.value,
      priority: priority.value,
      status: status.value,
      assigneeId: assigneeId.value,
    })
    notif.show('Task updated.')
    router.push(`/tasks/${id}`)
  } catch (e: any) { notif.show(e.message, 'error') }
  finally { saving.value = false }
}

onMounted(load)
</script>

<template>
  <nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
      <li class="breadcrumb-item"><RouterLink to="/tasks">Tasks</RouterLink></li>
      <li v-if="projectId" class="breadcrumb-item">
        <RouterLink :to="`/projects/${projectId}/tasks`">Project Tasks</RouterLink>
      </li>
      <li class="breadcrumb-item"><RouterLink :to="`/tasks/${id}`">{{ taskName }}</RouterLink></li>
      <li class="breadcrumb-item active">Edit</li>
    </ol>
  </nav>

  <div class="row justify-content-center">
    <div class="col-lg-8">
      <div class="card">
        <div class="card-header"><i class="bi bi-pencil-square me-2"></i>Edit Task</div>
        <div class="card-body">
          <div v-if="errors.length" class="alert alert-danger mb-3">
            <div v-for="e in errors" :key="e">{{ e }}</div>
          </div>

          <div v-if="project" class="mb-3 small text-muted">
            <i class="bi bi-folder2 me-1"></i>Project:
            <RouterLink :to="`/projects/${projectId}`" class="text-decoration-none">
              {{ project.name }}
            </RouterLink>
          </div>

          <div class="row g-3">
            <div class="col-12">
              <label class="form-label">Task Name *</label>
              <input v-model="name" type="text" class="form-control" />
            </div>
            <div class="col-md-4">
              <label class="form-label">Priority</label>
              <input v-model.number="priority" type="number" min="0" class="form-control" />
            </div>
            <div class="col-md-4">
              <label class="form-label">Status</label>
              <select v-model="status" class="form-select">
                <option value="ToDo">To Do</option>
                <option value="InProgress">In Progress</option>
                <option value="Done">Done</option>
              </select>
            </div>
            <div class="col-12">
              <label class="form-label">Assignee *</label>
              <select v-model.number="assigneeId" class="form-select">
                <option :value="0" disabled>Select a team member…</option>
                <option v-for="e in projectMembers" :key="e.id" :value="e.id">
                  {{ e.fullName }}
                </option>
              </select>
              <div class="form-text">Assignee must be a member of the project.</div>
            </div>
            <div class="col-12">
              <label class="form-label">Comment</label>
              <textarea v-model="comment" rows="3" class="form-control"></textarea>
            </div>
          </div>

          <div class="d-flex gap-2 mt-4">
            <button type="button" class="btn btn-primary" :disabled="saving" @click="save">
              <span v-if="saving" class="spinner-border spinner-border-sm me-1"></span>
              <i v-else class="bi bi-check-lg me-1"></i>Save Changes
            </button>
            <RouterLink :to="`/tasks/${id}`" class="btn btn-outline-secondary">Cancel</RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
