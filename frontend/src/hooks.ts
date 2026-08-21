import { useCallback, useEffect, useState } from 'react'
import { api } from './api'

export function useApiData<T>(path: string | null) {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(Boolean(path))
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    if (!path) return
    setLoading(true); setError('')
    try { setData(await api<T>(path)) } catch (e) { setError(e instanceof Error ? e.message : 'Неизвестная ошибка') } finally { setLoading(false) }
  }, [path])
  useEffect(() => { void load() }, [load])
  return { data, setData, loading, error, reload: load }
}

export function formatDate(value?: string) {
  return value ? new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long' }).format(new Date(value)) : '—'
}
