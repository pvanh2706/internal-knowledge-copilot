import { apiRequest } from './http'

export interface Team {
  id: string
  name: string
  description?: string
}

export function getTeams(token: string) {
  return apiRequest<Team[]>('/teams', {}, token)
}

export function createTeam(payload: { name: string; description?: string }, token: string) {
  return apiRequest<Team>(
    '/teams',
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    token,
  )
}
