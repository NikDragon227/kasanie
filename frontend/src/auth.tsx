import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, post, resetCsrf } from './api'

export type User = { id: string; email: string; roles: string[] }
type AuthValue = { user: User | null; loading: boolean; login: (email: string, password: string) => Promise<User>; logout: () => Promise<void>; refresh: () => Promise<void> }
const AuthContext = createContext<AuthValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)
  const refresh = async () => { try { setUser(await api<User>('/api/me')) } catch { setUser(null) } finally { setLoading(false) } }
  useEffect(() => { void refresh() }, [])
  const value = useMemo<AuthValue>(() => ({ user, loading, refresh, login: async (email, password) => { const next = await post<User>('/api/auth/login', { email, password }); resetCsrf(); setUser(next); return next }, logout: async () => { await post('/api/auth/logout'); resetCsrf(); setUser(null) } }), [user, loading])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside AuthProvider')
  return value
}
