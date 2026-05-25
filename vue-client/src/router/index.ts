import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import ProjectList    from '@/views/projects/ProjectList.vue'
import ProjectDetail  from '@/views/projects/ProjectDetail.vue'
import ProjectCreate  from '@/views/projects/ProjectCreate.vue'
import ProjectEdit    from '@/views/projects/ProjectEdit.vue'
import EmployeeList   from '@/views/employees/EmployeeList.vue'
import EmployeeDetail from '@/views/employees/EmployeeDetail.vue'
import EmployeeCreate from '@/views/employees/EmployeeCreate.vue'
import EmployeeEdit   from '@/views/employees/EmployeeEdit.vue'
import TaskList       from '@/views/tasks/TaskList.vue'
import TaskDetail     from '@/views/tasks/TaskDetail.vue'
import TaskCreate     from '@/views/tasks/TaskCreate.vue'
import TaskEdit       from '@/views/tasks/TaskEdit.vue'
import Login          from '@/views/Login.vue'
import { useAuth } from '@/stores/auth'
import { Roles, type RoleName } from '@/types'

// Route meta:
//   public — bypasses the auth check (used for /login).
//   roles  — list of roles allowed; missing means any authenticated user is ok.
declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    roles?: RoleName[]
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login',                    component: Login, meta: { public: true } },
    { path: '/',                    redirect: '/projects' },
    { path: '/projects',            component: ProjectList    },
    { path: '/projects/create',          component: ProjectCreate, meta: { roles: [Roles.Director] }  },
    { path: '/projects/:id(\\d+)',  component: ProjectDetail  },
    { path: '/projects/:id(\\d+)/edit', component: ProjectEdit, meta: { roles: [Roles.Director, Roles.ProjectManager] } },
    // Project-scoped task routes — the :projectId param locks the project in the views.
    { path: '/projects/:projectId(\\d+)/tasks',        component: TaskList   },
    { path: '/projects/:projectId(\\d+)/tasks/create', component: TaskCreate, meta: { roles: [Roles.Director, Roles.ProjectManager] } },
    { path: '/employees',                component: EmployeeList,   meta: { roles: [Roles.Director] }  },
    { path: '/employees/create',         component: EmployeeCreate, meta: { roles: [Roles.Director]} },
    { path: '/employees/:id(\\d+)', component: EmployeeDetail },
    { path: '/employees/:id(\\d+)/edit', component: EmployeeEdit,   meta: { roles: [Roles.Director]} },
    { path: '/tasks',               component: TaskList },
    { path: '/tasks/create',        component: TaskCreate, meta: {roles: [Roles.ProjectManager, Roles.Director]} },
    { path: '/tasks/:id(\\d+)',     component: TaskDetail },
    { path: '/tasks/:id(\\d+)/edit', component: TaskEdit, meta: {roles: [Roles.ProjectManager, Roles.Director]} },
  ],
  scrollBehavior: () => ({ top: 0 })
})

router.beforeEach((to) => {
  const auth = useAuth()

  if (to.meta.public) return true

  if (!auth.isAuthenticated) {
    // Stash the original path so /login can bounce the user back after
    // signing in. Avoid stashing /login itself to prevent self-loops.
    return {
      path: '/login',
      query: to.fullPath === '/login' ? {} : { returnUrl: to.fullPath }
    }
  }

  if (to.meta.roles && !auth.hasAnyRole(to.meta.roles)) {
    // Authenticated but lacks the role — send them to a page they CAN see.
    return { path: '/projects' }
  }

  return true
})

export default router
