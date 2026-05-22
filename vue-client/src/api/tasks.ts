import client from './client'
import type {
  ProjectTaskDto, PagedResult, ProjectTaskListFilter,
  CreateProjectTaskRequest, EditProjectTaskRequest, ProjectTaskStatus
} from '@/types'

export const tasksApi = {
  list: (filter: ProjectTaskListFilter) =>
    client.get<PagedResult<ProjectTaskDto>>('/tasks', { params: filter }).then(r => r.data),

  getById: (id: number) =>
    client.get<ProjectTaskDto>(`/tasks/${id}`).then(r => r.data),

  create: (data: CreateProjectTaskRequest) =>
    client.post<{ id: number }>('/tasks', data).then(r => r.data),

  update: (id: number, data: EditProjectTaskRequest) =>
    client.put<{ id: number }>(`/tasks/${id}`, data).then(r => r.data),

  delete: (id: number) =>
    client.delete(`/tasks/${id}`),

  changeStatus: (id: number, status: ProjectTaskStatus) =>
    client.patch(`/tasks/${id}/status`, { status }),
}
