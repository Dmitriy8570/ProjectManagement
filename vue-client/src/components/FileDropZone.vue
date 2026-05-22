<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{ 'update:files': [files: File[]] }>()

const files    = ref<File[]>([])
const dragOver = ref(false)
const input    = ref<HTMLInputElement>()

function addFiles(list: FileList | File[]) {
  files.value = [...files.value, ...Array.from(list)]
  emit('update:files', files.value)
}

function remove(idx: number) {
  files.value = files.value.filter((_, i) => i !== idx)
  emit('update:files', files.value)
}

function onDragOver(e: DragEvent) { e.preventDefault(); dragOver.value = true }
function onDragLeave() { dragOver.value = false }
function onDrop(e: DragEvent) {
  e.preventDefault(); dragOver.value = false
  if (e.dataTransfer?.files.length) addFiles(e.dataTransfer.files)
}
function onBrowse() { input.value?.click() }
function onChange(e: Event) {
  const el = e.target as HTMLInputElement
  if (el.files?.length) { addFiles(el.files); el.value = '' }
}

function fmtKb(b: number) { return (b / 1024).toFixed(1) }
</script>

<template>
  <div>
    <div
      class="drop-zone"
      :class="{ 'drag-over': dragOver }"
      @dragover="onDragOver"
      @dragleave="onDragLeave"
      @drop="onDrop"
    >
      <i class="bi bi-cloud-upload"></i>
      <p class="mb-1">
        Drag &amp; drop files here, or
        <span class="drop-zone-link" @click.stop="onBrowse">browse</span>
      </p>
      <p class="small text-muted mb-0">Max 50 MB per file</p>
      <input ref="input" type="file" multiple style="display:none;" @change="onChange" />
    </div>
    <div v-if="files.length" class="mt-3">
      <div v-for="(f, i) in files" :key="i" class="file-item">
        <i class="bi bi-file-earmark file-icon"></i>
        <span class="file-name">{{ f.name }}</span>
        <span class="file-size">{{ fmtKb(f.size) }} KB</span>
        <button type="button" class="remove-file" @click="remove(i)"><i class="bi bi-x-lg"></i></button>
      </div>
    </div>
  </div>
</template>
