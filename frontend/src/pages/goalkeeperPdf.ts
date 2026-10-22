import { jsPDF } from 'jspdf'
import regularFontUrl from '../assets/pdf-fonts/NotoSans-Regular.ttf?url'
import boldFontUrl from '../assets/pdf-fonts/NotoSans-Bold.ttf?url'
import { keeperName, keeperStatus, type KeeperPlan, type KeeperTeam } from './goalkeeperModel'

export type KeeperPdfInput = {
  plan: KeeperPlan
  team: Pick<KeeperTeam, 'school' | 'name' | 'coach'>
  names: { playerId: number; firstName: string; lastName: string }[]
}
export type KeeperPdfFonts = { regular: string; bold: string }
const ink = '#172E35', muted = '#576870', accent = '#4E56D9'
const clean = (value: string) => Array.from(value)
  .filter(char => ['\n', '\r', '\t'].includes(char) || (char.charCodeAt(0) >= 32 && char.charCodeAt(0) !== 127))
  .join('').replace(/[\u2010-\u2015\u2212]/g, '-').replace(/\r\n?/g, '\n').replace(/\t/g, '    ')
let fontRequest: Promise<KeeperPdfFonts> | undefined

async function readFont(url: string) {
  const response = await fetch(url, { credentials: 'same-origin' })
  if (!response.ok) throw new Error('Не удалось загрузить шрифт PDF. Проверьте соединение и попробуйте ещё раз.')
  const bytes = new Uint8Array(await response.arrayBuffer())
  if (bytes.length < 12 || bytes[0] !== 0 || bytes[1] !== 1 || bytes[2] !== 0 || bytes[3] !== 0) throw new Error('Шрифт PDF недоступен. Обновите страницу и повторите скачивание.')
  let binary = ''
  for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192))
  return btoa(binary)
}

function loadFonts() {
  fontRequest ??= Promise.all([readFont(regularFontUrl), readFont(boldFontUrl)])
    .then(([regular, bold]) => ({ regular, bold }))
    .catch((error: unknown) => { fontRequest = undefined; throw error })
  return fontRequest
}

export function keeperPdfFilename(plan: Pick<KeeperPlan, 'scheduledAt' | 'title'>) {
  const date = /^\d{4}-\d{2}-\d{2}/.exec(plan.scheduledAt)?.[0] ?? 'bez-daty'
  const title = plan.title.normalize('NFC').replace(/[^\p{L}\p{N} _-]/gu, ' ').replace(/\s+/g, ' ').trim().slice(0, 70)
  return `План тренировки ${date}${title ? ` - ${title}` : ''}.pdf`
}

