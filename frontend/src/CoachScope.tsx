import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useApiData } from './hooks'
import './coachScope.css'

export type CoachTeam = { teamId: number; schoolId: number; name: string; school: string; season?: string; players?: number }
type School = { schoolId: number; name: string }
type Selection = { schoolId: number; teamId: number }
type Scope = Selection & {
  schools: School[]; teams: CoachTeam[]; schoolTeams: CoachTeam[]; loading: boolean; error: string
  reload: () => Promise<void>; selectSchool: (id: number) => void; selectTeam: (id: number) => void
  link: (path: string) => string; registerGuard: (guard: () => boolean) => () => void
}
const CoachScopeContext = createContext<Scope | null>(null)
const positiveId = (value: unknown) => Number.isInteger(Number(value)) && Number(value) > 0 ? Number(value) : 0
function savedSelection(userId: string): Selection {
  try { const value = JSON.parse(sessionStorage.getItem(`kasanie:coach-scope:${userId}`) ?? '{}'); return { schoolId: positiveId(value?.schoolId), teamId: positiveId(value?.teamId) } } catch { return { schoolId: 0, teamId: 0 } }
}
export function coachScopedLink(path: string, schoolId: number, teamId: number) {
  if (!path.startsWith('/coach') && !path.startsWith('/school')) return path
  const [pathname, query = ''] = path.split('?'), params = new URLSearchParams(query)
  if (schoolId) params.set('school', String(schoolId))
  if (path.startsWith('/coach') && teamId) params.set('team', String(teamId))
  return `${pathname}${params.size ? `?${params}` : ''}`
}

export function CoachScopeProvider({ userId, children }: { userId: string; children: ReactNode }) {
  const teamState = useApiData<CoachTeam[]>('/api/coach/teams')
  const schoolState = useApiData<School[]>('/api/school/memberships')
  const [params, setParams] = useSearchParams()
  const [saved, setSaved] = useState(() => savedSelection(userId))
  const guard = useRef<(() => boolean) | null>(null)
  const teams = useMemo(() => teamState.data ?? [], [teamState.data])
  const schools = useMemo(() => [...new Map([...(schoolState.data ?? []), ...teams.map(team => ({ schoolId: team.schoolId, name: team.school }))].filter(school => school.schoolId > 0).map(school => [school.schoolId, school])).values()].sort((a, b) => a.name.localeCompare(b.name)), [schoolState.data, teams])
  const requestedSchool = positiveId(params.get('school')), requestedTeam = positiveId(params.get('team'))
  const linkedTeam = teams.find(team => team.teamId === requestedTeam)
  const schoolId = requestedSchool || linkedTeam?.schoolId || (schools.some(school => school.schoolId === saved.schoolId) ? saved.schoolId : 0) || teams[0]?.schoolId || schools[0]?.schoolId || 0
  const schoolTeams = useMemo(() => teams.filter(team => team.schoolId === schoolId), [teams, schoolId])
  const teamId = schoolTeams.find(team => team.teamId === requestedTeam)?.teamId || schoolTeams.find(team => team.teamId === saved.teamId)?.teamId || schoolTeams[0]?.teamId || 0
  const loading = teamState.loading || schoolState.loading
  const error = teamState.error || schoolState.error || (!loading && requestedSchool && !schools.some(school => school.schoolId === requestedSchool) ? 'Эта школа недоступна тренеру.' : '') || (!loading && requestedTeam && !linkedTeam ? 'Этот состав не назначен тренеру.' : '')
  useEffect(() => {
    if (loading || error || !schoolId) return
    if (saved.schoolId !== schoolId || saved.teamId !== teamId) setSaved({ schoolId, teamId })
    try { sessionStorage.setItem(`kasanie:coach-scope:${userId}`, JSON.stringify({ schoolId, teamId })) } catch { /* Selection still persists in the URL when storage is unavailable. */ }
    if (requestedSchool !== schoolId || requestedTeam !== teamId) {
      const next = new URLSearchParams(params); next.set('school', String(schoolId))
      if (teamId) next.set('team', String(teamId)); else next.delete('team')
      setParams(next, { replace: true })
    }
  }, [loading, error, schoolId, teamId, userId, requestedSchool, requestedTeam, params, setParams, saved])
  const change = useCallback((nextSchool: number, nextTeam: number) => {
    if (nextSchool === schoolId && nextTeam === teamId || guard.current && !guard.current()) return
    const next = new URLSearchParams(params); next.set('school', String(nextSchool))
    if (nextTeam) next.set('team', String(nextTeam)); else next.delete('team')
    next.delete('player')
    setParams(next)
  }, [params, schoolId, teamId, setParams])
  const registerGuard = useCallback((callback: () => boolean) => { guard.current = callback; return () => { if (guard.current === callback) guard.current = null } }, [])
  const value: Scope = {
    schoolId, teamId, schools, teams, schoolTeams, loading, error,
    reload: async () => { await Promise.all([teamState.reload(), schoolState.reload()]) },
    selectSchool: id => { if (schools.some(school => school.schoolId === id)) change(id, teams.find(team => team.schoolId === id)?.teamId ?? 0) },
    selectTeam: id => { if (schoolTeams.some(team => team.teamId === id)) change(schoolId, id) },
    link: path => {
      const linkedId = positiveId(new URLSearchParams(path.split('?')[1] ?? '').get('team'))
      const target = teams.find(team => team.teamId === linkedId)
      return coachScopedLink(path, target?.schoolId ?? schoolId, target?.teamId ?? teamId)
    }, registerGuard
  }
  return <CoachScopeContext.Provider value={value}>{children}</CoachScopeContext.Provider>
}
export const useOptionalCoachScope = () => useContext(CoachScopeContext)
export function useCoachScope() {
  const scope = useOptionalCoachScope()
  if (!scope) throw new Error('Coach pages require CoachScopeProvider')
  return scope
}
export function CoachScopeBar({ children }: { children?: ReactNode }) {
  const scope = useCoachScope()
  return <section className="team-context-bar coach-team-context coach-scope-bar" aria-label="Школа и команда тренера"><label><small>ШКОЛА</small><select aria-label="Школа тренера" value={scope.schoolId} onChange={event => scope.selectSchool(Number(event.target.value))}>{scope.schools.length ? scope.schools.map(school => <option key={school.schoolId} value={school.schoolId}>{school.name}</option>) : <option value={0}>Нет доступных школ</option>}</select></label><label><small>КОМАНДА И СОСТАВ</small><select aria-label="Команда и состав" disabled={!scope.schoolTeams.length} value={scope.teamId} onChange={event => scope.selectTeam(Number(event.target.value))}>{scope.schoolTeams.length ? scope.schoolTeams.map(team => <option key={team.teamId} value={team.teamId}>{team.name}</option>) : <option value={0}>Состав не назначен</option>}</select></label>{children}</section>
}
