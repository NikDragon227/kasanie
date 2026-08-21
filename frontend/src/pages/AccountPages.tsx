import { useState, type FormEvent } from 'react'
import { ApiError, post } from '../api'
import { PageHeader } from '../components'

type Status = { text: string; ok: boolean }

export function AccountSecurityPage() {
  const [show, setShow] = useState(false)
  const [pending, setPending] = useState(false)
  const [status, setStatus] = useState<Status | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const currentPassword = String(data.get('currentPassword'))
    const newPassword = String(data.get('newPassword'))
    const repeatPassword = String(data.get('repeatPassword'))
    setStatus(null)
    setErrors({})
    if (newPassword.length < 8) return setErrors({ newPassword: 'Пароль должен содержать не менее 8 символов.' })
    if (newPassword !== repeatPassword) return setErrors({ repeatPassword: 'Пароли не совпадают.' })

    setPending(true)
    try {
      const result = await post<{ message: string }>('/api/auth/change-password', { currentPassword, newPassword })
      setStatus({ text: result.message, ok: true })
      form.reset()
      setShow(false)
    } catch (e) {
      const apiErrors = e instanceof ApiError ? e.body.errors as Record<string, unknown> | undefined : undefined
      const nextErrors: Record<string, string> = {}
      for (const field of ['currentPassword', 'newPassword']) {
        const values = apiErrors?.[field]
        if (Array.isArray(values) && values.every(x => typeof x === 'string')) nextErrors[field] = values.join(' ')
      }
      if (Object.keys(nextErrors).length > 0) setErrors(nextErrors)
      else setStatus({ text: e instanceof Error ? e.message : 'Не удалось изменить пароль.', ok: false })
    } finally {
      setPending(false)
    }
  }

  const passwordType = show ? 'text' : 'password'
  return <><PageHeader eyebrow="Личный кабинет" title="Безопасность" /><section className="card form-card account-security"><h2>Сменить пароль</h2><p className="legal-note">После смены пароля текущий вход останется активным, а новый пароль начнёт действовать сразу.</p><form className="profile-form" onSubmit={submit}><label className="full">Текущий пароль<span className="password-control"><input name="currentPassword" type={passwordType} autoComplete="current-password" aria-invalid={Boolean(errors.currentPassword)} required /><button type="button" className="password-toggle" onClick={() => setShow(x => !x)} aria-pressed={show}>{show ? 'Скрыть' : 'Показать'}</button></span>{errors.currentPassword && <span className="error-message" role="alert">{errors.currentPassword}</span>}</label><label>Новый пароль<input name="newPassword" type={passwordType} autoComplete="new-password" minLength={8} aria-invalid={Boolean(errors.newPassword)} required /><small>Не менее 8 символов: строчная и заглавная буквы, цифра и специальный знак.</small>{errors.newPassword && <span className="error-message" role="alert">{errors.newPassword}</span>}</label><label>Повторите новый пароль<input name="repeatPassword" type={passwordType} autoComplete="new-password" minLength={8} aria-invalid={Boolean(errors.repeatPassword)} required />{errors.repeatPassword && <span className="error-message" role="alert">{errors.repeatPassword}</span>}</label><div className="form-actions"><button className="button" disabled={pending}>{pending ? 'Сохраняем…' : 'Изменить пароль'}</button></div>{status && <span className={status.ok ? 'success-message full' : 'error-message full'} role={status.ok ? 'status' : 'alert'}>{status.text}</span>}</form></section></>
}
