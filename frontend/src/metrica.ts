type MetricaProperties = Record<string, string | number | boolean | null | undefined>

type YandexMetrica = ((counterId: number, method: string, ...args: unknown[]) => void) & {
  a?: unknown[][]
  l?: number
}

declare global {
  interface Window {
    ym?: YandexMetrica
  }
}

const configuredCounterId = Number(import.meta.env.VITE_YANDEX_METRIKA_ID?.trim())
const counterId = Number.isSafeInteger(configuredCounterId) && configuredCounterId > 0 ? configuredCounterId : null
const tagId = 'yandex-metrica-tag'
let initialized = false

function currentPath(path = window.location.pathname) {
  return path
    .replace(/^\/guest\/participations\/[^/]+$/, '/guest/participations/:token')
    .replace(/^\/(player\/training|coach\/trainings|coach\/players|parent\/children)\/[^/]+$/, '/$1/:id')
}

function safeProperties(properties: MetricaProperties) {
  return Object.fromEntries(Object.entries(properties).filter(([key, value]) => {
    if (value === undefined || /email|phone|name|contact|token|password/i.test(key)) return false
    return typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' || value === null
  }))
}

function callMetrica(method: string, ...args: unknown[]) {
  if (!counterId || typeof window === 'undefined') return
  window.ym?.(counterId, method, ...args)
}

export function initYandexMetrica() {
  if (!counterId || initialized || typeof window === 'undefined') return
  initialized = true

  if (!window.ym) {
    const queued = ((...args: unknown[]) => { (queued.a ??= []).push(args) }) as YandexMetrica
    queued.l = Date.now()
    window.ym = queued
  }

  if (!document.getElementById(tagId)) {
    const tag = document.createElement('script')
    tag.id = tagId
    tag.async = true
    tag.src = 'https://mc.yandex.ru/metrika/tag.js'
    document.head.append(tag)
  }

  callMetrica('init', { defer: true, clickmap: true, trackLinks: true, accurateTrackBounce: true, sendTitle: false })
}

export function trackYandexMetricaPageView(path?: string) {
  if (!counterId || typeof window === 'undefined') return
  callMetrica('hit', currentPath(path), { title: document.title })
}

export function trackYandexMetricaGoal(goal: string, properties: MetricaProperties = {}) {
  callMetrica('reachGoal', goal, safeProperties(properties))
}
