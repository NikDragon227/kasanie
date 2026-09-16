import { post } from './api'

export const analyticsEvents = {
  homeViewed: 'home_viewed',
  filterChanged: 'filter_changed',
  searchSubmitted: 'search_submitted',
  searchEmpty: 'search_empty',
  activityOpened: 'activity_opened',
  joinStarted: 'join_started',
  joinCompleted: 'join_completed',
  participationCancelled: 'participation_cancelled',
  registrationStarted: 'registration_started',
  registrationCompleted: 'registration_completed',
  loginCompleted: 'login_completed'
} as const

type AnalyticsEvent = typeof analyticsEvents[keyof typeof analyticsEvents]
type Properties = Record<string, string | number | boolean | null | undefined>

let generatedSessionId: string | null = null
const viewedPaths = new Set<string>()

function sessionId() {
  try {
    const stored = window.sessionStorage.getItem('kasanie.analytics.session')
    if (stored) return stored
    const next = window.crypto?.randomUUID?.() ?? `s-${Date.now()}-${Math.random().toString(36).slice(2, 12)}`
    window.sessionStorage.setItem('kasanie.analytics.session', next)
    return next
  } catch {
    generatedSessionId ??= `s-${Date.now()}-${Math.random().toString(36).slice(2, 12)}`
    return generatedSessionId
  }
}

export function trackProductEvent(name: AnalyticsEvent, properties: Properties = {}) {
  if (typeof window === 'undefined') return
  const safeProperties = Object.fromEntries(Object.entries(properties).filter(([, value]) => value !== undefined))
  void post('/api/analytics/events', { name, pagePath: window.location.pathname, sessionId: sessionId(), properties: safeProperties }).catch(() => undefined)
}

export function trackPageView(path = window.location.pathname) {
  if (viewedPaths.has(path)) return
  viewedPaths.add(path)
  trackProductEvent(analyticsEvents.homeViewed)
}
