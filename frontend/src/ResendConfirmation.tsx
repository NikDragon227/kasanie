import { useEffect, useState } from 'react'
import { post } from './api'
import './resendConfirmation.css'

export function ResendConfirmation({ email }: { email?: string }) {
  const [enteredEmail, setEnteredEmail] = useState('')
  const [pending, setPending] = useState(false)
  const [seconds, setSeconds] = useState(0)
  const [status, setStatus] = useState<{ text: string; ok: boolean } | null>(null)
  const cooling = seconds > 0
  useEffect(() => {
    if (!cooling) return
    const timer = window.setInterval(() => setSeconds(value => Math.max(0, value - 1)), 1000)
    return () => window.clearInterval(timer)
  }, [cooling])
  const send = async () => {
    if (pending || seconds) return
    const address = (email ?? enteredEmail).trim()
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) {
      setStatus({ text: 'Укажите корректный email.', ok: false })
      return
    }
    setPending(true); setStatus(null)
    try {
      await post('/api/auth/resend-confirmation', { email: address })
      setStatus({ text: 'Запрос на отправку принят. Если email ещё не подтверждён, проверьте «Входящие» и «Спам».', ok: true })
      setSeconds(60)
    } catch (error) {
      setStatus({ text: error instanceof Error ? error.message : 'Не удалось отправить запрос. Попробуйте позже.', ok: false })
    } finally { setPending(false) }
  }
  const content = <>
    {email === undefined ? <label>Email<input name="email" type="email" autoComplete="email" required value={enteredEmail} onChange={event => { setEnteredEmail(event.target.value); setStatus(null) }} placeholder="you@example.ru" /></label> : <p>Не пришло письмо на <strong>{email}</strong>?</p>}
    <button type={email === undefined ? 'submit' : 'button'} className="button large ghost" disabled={pending || cooling} onClick={email === undefined ? undefined : () => void send()}>{pending ? 'Отправляем…' : cooling ? `Отправить повторно через ${seconds} с` : 'Отправить письмо повторно'}</button>
    {status && <div className={status.ok ? 'success-message' : 'form-error'} role={status.ok ? 'status' : 'alert'}>{status.text}</div>}
    <small>Подтверждать email нужно по ссылке из письма. Повторный запрос доступен через минуту.</small>
  </>
  return email === undefined ? <form className="auth-form email-confirmation-resend" onSubmit={event => { event.preventDefault(); void send() }}>{content}</form> : <section className="email-confirmation-resend" aria-label="Повторная отправка подтверждения">{content}</section>
}
