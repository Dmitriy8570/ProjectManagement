<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { tasksApi } from '@/api/tasks'
import { useNotification } from '@/stores/notification'
import type { ProjectTaskDto, ProjectTaskStatus } from '@/types'

const route  = useRoute()
const router = useRouter()
const notif  = useNotification()
const id     = Number(route.params.id)

const task    = ref<ProjectTaskDto | null>(null)
const loading = ref(true)

async function load() {
  loading.value = true
  try { task.value = await tasksApi.getById(id) }
  catch { router.push('/tasks') }
  finally { loading.value = false }
}

async function del() {
  if (!task.value) return
  if (!confirm(`Delete task «${task.value.name}»? This cannot be undone.`)) return
  try { await tasksApi.delete(id); notif.show('Task deleted.'); router.push('/tasks') }
  catch (e: any) { notif.show(e.message, 'error') }
}

async function setStatus(status: ProjectTaskStatus) {
  if (!task.value) return
  try { await tasksApi.changeStatus(id, status); task.value.status = status; notif.show('Status updated.') }
  catch (e: any) { notif.show(e.message, 'error') }
}

function statusBadgeClass(s: ProjectTaskStatus) {
  return s === 'Done' ? 'bg-success'
       : s === 'InProgress' ? 'bg-primary'
       : 'bg-secondary'
}

function statusLabel(s: ProjectTaskStatus) {
  return s === 'Done' ? 'Done'
       : s === 'InProgress' ? 'In Progress'
       : 'To Do'
}

onMounted(load)
</script>

<template>
  <div v-if="loading" class="text-center py-5 text-muted">
    <div class="spinner-border"></div>
  </div>
  <template v-else-if="task">
    <nav aria-label="breadcrumb" class="mb-3">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><RouterLink to="/tasks">Tasks</RouterLink></li>
        <li class="breadcrumb-item">
          <RouterLink :to="`/projects/${task.projectId}`">{{ task.projectName }}</RouterLink>
        </li>
        <li class="breadcrumb-item active">{{ task.name }}</li>
      </ol>
    </nav>

    <div class="pm-page-header">
      <h2>{{ task.name }}</h2>
      <div class="d-flex gap-2">
        <RouterLink :to="`/tasks/${id}/edit`" class="btn btn-outline-secondary">
          <i class="bi bi-pencil me-1"></i>Edit
        </RouterLink>
        <button class="btn btn-outline-danger" @click="del">
          <i class="bi bi-trash me-1"></i>Delete
        </button>
      </div>
    </div>

    <div class="row g-4">
      <div class="col-lg-8">
        <div class="card h-100">
          <div class="card-header"><i class="bi bi-info-circle me-2"></i>Task Details</div>
          <div class="card-body">
            <dl class="row mb-0">
              <dt class="col-sm-4 text-muted">Project</dt>
              <dd class="col-sm-8">
                <RouterLink :to="`/projects/${task.projectId}`" class="text-decoration-none">
                  <i class="bi bi-folder2 me-1"></i>{{ task.projectName }}
                </RouterLink>
              </dd>
              <dt class="col-sm-4 text-muted">Status</dt>
              <dd class="col-sm-8">
                <span class="badge" :class="statusBadgeClass(task.status)">{{ statusLabel(task.status) }}</span>
              </dd>
              <dt class="col-sm-4 text-muted">Priority</dt>
              <dd class="col-sm-8"><span class="badge bg-secondary fs-6">{{ task.priority }}</span></dd>
              <dt class="col-sm-4 text-muted">Author</dt>
              <dd class="col-sm-8">
                <RouterLink :to="`/employees/${task.author.id}`" class="text-decoration-none">
                  <i class="bi bi-person me-1"></i>{{ task.author.fullName }}
                </RouterLink>
              </dd>
              <dt class="col-sm-4 text-muted">Assignee</dt>
              <dd class="col-sm-8">
                <RouterLink :to="`/employees/${task.assignee.id}`" class="text-decoration-none">
                  <i class="bi bi-person-badge me-1"></i>{{ task.assignee.fullName }}
                </RouterLink>
              </dd>
              <dt class="col-sm-4 text-muted">Comment</dt>
              <dd class="col-sm-8">
                <span v-if="task.comment" style="white-space:pre-wrap;">{{ task.comment }}</span>
                <span v-else class="text-muted small">—</span>
              </dd>
            </dl>
          </div>
        </div>
      </div>

      <div class="col-lg-4">
        <div class="card h-100">
          <div class="card-header"><i class="bi bi-arrow-repeat me-2"></i>Change Status</div>
          <div class="card-body d-flex flex-column gap-2">
            <button class="btn btn-outline-secondary" :disabled="task.status === 'ToDo'" @click="setStatus('ToDo')">
              <i class="bi bi-circle me-1"></i>To Do
            </button>
            <button class="btn btn-outline-primary" :disabled="task.status === 'InProgress'" @click="setStatus('InProgress')">
              <i class="bi bi-play-circle me-1"></i>In Progress
            </button>
            <button class="btn btn-outline-success" :disabled="task.status === 'Done'" @click="setStatus('Done')">
              <i class="bi bi-check-circle me-1"></i>Done
            </button>
          </div>
        </div>
      </div>
    </div>
  </template>
</template>
