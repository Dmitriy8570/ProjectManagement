<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { projectsApi } from '@/api/projects'
import { employeesApi } from '@/api/employees'
import { documentsApi } from '@/api/documents'
import { useNotification } from '@/stores/notification'
import AutocompleteInput from '@/components/AutocompleteInput.vue'
import FileDropZone from '@/components/FileDropZone.vue'
import type { EmployeeDto } from '@/types'

const router = useRouter()
const notif  = useNotification()

const step    = ref(1)
const total   = 5
const stepErr = ref('')
const saving  = ref(false)

const form = reactive({
  name: '', startDate: new Date().toISOString().slice(0,10),
  endDate: new Date(Date.now() + 90*864e5).toISOString().slice(0,10),
  priority: 0,
  customerCompany: '', executingCompany: '',
  projectManagerId: 0, projectManagerName: '',
  employeeIds: [] as number[],
  files: [] as File[],
})

const teamChips  = ref<EmployeeDto[]>([])
let   teamTimer  = 0
const teamTerm   = ref('')
const teamResults= ref<EmployeeDto[]>([])
const teamShow   = ref(false)

async function searchTeam(q: string) {
  try {
    const all = await employeesApi.search(q, 10)
    teamResults.value = all.filter(e => e.id !== form.projectManagerId && !form.employeeIds.includes(e.id))
    teamShow.value = true
  } catch { teamShow.value = false }
}
function onTeamFocus() { searchTeam(teamTerm.value) }
function onTeamInput() { clearTimeout(teamTimer); teamTimer = window.setTimeout(() => searchTeam(teamTerm.value), 220) }
function onTeamBlur()  { setTimeout(() => { teamShow.value = false }, 180) }
function addTeam(e: EmployeeDto) {
  form.employeeIds.push(e.id)
  teamChips.value.push(e)
  teamTerm.value = ''; teamShow.value = false
}
function removeTeam(id: number) {
  form.employeeIds = form.employeeIds.filter(x => x !== id)
  teamChips.value  = teamChips.value.filter(e => e.id !== id)
}

function validate(): boolean {
  stepErr.value = ''
  if (step.value === 1) {
    if (!form.name.trim())  { stepErr.value = 'Project name is required.'; return false }
    if (!form.startDate)    { stepErr.value = 'Start date is required.'; return false }
    if (!form.endDate)      { stepErr.value = 'End date is required.'; return false }
    if (form.endDate <= form.startDate) { stepErr.value = 'End date must be after start date.'; return false }
  }
  if (step.value === 2) {
    if (!form.customerCompany.trim()) { stepErr.value = 'Customer company is required.'; return false }
    if (!form.executingCompany.trim()){ stepErr.value = 'Executing company is required.'; return false }
  }
  if (step.value === 3) {
    if (!form.projectManagerId) { stepErr.value = 'Please select a project manager.'; return false }
  }
  return true
}

function next() { if (validate()) { stepErr.value = ''; step.value++ } }
function prev() { stepErr.value = ''; step.value-- }

async function submit() {
  saving.value = true
  try {
    const result = await projectsApi.create({
      name: form.name, customerCompany: form.customerCompany, executingCompany: form.executingCompany,
      startDate: form.startDate, endDate: form.endDate,
      projectManagerId: form.projectManagerId, employeeIds: form.employeeIds, priority: form.priority,
    })
    for (const f of form.files) {
      try { await documentsApi.upload(result.id, f) } catch { /* skip failed files */ }
    }
    notif.show(`Project «${form.name}» created.`)
    router.push(`/projects/${result.id}`)
  } catch (e: any) { notif.show(e.message, 'error') }
  finally { saving.value = false }
}

const stepLabels = ['Basic Info', 'Companies', 'Manager', 'Team', 'Documents']
</script>

