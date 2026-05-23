<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter, RouterLink, RouterView } from 'vue-router'
import { useNotification } from '@/stores/notification'
import { useAuth } from '@/stores/auth'
import { Roles } from '@/types'

const route   = useRoute()
const router  = useRouter()
const notif   = useNotification()
const auth    = useAuth()
const open    = ref(false)

function toggle()  { open.value = !open.value }
function closeSb() { open.value = false }

const title = computed(() => {
  if (route.path.startsWith('/employees')) return 'Employees'
  if (route.path.startsWith('/tasks'))     return 'Tasks'
  if (route.path.startsWith('/projects'))  return 'Projects'
  if (route.path.startsWith('/login'))     return 'Sign in'
  return 'PM System'
})

function isActive(prefix: string) {
  return route.path.startsWith(prefix) && !route.path.includes('/create') && !route.path.includes('/edit')
}

// On /login the whole chrome (sidebar + topbar) goes away — the standalone
// card looks cleaner and matches the Razor Sign-in page.
const showChrome = computed(() => auth.isAuthenticated && !route.path.startsWith('/login'))

const isDirector = computed(() => auth.hasRole(Roles.Director))

async function signOut() {
  auth.logout()
  await router.push('/login')
}
</script>

<template>
  <div class="pm-layout">
    <!-- Sidebar (only when signed in) -->
    <aside v-if="showChrome" class="pm-sidebar" :class="{ open }">
      <RouterLink to="/projects" class="pm-sidebar-brand" @click="closeSb">
        <i class="bi bi-kanban-fill"></i>PM System
      </RouterLink>
      <nav class="pm-sidebar-nav">
        <div class="pm-nav-section-label">Navigation</div>
        <RouterLink to="/projects"  class="pm-nav-link" :class="{ active: isActive('/projects') }"  @click="closeSb">
          <i class="bi bi-folder2-open"></i>Projects
        </RouterLink>
        <RouterLink v-if="isDirector" to="/employees" class="pm-nav-link" :class="{ active: isActive('/employees') }" @click="closeSb">
          <i class="bi bi-people"></i>Employees
        </RouterLink>
        <RouterLink v-if="isDirector"to="/tasks" class="pm-nav-link" :class="{ active: isActive('/tasks') }" @click="closeSb">
          <i class="bi bi-list-check"></i>Tasks
        </RouterLink>

        <template v-if="isDirector">
          <div class="pm-nav-divider"></div>
          <div class="pm-nav-section-label">Create</div>
          <RouterLink to="/projects/create"  class="pm-nav-link" :class="{ active: route.path === '/projects/create' }"  @click="closeSb">
            <i class="bi bi-plus-circle"></i>New Project
          </RouterLink>
          <RouterLink to="/employees/create" class="pm-nav-link" :class="{ active: route.path === '/employees/create' }" @click="closeSb">
            <i class="bi bi-person-plus"></i>Add Employee
          </RouterLink>
          <RouterLink to="/tasks/create" class="pm-nav-link" :class="{ active: route.path === '/tasks/create' }" @click="closeSb">
            <i class="bi bi-plus-square"></i>New Task
          </RouterLink>
        </template>
>>>>>>> task.2
      </nav>
    </aside>

    <div v-if="showChrome" class="pm-sidebar-overlay" :class="{ open }" @click="closeSb"></div>

    <div class="pm-main" :class="{ 'pm-main-full': !showChrome }">
      <header v-if="showChrome" class="pm-topbar">
        <button class="pm-topbar-toggle" @click="toggle"><i class="bi bi-list"></i></button>
        <span class="pm-topbar-title">{{ title }}</span>

        <div class="ms-auto d-flex align-items-center gap-2">
          <span class="text-muted small d-none d-sm-inline">
            <i class="bi bi-person-circle me-1"></i>{{ auth.user?.email }}
          </span>
          <button type="button" class="btn btn-sm btn-outline-secondary" @click="signOut">
            <i class="bi bi-box-arrow-right me-1"></i>Sign out
          </button>
        </div>
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

<style scoped>
/* When the sidebar is hidden (login page) the main pane should occupy the
   full viewport — drop the side margin that the regular layout reserves. */
.pm-main-full {
  margin-left: 0;
}
</style>
