import client from './client'
import type { ProjectDto, PagedResult, ProjectListFilter, CreateProjectRequest, EditProjectRequest } from '@/types'

export const projectsApi = {
  list: (filter: ProjectListFilter) =>
    client.get<PagedResult<ProjectDto>>('/projects', { params: filter }).then(r => r.data),

  getById: (id: number) =>
    client.get<ProjectDto>(`/projects/${id}`).then(r => r.data),

  create: (data: CreateProjectRequest) =>
    client.post<{ id: number }>('/projects', data).then(r => r.data),

  update: (id: number, data: EditProjectRequest) =>
    client.put<{ id: number }>(`/projects/${id}`, data).then(r => r.data),

  delete: (id: number) =>
    client.delete(`/projects/${id}`),

  assign: (projectId: number, employeeId: number) =>
    client.patch('/projects/assign', { projectId, employeeId }),

  unassign: (projectId: number, employeeId: number) =>
    client.patch('/projects/unassign', { projectId, employeeId }),
}
