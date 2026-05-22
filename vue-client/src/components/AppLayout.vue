<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, RouterLink, RouterView } from 'vue-router'
import { useNotification } from '@/stores/notification'

const route  = useRoute()
const notif  = useNotification()
const open   = ref(false)

function toggle()  { open.value = !open.value }
function closeSb() { open.value = false }

const title = computed(() => {
  if (route.path.startsWith('/employees')) return 'Employees'
  if (route.path.startsWith('/projects'))  return 'Projects'
  return 'PM System'
})

function isActive(prefix: string) {
  return route.path.startsWith(prefix) && !route.path.includes('/create') && !route.path.includes('/edit')
}
</script>

<template>
  <div class="pm-layout">
    <aside class="pm-sidebar" :class="{ open }">
      <RouterLink to="/projects" class="pm-sidebar-brand" @click="closeSb">
        <i class="bi bi-kanban-fill"></i>PM System
      </RouterLink>
      <nav class="pm-sidebar-nav">
        <div class="pm-nav-section-label">Navigation</div>
        <RouterLink to="/projects"  class="pm-nav-link" :class="{ active: isActive('/projects') }"  @click="closeSb">
          <i class="bi bi-folder2-open"></i>Projects
        </RouterLink>
        <RouterLink to="/employees" class="pm-nav-link" :class="{ active: isActive('/employees') }" @click="closeSb">
          <i class="bi bi-people"></i>Employees
        </RouterLink>
        <div class="pm-nav-divider"></div>
        <div class="pm-nav-section-label">Create</div>
        <RouterLink to="/projects/create"  class="pm-nav-link" :class="{ active: route.path === '/projects/create' }"  @click="closeSb">
          <i class="bi bi-plus-circle"></i>New Project
        </RouterLink>
        <RouterLink to="/employees/create" class="pm-nav-link" :class="{ active: route.path === '/employees/create' }" @click="closeSb">
          <i class="bi bi-person-plus"></i>Add Employee
        </RouterLink>
      </nav>
    </aside>

    <div class="pm-sidebar-overlay" :class="{ open }" @click="closeSb"></div>

    <div class="pm-main">
      <header class="pm-topbar">
        <button class="pm-topbar-toggle" @click="toggle"><i class="bi bi-list"></i></button>
        <span class="pm-topbar-title">{{ title }}</span>
      </header>

      <main class="pm-content">
        <Teleport to="body">
          <Transition name="slide-down">
            <div v-if="notif.message" class="pm-toast">
              <div class="alert mb-0 py-2 d-flex align-items-center gap-2"
                   :class="notif.type === 'success' ? 'alert-success' : 'alert-danger'">
                <i class="bi flex-shrink-0"
                   :class="notif.type === 'success' ? 'bi-check-circle' : 'bi-exclamation-triangle'"></i>
                <span>{{ notif.message }}</span>
              </div>
            </div>
          </Transition>
        </Teleport>
        <RouterView />
      </main>
    </div>
  </div>
</template>
