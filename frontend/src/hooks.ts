import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from './api'

export function useApiData<T>(path: string | null) {
  const requestVersion = useRef(0)
  const invalidate = useCallback(() => { requestVersion.current++ }, [])
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(Boolean(path))
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    const version = ++requestVersion.current
    if (!path) { setData(null); setError(''); setLoading(false); return }
    setLoading(true); setError('')
    try { const next = await api<T>(path); if (version === requestVersion.current) setData(next) } catch (e) { if (version === requestVersion.current) setError(e instanceof Error ? e.message : 'Неизвестная ошибка') } finally { if (version === requestVersion.current) setLoading(false) }
  }, [path])
  useEffect(() => { void load(); return invalidate }, [load, invalidate])
  return { data, setData, loading, error, reload: load }
}

export function formatDate(value?: string) {
  return value ? new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long' }).format(new Date(value)) : '—'
}
