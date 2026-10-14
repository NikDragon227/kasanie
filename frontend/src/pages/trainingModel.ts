export type TrainingPlayer = { playerId: number; firstName: string; lastName: string; dateOfBirth: string; preferredPosition: string; dominantFoot: string; experienceLevel: string; shirtNumber?: number; coachNote?: string }
export type TrainingTeamData = {
  team: { id: number; name: string; school: string; season?: string; matchFormat?: string; coachName?: string; tacticFormation?: string; trainingCycleStage: string; cycleStart?: string; cycleEnd?: string }
  players: TrainingPlayer[]
  injuries: { playerId: number; player: string; type: string; status: string; riskLevel: number; notes?: string; expectedReturnOn?: string }[]
  matches: { id: number; opponent: string; scheduledAt: string; status: string; goalsFor?: number; goalsAgainst?: number }[]
}
export type CalendarEntry = { date: string; slot: number; session: string; category: string; intensity: number; minutes: number; group: string; notes: string | null }
export type PlayerSettings = { playerId: number; group: string; position: string; role: string; focus: string; intensity: string; weakFoot: boolean; restUntil: string | null; notes: string | null }
export type MentoringGroup = { id: string; name: string; mentorId: number | null; playerIds: number[] }
export type TrainingState = {
  responsibleCoachId: number | null
  tactics: { name: string; formation: string; notes: string | null }[]
  calendar: CalendarEntry[]
  players: PlayerSettings[]
  mentoring: MentoringGroup[]
  coaches: { coachId: number; tasks: string[] }[]
  satisfaction: string | null; matchPreparation: string | null; lastMatchComment: string | null
}
export type CalendarLock = { date: string; slot: number; kind: 'Match' | 'Training'; title: string; id: number; status: string; minutes: number }
export type TrainingResponse = { state: TrainingState; version: number; updatedAt?: string; coaches: { id: number; name: string; isHeadCoach: boolean }[]; lockedSlots: CalendarLock[] }
export const trainingTabs = [['overview', 'Обзор'], ['calendar', 'Календарь'], ['groups', 'Группы'], ['mentoring', 'Наставничество'], ['individual', 'Индивидуальные'], ['rest', 'Отдых'], ['coaches', 'Тренеры']] as const
export const slotNames = ['Тренировка 1', 'Тренировка 2', 'Дополнительная']
export const defaultTrainingFormation = (format?: string) => format === '5v5' ? '1-2-1' : format === '7v7' ? '2-3-1' : format === '8v8' ? '3-3-1' : format === '9v9' ? '3-3-2' : '4-4-2'
export const positionGroups = [{ id: 'Goalkeepers', label: 'Вратари' }, { id: 'Defence', label: 'Защитники' }, { id: 'Attack', label: 'Нападение' }]
export const intensities = [{ id: 'Normal', label: 'Нормальная' }, { id: 'Half', label: 'Сократить вдвое' }, { id: 'Double', label: 'Двойная' }, { id: 'Rest', label: 'Отдых' }]
export const trainingCategories = [
  { id: 'General', label: 'Общая работа', color: '#5865ef', sessions: ['Общая тренировка', 'Командная работа', 'Игровая практика'] },
  { id: 'Defence', label: 'Защита', color: '#9460c6', sessions: ['Защита при игре низом', 'Защита при игре в воздухе', 'Защита с сохранением построения', 'Прессинг и отбор'] },
  { id: 'Attack', label: 'Атака', color: '#e78857', sessions: ['Создание голевых моментов', 'Реализация голевых моментов', 'Завершение атак'] },
  { id: 'Possession', label: 'Владение', color: '#698bcc', sessions: ['Сохранение владения мячом', 'Доставка мяча', 'Переход мяча — прессинг', 'Созидание, начиная с защиты'] },
  { id: 'Tactics', label: 'Тактика', color: '#559a69', sessions: ['Тактика матча', 'Позиционная игра', 'Без мяча — атака', 'Переходы и закрытие зон'] },
  { id: 'Fitness', label: 'Физическая форма', color: '#b79750', sessions: ['Физическая форма', 'Скоростные качества', 'Сила', 'Выносливость'] },
  { id: 'Goalkeeping', label: 'Вратари', color: '#986db2', sessions: ['Вратари — реакция', 'Вратари — игра руками', 'Вратари — ввод мяча'] },
  { id: 'SetPieces', label: 'Стандарты', color: '#c46da0', sessions: ['Угловые', 'Исполнение штрафных', 'Исполнение пенальти', 'Ауты'] },
  { id: 'Recovery', label: 'Восстановление', color: '#49a8a2', sessions: ['Восстановление', 'Общее восстановление'] },
  { id: 'Rest', label: 'Отдых', color: '#9aa5ad', sessions: ['Отдых'] }
]
export const individualFocus = ['Не задано', 'Исполнение штрафных', 'Угловые', 'Исполнение пенальти', 'Ауты', 'Скоростные качества', 'Ловкость и координация', 'Силовые качества', 'Выносливость', 'Действия в обороне', 'Действия в атаке', 'Последняя треть', 'Удары', 'Пас', 'Навесы', 'Контроль мяча', 'Игра в воздухе', 'Вратарь — реакция', 'Вратарь — тактика', 'Вратарь — техника', 'Вратарь — игра руками', 'Вратарь — ввод мяча (длинный)', 'Вратарь — ввод мяча (короткий)']
export const coachTasks = [
  { id: 'GkShots', label: 'Вратари · отражение ударов' }, { id: 'GkHandling', label: 'Вратари · игра руками и ввод мяча' },
  { id: 'DefenceTactics', label: 'Защита · тактика' }, { id: 'DefenceTechnique', label: 'Защита · техника' },
  { id: 'AttackTactics', label: 'Атака · тактика' }, { id: 'AttackTechnique', label: 'Атака · техника' },
  { id: 'PossessionTactics', label: 'Владение · тактика' }, { id: 'PossessionTechnique', label: 'Владение · техника' },
  { id: 'Strength', label: 'Физика · сила' }, { id: 'Speed', label: 'Физика · скорость' }
]
export function dateKey(date: Date) { return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}` }
export function dateFromKey(value: string) { return new Date(`${value}T12:00:00`) }
export function addDays(date: Date, days: number) { const next = new Date(date); next.setDate(next.getDate() + days); return next }
export function monday(date: Date) { return addDays(date, -((date.getDay() + 6) % 7)) }
export function weekDates(date: Date) { return Array.from({ length: 7 }, (_, i) => addDays(monday(date), i)) }
export function monthWeeks(date: Date) {
  const start = monday(new Date(date.getFullYear(), date.getMonth(), 1, 12))
  const end = new Date(date.getFullYear(), date.getMonth() + 1, 0, 12)
  const weeks: Date[][] = []
  for (let current = start; current <= end; current = addDays(current, 7)) weeks.push(weekDates(current))
  return weeks
}
export function playerAge(birthday: string) {
  const now = new Date(), born = dateFromKey(birthday.slice(0, 10))
  return now.getFullYear() - born.getFullYear() - (now.getMonth() < born.getMonth() || now.getMonth() === born.getMonth() && now.getDate() < born.getDate() ? 1 : 0)
}
export function defaultPlayerSettings(player: TrainingPlayer): PlayerSettings {
  return { playerId: player.playerId, group: player.preferredPosition === 'Вратарь' ? 'Goalkeepers' : player.preferredPosition === 'Защитник' ? 'Defence' : 'Attack', position: player.preferredPosition || 'Не указана', role: 'Игровая позиция', focus: 'Не задано', intensity: 'Normal', weakFoot: false, restUntil: null, notes: null }
}
export function normalizeTrainingState(response: TrainingResponse, data: TrainingTeamData): TrainingState {
  const state = response.state, players = new Set(data.players.map(p => p.playerId)), staff = new Set(response.coaches.map(c => c.id))
  return {
    ...state,
    responsibleCoachId: state.responsibleCoachId && staff.has(state.responsibleCoachId) ? state.responsibleCoachId : null,
    calendar: state.calendar ?? [], tactics: state.tactics ?? [],
    players: (state.players ?? []).filter(p => players.has(p.playerId)),
    mentoring: (response.version === 0 && !state.mentoring?.length ? [1, 2, 3].map(i => ({ id: `group-${i}`, name: `Группа ${i}`, mentorId: null, playerIds: [] })) : state.mentoring ?? []).map(group => ({ ...group, playerIds: group.playerIds.filter(id => players.has(id)), mentorId: group.mentorId && players.has(group.mentorId) ? group.mentorId : null })),
    coaches: (state.coaches ?? []).filter(c => staff.has(c.coachId)),
    satisfaction: state.satisfaction ?? null, matchPreparation: state.matchPreparation ?? null, lastMatchComment: state.lastMatchComment ?? null
  }
}
export function dayIntensity(date: string, state: TrainingState, locks: CalendarLock[]) {
  const locked = locks.filter(l => l.date === date)
  if (locked.some(l => l.kind === 'Match')) return 100
  const minutes = state.calendar.filter(c => c.date === date && !locked.some(l => l.slot === c.slot)).reduce((sum, c) => sum + c.intensity * c.minutes / 90, 0)
  return Math.min(100, Math.round(minutes + locked.reduce((sum, l) => sum + l.minutes * 50 / 90, 0)))
}
