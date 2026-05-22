<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { projectsApi } from '@/api/projects'
import { employeesApi } from '@/api/employees'
import { useNotification } from '@/stores/notification'
import AutocompleteInput from '@/components/AutocompleteInput.vue'

const route  = useRoute()
const router = useRouter()
const notif  = useNotification()
const id     = Number(route.params.id)

const name             = ref('')
const customerCompany  = ref('')
const executingCompany = ref('')
const startDate        = ref('')
const endDate          = ref('')
const priority         = ref(0)
const pmId             = ref(0)
const pmName           = ref('')
const projectName      = ref('')
const saving           = ref(false)
const errors           = ref<string[]>([])

async function load() {
  try {
    const p = await projectsApi.getById(id)
    projectName.value      = p.name
    name.value             = p.name
    customerCompany.value  = p.customerCompany
    executingCompany.value = p.executingCompany
    startDate.value        = p.startDate.slice(0, 10)
    endDate.value          = p.endDate.slice(0, 10)
    priority.value         = p.priority
    pmId.value             = p.projectManager.id
    pmName.value           = p.projectManager.fullName
  } catch { router.push('/projects') }
}

function validate(): boolean {
  errors.value = []
  if (!name.value.trim())              errors.value.push('Project name is required.')
  if (!customerCompany.value.trim())   errors.value.push('Customer company is required.')
  if (!executingCompany.value.trim())  errors.value.push('Executing company is required.')
  if (!startDate.value)                errors.value.push('Start date is required.')
  if (!endDate.value)                  errors.value.push('End date is required.')
  if (endDate.value <= startDate.value) errors.value.push('End date must be after start date.')
  if (!pmId.value)                     errors.value.push('Project manager is required.')
  return !errors.value.length
}

async function save() {
  if (!validate()) return
  saving.value = true
  try {
    await projectsApi.update(id, {
      name: name.value, customerCompany: customerCompany.value,
      executingCompany: executingCompany.value, startDate: startDate.value,
      endDate: endDate.value, projectManagerId: pmId.value, priority: priority.value,
    })
    notif.show('Project updated.')
    router.push(`/projects/${id}`)
  } catch (e: any) { notif.show(e.message, 'error') }
  finally { saving.value = false }
}

onMounted(load)
</script>

<template>
  <nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
      <li class="breadcrumb-item"><RouterLink to="/projects">Projects</RouterLink></li>
      <li class="breadcrumb-item"><RouterLink :to="`/projects/${id}`">{{ projectName }}</RouterLink></li>
      <li class="breadcrumb-item active">Edit</li>
    </ol>
  </nav>

  <div class="row justify-content-center">
    <div class="col-lg-8">
      <div class="card">
        <div class="card-header"><i class="bi bi-pencil-square me-2"></i>Edit Project</div>
        <div class="card-body">
          <div v-if="errors.length" class="alert alert-danger mb-3">
            <div v-for="e in errors" :key="e">{{ e }}</div>
          </div>

          <div class="row g-3">
            <div class="col-12">
              <label class="form-label">Project Name *</label>
              <input v-model="name" type="text" class="form-control" />
            </div>
            <div class="col-md-6">
              <label class="form-label">Start Date *</label>
              <input v-model="startDate" type="date" class="form-control" />
            </div>
            <div class="col-md-6">
              <label class="form-label">End Date *</label>
              <input v-model="endDate" type="date" class="form-control" />
            </div>
            <div class="col-md-4">
              <label class="form-label">Priority</label>
              <input v-model.number="priority" type="number" min="0" class="form-control" />
            </div>
            <div class="col-md-6">
              <label class="form-label">Customer Company *</label>
              <input v-model="customerCompany" type="text" class="form-control" />
            </div>
            <div class="col-md-6">
              <label class="form-label">Executing Company *</label>
              <input v-model="executingCompany" type="text" class="form-control" />
            </div>
            <div class="col-12">
              <label class="form-label">Project Manager *</label>
              <AutocompleteInput
                v-model="pmId"
                v-model:modelName="pmName"
                :searchFn="(t) => employeesApi.search(t, 10)"
              />
            </div>
          </div>

          <div class="d-flex gap-2 mt-4">
            <button type="button" class="btn btn-primary" :disabled="saving" @click="save">
              <span v-if="saving" class="spinner-border spinner-border-sm me-1"></span>
              <i v-else class="bi bi-check-lg me-1"></i>Save Changes
            </button>
            <RouterLink :to="`/projects/${id}`" class="btn btn-outline-secondary">Cancel</RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
