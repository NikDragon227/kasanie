import { useEffect, useState, type FormEvent } from 'react'
import { useLocation } from 'react-router-dom'
import { post } from './api'
import { useAuth } from './auth'

const categories = [
  { value: 'Bug', label: 'Баг' },
  { value: 'Idea', label: 'Идея' },
  { value: 'HardToUse', label: 'Неудобство' },
  { value: 'NotFound', label: 'Не нашёл нужного' },
  { value: 'Complaint', label: 'Жалоба' },
  { value: 'Other', label: 'Другое' }
]

export function FeedbackWidget() {
  const location = useLocation()
  const { user } = useAuth()
  const [open, setOpen] = useState(false)
  const [sent, setSent] = useState(false)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open])

  const close = () => {
    setOpen(false)
    setSent(false)
    setError('')
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setSending(true)
    setError('')
    try {
      await post('/api/feedback', {
        category: form.get('category'),
        message: String(form.get('message') ?? ''),
        contactEmail: String(form.get('contactEmail') ?? '').trim() || null,
        pagePath: location.pathname,
        technicalContext: JSON.stringify({ viewport: `${window.innerWidth}×${window.innerHeight}`, language: navigator.language })
      })
      setSent(true)
      event.currentTarget.reset()
    } catch (submissionError) {
      setError(submissionError instanceof Error ? submissionError.message : 'Не удалось отправить обращение. Попробуйте ещё раз.')
    } finally {
      setSending(false)
    }
  }

  return <>
    <button type="button" className="feedback-trigger" onClick={() => setOpen(true)} aria-haspopup="dialog" aria-label="Оставить обратную связь"><span aria-hidden>✦</span><b>Обратная связь</b></button>
    {open && <div className="feedback-layer" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) close() }}>
      <section className="feedback-dialog" role="dialog" aria-modal="true" aria-labelledby="feedback-title">
        <button type="button" className="feedback-close" aria-label="Закрыть форму обратной связи" onClick={close}>×</button>
        {sent ? <div className="feedback-success"><span aria-hidden>✓</span><h2 id="feedback-title">Спасибо — обращение получено</h2><p>Команда «Касания» разберёт его в общей очереди. Если вы оставили email, сможем уточнить детали или ответить.</p><button type="button" className="button" onClick={close}>Готово</button></div>
          : <><span className="eyebrow">Помогите сделать сервис лучше</span><h2 id="feedback-title">Обратная связь</h2><p className="feedback-intro">Расскажите о проблеме, идее или том, чего не хватило. Мы сохраним страницу и технический контекст без личных данных.</p><form className="feedback-form" onSubmit={event => void submit(event)}><label>Тип обращения<select name="category" defaultValue="Bug">{categories.map(category => <option key={category.value} value={category.value}>{category.label}</option>)}</select></label><label>Сообщение<textarea name="message" minLength={10} maxLength={2000} placeholder="Что произошло или что стоит улучшить?" required autoFocus /></label><label>Email для ответа <small>необязательно{user ? ` · вы вошли как ${user.email}` : ''}</small><input name="contactEmail" type="email" maxLength={254} placeholder="you@example.com" /></label><div className="feedback-form-actions"><small>Нажимая «Отправить», вы передаёте текст обращения команде сервиса.</small><button className="button" disabled={sending}>{sending ? 'Отправляем…' : 'Отправить'}</button></div>{error && <p className="form-error" role="alert">{error}</p>}</form></>}
      </section>
    </div>}
  </>
}
