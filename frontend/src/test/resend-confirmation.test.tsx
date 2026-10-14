import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ResendConfirmation } from '../ResendConfirmation'
import { AuthProvider } from '../auth'
import { resetCsrf } from '../api'
import { ConfirmEmailPage, LoginForm, ResendConfirmationPage } from '../pages/PublicPages'

const json = (value: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }))
afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers(); resetCsrf() })

describe('resend email confirmation', () => {
  it('offers a standalone route without requiring a password', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(input => json(String(input) === '/api/auth/csrf' ? { token: 'csrf-test' } : { message: 'OK' }))
    render(<MemoryRouter><ResendConfirmationPage /></MemoryRouter>)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'recipient@example.test' } })
    fireEvent.click(screen.getByRole('button', { name: 'Отправить письмо повторно' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Если email ещё не подтверждён')
    const call = fetch.mock.calls.find(([input]) => String(input) === '/api/auth/resend-confirmation')
    expect(JSON.parse(String(call?.[1]?.body))).toEqual({ email: 'recipient@example.test' })
    expect(screen.queryByLabelText(/Пароль/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Отправить повторно через/ })).toBeDisabled()
  })
  it('blocks repeated clicks for sixty seconds after a successful request', async () => {
    vi.useFakeTimers()
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(input => json(String(input) === '/api/auth/csrf' ? { token: 'csrf-test' } : {}))
    render(<ResendConfirmation email="recipient@example.test" />)
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Отправить письмо повторно' })) })
    expect(screen.getByRole('status')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /Отправить повторно через/ }))
    expect(fetch.mock.calls.filter(([input]) => String(input).endsWith('/resend-confirmation'))).toHaveLength(1)
    await act(async () => { vi.advanceTimersByTime(60000) })
    expect(screen.getByRole('button', { name: 'Отправить письмо повторно' })).toBeEnabled()
  })
  it('reports rate limit errors without claiming a letter was sent', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(input => String(input) === '/api/auth/csrf' ? json({ token: 'csrf-test' }) : json({ message: 'Слишком много запросов. Попробуйте позже.' }, 429))
    render(<ResendConfirmation email="recipient@example.test" />)
    fireEvent.click(screen.getByRole('button', { name: 'Отправить письмо повторно' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Слишком много запросов')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
  it('shows a resend button for an unconfirmed login without submitting login twice', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(input => {
      if (String(input) === '/api/me') return json(null)
      if (String(input) === '/api/auth/csrf') return json({ token: 'csrf-test' })
      if (String(input) === '/api/auth/login') return json({ detail: 'Подтвердите email перед входом.', code: 'email_not_confirmed' }, 403)
      return json({})
    })
    render(<MemoryRouter><AuthProvider><LoginForm /></AuthProvider></MemoryRouter>)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'recipient@example.test' } })
    fireEvent.change(screen.getByLabelText(/Пароль/), { target: { value: 'Demo-only-password-2026!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Войти' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Отправить письмо повторно' }))
    await screen.findByRole('status')
    expect(fetch.mock.calls.filter(([input]) => String(input) === '/api/auth/login')).toHaveLength(1)
    expect(fetch.mock.calls.filter(([input]) => String(input) === '/api/auth/resend-confirmation')).toHaveLength(1)
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'different@example.test' } })
    await waitFor(() => expect(screen.queryByRole('button', { name: /Отправить повторно через/ })).not.toBeInTheDocument())
  })
  it('lets the user request a new confirmation after an invalid link', async () => {
    render(<MemoryRouter initialEntries={['/confirm-email']}><ConfirmEmailPage /></MemoryRouter>)
    expect(await screen.findByText('Ссылка недействительна или устарела.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Отправить письмо повторно' })).toHaveAttribute('href', '/resend-confirmation')
  })
})
