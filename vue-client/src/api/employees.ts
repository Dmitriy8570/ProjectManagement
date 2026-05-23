import client from './client'
import type { EmployeeDto, CreateEmployeeRequest, EditEmployeeRequest, EmployeeProjectsDto } from '@/types'

export const employeesApi = {
  /**
   * @param roles Optional whitelist of role names — when set, the server
   *   restricts the results to users in at least one of these roles. Used by
   *   the PM picker on the project wizard to hide plain Employee users.
   *   The server re-validates the eligibility rule on submit.
   */
  search: (term = '', limit = 10, roles?: string[]) =>
    client.get<EmployeeDto[]>('/employees', {
      params: {
        term,
        limit,
        ...(roles && roles.length > 0 ? { roles: roles.join(',') } : {})
      }
    }).then(r => r.data),

  getById: (id: number) =>
    client.get<EmployeeDto>(`/employees/${id}`).then(r => r.data),

  getProjects: (id: number) =>
    client.get<EmployeeProjectsDto>(`/employees/${id}/projects`).then(r => r.data),

  create: (data: CreateEmployeeRequest) =>
    client.post<{ id: number }>('/employees', data).then(r => r.data),

  update: (id: number, data: EditEmployeeRequest) =>
    client.put<{ id: number }>(`/employees/${id}`, data).then(r => r.data),

  delete: (id: number) =>
    client.delete(`/employees/${id}`),
}
