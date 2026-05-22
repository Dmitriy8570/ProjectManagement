import client from './client'
import type { ProjectDocumentDto } from '@/types'

export const documentsApi = {
  list: (projectId: number) =>
    client.get<ProjectDocumentDto[]>(`/projects/${projectId}/documents`).then(r => r.data),

  upload: (projectId: number, file: File) => {
    const form = new FormData()
    form.append('file', file, file.name)
    return client.post<ProjectDocumentDto>(
      `/projects/${projectId}/documents`, form,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    ).then(r => r.data)
  },

  downloadUrl: (id: number) => `/api/documents/${id}/download`,

  delete: (id: number) =>
    client.delete(`/documents/${id}`),
}