/** Renders plain text only. No HTML, remote images, player medical data or personal observations. */
export function createKeeperPlanPdf({ plan, team, names }: KeeperPdfInput, fonts: KeeperPdfFonts) {
  const start = new Date(plan.scheduledAt)
  if (!Number.isFinite(start.getTime()) || !plan.title.trim() || !plan.plan.length || plan.plan.some(step => !step.title.trim() || !Number.isFinite(step.durationMinutes) || step.durationMinutes < 1)) {
    throw new Error('В сохранённом плане должны быть дата, тема и этапы с длительностью.')
  }
  const doc = new jsPDF({ unit: 'mm', format: 'a4', compress: true, putOnlyUsedFonts: true })
  doc.addFileToVFS('NotoSans-Regular.ttf', fonts.regular)
  doc.addFont('NotoSans-Regular.ttf', 'NotoSans', 'normal')
  doc.addFileToVFS('NotoSans-Bold.ttf', fonts.bold)
  doc.addFont('NotoSans-Bold.ttf', 'NotoSans', 'bold')
  doc.setLanguage('ru')
  doc.setProperties({ title: clean(plan.title), subject: 'Поэтапный план тренировки вратарей', creator: 'Касание' })
  const left = 16, width = 178, bottom = 274
  let y = 31, continuing = ''
  const font = (size = 10, bold = false, color = ink) => { doc.setFont('NotoSans', bold ? 'bold' : 'normal'); doc.setFontSize(size); doc.setTextColor(color) }
  const header = () => {
    font(12, true, accent); doc.text('КАСАНИЕ', left, 15)
    font(7.5, false, muted); doc.text('ПЛАН ТРЕНИРОВКИ ВРАТАРЕЙ', 194, 15, { align: 'right' })
    doc.setDrawColor('#DCE3E6'); doc.setLineWidth(.3); doc.line(left, 21, 194, 21)
  }
  const nextPage = () => {
    doc.addPage(); header(); y = 31
    if (continuing) { font(8, true, muted); doc.text(continuing, left, y); y += 7 }
  }
  const ensure = (height: number) => { if (y + height > bottom) nextPage() }
  const text = (value: string, options: { size?: number; bold?: boolean; color?: string; gap?: number; x?: number; maxWidth?: number } = {}) => {
    const { size = 10, bold = false, color = ink, gap = 3, x = left, maxWidth = width } = options
    font(size, bold, color)
    const lines = doc.splitTextToSize(clean(value), maxWidth) as string[]
    const leading = size * .3528 * 1.5
    for (const line of lines) { ensure(leading); font(size, bold, color); doc.text(line, x, y); y += leading }
    y += gap
  }
  const section = (title: string, reserve = 20) => {
    continuing = ''; ensure(reserve)
    font(9, true, accent); doc.text(title, left, y); y += 8
  }
  const field = (label: string, value: string) => { if (value.trim()) text(`${label}: ${value}`) }
  header()
  text(plan.title, { size: 20, bold: true, gap: 5 })
  field('Школа', team.school)
  field('Команда', team.name)
  field('Тренер', team.coach)
  const total = plan.plan.reduce((sum, step) => sum + step.durationMinutes, 0)
  const date = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' }).format(start)
  const clock = (minutes: number) => new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(new Date(start.getTime() + minutes * 60000))
  const zone = Intl.DateTimeFormat('ru-RU', { timeZoneName: 'short' }).formatToParts(start).find(part => part.type === 'timeZoneName')?.value ?? ''
  text(`${date} | ${clock(0)} - ${clock(total)} (${zone}) | ${total} мин`, { bold: true })
  text(`${keeperStatus[plan.status] ?? plan.status} | Интенсивность: ${plan.intensity}`, { color: muted, gap: 5 })
  field('Место', plan.venue)
  if (plan.objective.trim()) { section('ЦЕЛЬ ЗАНЯТИЯ'); text(plan.objective) }
  const participants = [...new Set(plan.players.map(player => player.playerId))]
  section(`УЧАСТНИКИ - ${participants.length}`)
  text(participants.map(id => { const player = names.find(p => p.playerId === id); return player ? keeperName(player) : `Вратарь #${id}` }).join(', ') || 'Не выбраны', { gap: 7 })
  section('ПЛАН ПО ЭТАПАМ', 38)
  let elapsed = 0
  plan.plan.forEach((step, index) => {
    continuing = ''; ensure(29)
    doc.setDrawColor('#DCE3E6'); doc.line(left, y - 2, 194, y - 2); y += 4
    text(`ЭТАП ${index + 1} | ${step.stage} | ${step.durationMinutes} мин | ${clock(elapsed)} - ${clock(elapsed + step.durationMinutes)}`, { size: 8.5, bold: true, color: accent, gap: 2 })
    continuing = `Этап ${index + 1} - продолжение`
    text(step.title, { size: 13, bold: true, gap: 2 })
    if (step.instructions.trim()) text(step.instructions, { gap: 6 })
    else text('Описание не добавлено.', { color: muted, gap: 6 })
    elapsed += step.durationMinutes
  })
  if (plan.notes.trim()) { section('ЗАМЕТКИ К ЗАНЯТИЮ'); text(plan.notes) }
  const count = doc.getNumberOfPages()
  for (let page = 1; page <= count; page++) {
    doc.setPage(page); doc.setDrawColor('#DCE3E6'); doc.line(left, 280, 194, 280)
    font(8, false, muted); doc.text('Касание | План тренировки', left, 286)
    doc.text(`${page} / ${count}`, 194, 286, { align: 'right' })
  }
  return doc
}

export async function downloadKeeperPlanPdf(input: KeeperPdfInput) {
  const doc = createKeeperPlanPdf(input, await loadFonts())
  await doc.save(keeperPdfFilename(input.plan), { returnPromise: true })
}
