import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useNotification = defineStore('notification', () => {
  const message = ref('')
  const type    = ref<'success' | 'error'>('success')
  let timer = 0

  function show(msg: string, t: 'success' | 'error' = 'success') {
    message.value = msg
    type.value    = t
    clearTimeout(timer)
    timer = window.setTimeout(() => { message.value = '' }, 4500)
  }

  return { message, type, show }
})
