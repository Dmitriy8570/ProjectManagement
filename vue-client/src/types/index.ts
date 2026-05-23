// ── Role constants ────────────────────────────────────────────────────────────
// Must stay in sync with BusinessLogic.Identity.Roles on the server. Defined
// as a `const` map (not an enum) so the strings can flow directly into API
// calls / role checks without conversion.
export const Roles = {
  Director: 'Director',
  ProjectManager: 'ProjectManager',
  Employee: 'Employee',
} as const

export type RoleName = typeof Roles[keyof typeof Roles]

// ── Domain DTOs ───────────────────────────────────────────────────────────────

export interface EmployeeDto {
  id: number
  firstName: string
  lastName: string
  patronymic: string
  email: string
  fullName: string
}

export interface ProjectDto {
  id: number
  name: string
  customerCompany: string
  executingCompany: string
  startDate: string
  endDate: string
  priority: number
  projectManager: EmployeeDto
  employees: EmployeeDto[]
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
}

export interface ProjectListFilter {
  nameSearch?: string
  startDateFrom?: string
  startDateTo?: string
  minPriority?: number | null
  maxPriority?: number | null
  projectManagerId?: number | null
  sortBy?: string
  descending?: boolean
  page?: number
  pageSize?: number
}

export interface CreateProjectRequest {
  name: string
  customerCompany: string
  executingCompany: string
  startDate: string
  endDate: string
  projectManagerId: number
  employeeIds: number[]
  priority: number
}

export interface EditProjectRequest {
  name?: string
  customerCompany?: string
  executingCompany?: string
  startDate?: string
  endDate?: string
  projectManagerId?: number
  priority?: number
}

export interface CreateEmployeeRequest {
  firstName: string
  lastName: string
  patronymic?: string
  email: string
  /** Initial password for the linked Identity account — validated by the server's password policy. */
  password: string
  /** One of `Roles.*`. Defaults to plain Employee on the server when omitted. */
  role?: RoleName
}

export interface EditEmployeeRequest {
  firstName?: string
  lastName?: string
  patronymic?: string
  email?: string
}

export interface ProjectDocumentDto {
  id: number
  projectId: number
  fileName: string
  contentType: string
  sizeBytes: number
  uploadedAt: string
}

export interface EmployeeProjectsDto {
  managedProjects: ProjectDto[]
  participantProjects: ProjectDto[]
}

export type ProjectTaskStatus = 'ToDo' | 'InProgress' | 'Done'

export interface ProjectTaskDto {
  id: number
  name: string
  comment: string
  status: ProjectTaskStatus
  priority: number
  projectId: number
  projectName: string
  author: EmployeeDto
  assignee: EmployeeDto
}
// ── Auth DTOs ─────────────────────────────────────────────────────────────────

export interface CurrentUserDto {
  id: string
  email: string
  employeeId: number
  roles: RoleName[]
}

export interface LoginResponse {
  token: string
  expiresAtUtc: string
  user: CurrentUserDto
}
