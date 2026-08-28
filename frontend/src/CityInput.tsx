import { useEffect, useId, useState } from 'react'
import { api } from './api'

type CityOption = { city: string; region: string }

export function CityInput({ name = 'city', defaultValue = '', required = false, onValueChange }: { name?: string; defaultValue?: string; required?: boolean; onValueChange?: (value: string) => void }) {
  const [value, setValue] = useState(defaultValue)
  const [options, setOptions] = useState<CityOption[]>([])
  const [open, setOpen] = useState(false)
  const listId = useId()

  useEffect(() => { setValue(defaultValue) }, [defaultValue])
  useEffect(() => {
    const query = value.trim()
    if (query.length < 2) { setOptions([]); return }
    const timer = window.setTimeout(() => {
      api<CityOption[]>(`/api/reference/cities?q=${encodeURIComponent(query)}`)
        .then(setOptions)
        .catch(() => setOptions([]))
    }, 180)
    return () => window.clearTimeout(timer)
  }, [value])

  return <div className="city-picker">
    <input name={name} value={value} required={required} autoComplete="address-level2" placeholder="Начните вводить город"
      role="combobox" aria-autocomplete="list" aria-controls={listId} aria-expanded={open && options.length > 0}
      onFocus={() => setOpen(true)} onChange={event => { setValue(event.target.value); onValueChange?.(event.target.value); setOpen(true) }}
      onBlur={() => window.setTimeout(() => setOpen(false), 120)} />
    {open && value.trim().length >= 2 && <ul id={listId} className="city-suggestions" role="listbox">
      {options.length > 0 ? options.map(option => <li key={`${option.city}-${option.region}`}>
        <button type="button" onMouseDown={event => event.preventDefault()} onClick={() => { setValue(option.city); onValueChange?.(option.city); setOpen(false) }}>
          <strong>{option.city}</strong><span>{option.region}</span>
        </button>
      </li>) : <li className="city-suggestions-empty">Город не найден. Уточните написание.</li>}
    </ul>}
  </div>
}
