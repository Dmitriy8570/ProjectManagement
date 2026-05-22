<script setup lang="ts">
import { ref, watch } from 'vue'
import type { EmployeeDto } from '@/types'

interface Props {
  modelValue: number
  modelName?: string
  searchFn: (term: string) => Promise<EmployeeDto[]>
  placeholder?: string
  invalid?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  modelName: '',
  placeholder: 'Search by name…',
  invalid: false
})

const emit = defineEmits<{
  'update:modelValue': [id: number]
  'update:modelName':  [name: string]
}>()

const term    = ref('')
const results = ref<EmployeeDto[]>([])
const show    = ref(false)
const chip    = ref(props.modelName)
let   timer   = 0

watch(() => props.modelName, v => { chip.value = v })

async function search(q: string) {
  try {
    results.value = await props.searchFn(q)
    show.value = true
  } catch { show.value = false }
}

function onFocus()  { search(term.value) }
function onInput()  { clearTimeout(timer); timer = window.setTimeout(() => search(term.value), 220) }
function onBlur()   { setTimeout(() => { show.value = false }, 180) }

function select(e: EmployeeDto) {
  emit('update:modelValue', e.id)
  emit('update:modelName',  e.fullName)
  chip.value  = e.fullName
  term.value  = ''
  show.value  = false
}

function clear() {
  emit('update:modelValue', 0)
  emit('update:modelName',  '')
  chip.value = ''
}
</script>

<template>
  <div>
    <div class="autocomplete-wrapper">
      <input
        v-model="term"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': invalid }"
        :placeholder="placeholder"
        autocomplete="off"
        @focus="onFocus"
        @input="onInput"
        @blur="onBlur"
      />
      <div v-if="show && results.length" class="autocomplete-dropdown" style="display:block;">
        <div
          v-for="emp in results"
          :key="emp.id"
          class="autocomplete-item"
          @mousedown.prevent="select(emp)"
        >{{ emp.fullName }}</div>
      </div>
      <div v-if="show && !results.length" class="autocomplete-dropdown" style="display:block;">
        <div class="autocomplete-item no-results">No employees found</div>
      </div>
    </div>
    <div v-if="chip" class="pm-selected-chip">
      <span><i class="bi bi-person-badge me-2"></i>{{ chip }}</span>
      <button type="button" class="pm-chip-remove" @click="clear">
        <i class="bi bi-x-lg"></i>
      </button>
    </div>
  </div>
</template>
