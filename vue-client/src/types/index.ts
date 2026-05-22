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