<template>
  <nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
      <li class="breadcrumb-item"><RouterLink to="/projects">Projects</RouterLink></li>
      <li class="breadcrumb-item active">New Project</li>
    </ol>
  </nav>

  <div class="row justify-content-center">
    <div class="col-lg-8">
      <div class="card">
        <div class="card-header"><i class="bi bi-plus-circle me-2"></i>Create New Project</div>
        <div class="card-body">

          <!-- Wizard steps -->
          <div class="wizard-steps mb-4">
            <div v-for="(label, i) in stepLabels" :key="i"
                 class="wizard-step"
                 :class="{ active: step === i+1, completed: step > i+1 }">
              <span class="step-number">{{ i+1 }}</span>
              <span class="d-none d-xl-inline">{{ label }}</span>
            </div>
          </div>

          <div v-if="stepErr" class="alert alert-warning py-2 mb-3">
            <i class="bi bi-exclamation-triangle me-2"></i>{{ stepErr }}
          </div>

          <!-- Step 1: Basic Info -->
          <div v-show="step === 1">
            <h5 class="mb-3 text-muted">Step 1 — Project Information</h5>
            <div class="mb-3">
              <label class="form-label">Project Name *</label>
              <input v-model="form.name" type="text" class="form-control" placeholder="e.g. CRM Integration Phase 2" />
            </div>
            <div class="row mb-3">
              <div class="col-md-6">
                <label class="form-label">Start Date *</label>
                <input v-model="form.startDate" type="date" class="form-control" />
              </div>
              <div class="col-md-6">
                <label class="form-label">End Date *</label>
                <input v-model="form.endDate" type="date" class="form-control" />
              </div>
            </div>
            <div class="mb-3" style="max-width:180px;">
              <label class="form-label">Priority</label>
              <input v-model.number="form.priority" type="number" min="0" class="form-control" />
              <div class="form-text">Higher = more priority.</div>
            </div>
          </div>

          <!-- Step 2: Companies -->
          <div v-show="step === 2">
            <h5 class="mb-3 text-muted">Step 2 — Companies</h5>
            <div class="mb-3">
              <label class="form-label">Customer Company *</label>
              <input v-model="form.customerCompany" type="text" class="form-control" placeholder="e.g. Acme Corporation" />
            </div>
            <div class="mb-3">
              <label class="form-label">Executing Company *</label>
              <input v-model="form.executingCompany" type="text" class="form-control" placeholder="e.g. Dev Studio LLC" />
            </div>
          </div>

          <!-- Step 3: Manager -->
          <div v-show="step === 3">
            <h5 class="mb-3 text-muted">Step 3 — Project Manager</h5>
            <p class="text-muted small">Search for an employee to assign as the project manager.</p>
            <div class="mb-3">
              <label class="form-label">Search Employee *</label>
              <AutocompleteInput
                v-model="form.projectManagerId"
                v-model:modelName="form.projectManagerName"
                :searchFn="(t) => employeesApi.search(t, 10)"
                :invalid="stepErr.includes('manager')"
              />
            </div>
          </div>

          <!-- Step 4: Team -->
          <div v-show="step === 4">
            <h5 class="mb-3 text-muted">Step 4 — Team Members</h5>
            <p class="text-muted small">Optionally add team members. Can also be assigned later.</p>
            <div class="mb-3">
              <label class="form-label">Search &amp; Add Employee</label>
              <div class="autocomplete-wrapper">
                <input
                  v-model="teamTerm"
                  type="text"
                  class="form-control"
                  placeholder="Start typing a name…"
                  autocomplete="off"
                  @focus="onTeamFocus"
                  @input="onTeamInput"
                  @blur="onTeamBlur"
                />
                <div v-if="teamShow" class="autocomplete-dropdown" style="display:block;">
                  <div v-if="!teamResults.length" class="autocomplete-item no-results">No employees found</div>
                  <div
                    v-for="e in teamResults" :key="e.id"
                    class="autocomplete-item"
                    @mousedown.prevent="addTeam(e)"
                  >{{ e.fullName }}</div>
                </div>
              </div>
            </div>
            <div class="emp-chips mb-2">
              <span v-for="e in teamChips" :key="e.id" class="employee-chip">
                {{ e.fullName }}
                <button type="button" class="remove-btn" @click="removeTeam(e.id)">
                  <i class="bi bi-x"></i>
                </button>
              </span>
            </div>
            <p v-if="!teamChips.length" class="text-muted small">No team members selected yet.</p>
          </div>

          <!-- Step 5: Documents -->
          <div v-show="step === 5">
            <h5 class="mb-3 text-muted">Step 5 — Project Documents</h5>
            <p class="text-muted small">Optionally attach files. You can also upload after creation.</p>
            <FileDropZone @update:files="form.files = $event" />
          </div>

          <!-- Navigation -->
          <div class="d-flex justify-content-between mt-4 pt-3 border-top">
            <button v-if="step > 1" type="button" class="btn btn-outline-secondary" @click="prev">
              <i class="bi bi-chevron-left me-1"></i>Previous
            </button>
            <div class="ms-auto d-flex gap-2">
              <button v-if="step < total" type="button" class="btn btn-primary" @click="next">
                Next<i class="bi bi-chevron-right ms-1"></i>
              </button>
              <button v-else type="button" class="btn btn-success" :disabled="saving" @click="submit">
                <span v-if="saving" class="spinner-border spinner-border-sm me-1"></span>
                <i v-else class="bi bi-check-lg me-1"></i>Create Project
              </button>
            </div>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>
