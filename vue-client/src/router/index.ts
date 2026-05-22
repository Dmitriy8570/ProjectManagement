import { createRouter, createWebHistory } from 'vue-router'
import ProjectList    from '@/views/projects/ProjectList.vue'
import ProjectDetail  from '@/views/projects/ProjectDetail.vue'
import ProjectCreate  from '@/views/projects/ProjectCreate.vue'
import ProjectEdit    from '@/views/projects/ProjectEdit.vue'
import EmployeeList   from '@/views/employees/EmployeeList.vue'
import EmployeeDetail from '@/views/employees/EmployeeDetail.vue'
import EmployeeCreate from '@/views/employees/EmployeeCreate.vue'
import EmployeeEdit   from '@/views/employees/EmployeeEdit.vue'

export default createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/',                    redirect: '/projects' },
    { path: '/projects',            component: ProjectList    },
    { path: '/projects/create',     component: ProjectCreate  },
    { path: '/projects/:id(\\d+)',  component: ProjectDetail  },
    { path: '/projects/:id(\\d+)/edit', component: ProjectEdit },
    { path: '/employees',           component: EmployeeList   },
    { path: '/employees/create',    component: EmployeeCreate },
    { path: '/employees/:id(\\d+)', component: EmployeeDetail },
    { path: '/employees/:id(\\d+)/edit', component: EmployeeEdit },
  ],
  scrollBehavior: () => ({ top: 0 })
})
