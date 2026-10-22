export type KeeperTeam = { teamId: number; schoolId: number; school: string; name: string; coach: string }
export type KeeperExercise = { id: number; title: string; category: string; description: string; equipment: string; durationMinutes: number; isActive: boolean }
export type KeeperPlayer = { playerId: number; firstName: string; lastName: string; dateOfBirth: string; preferredPosition: string; dominantFoot: string; height?: number; weight?: number; shirtNumber?: number; record?: { goal: string; notes: string; version: number }; sessions: number; attended: number }
export type KeeperStep = { exerciseId: number | null; title: string; stage: string; durationMinutes: number; instructions: string }
export type KeeperParticipant = { playerId: number; attendance: string; rating: number | null; notes: string }
export type KeeperPlan = { title: string; scheduledAt: string; venue: string; objective: string; intensity: string; plan: KeeperStep[]; players: KeeperParticipant[]; notes: string; version: number; status: string }
export type KeeperSession = { id: number; title: string; scheduledAt: string; durationMinutes: number; status: string; version: number; completedAt?: string; players: number }
export type KeeperDetail = { id: number; teamId: number; status: string; version: number; state: KeeperPlan; players: { playerId: number; firstName: string; lastName: string }[] }
export type KeeperWorkspace = { players: KeeperPlayer[]; exercises: KeeperExercise[]; trainings: KeeperSession[]; matches: { id: number; opponent: string; scheduledAt: string; venue: string }[] }
export const keeperStatus: Record<string, string> = { Planned: 'Запланирована', InProgress: 'Идёт занятие', Completed: 'Завершена', Cancelled: 'Отменена' }
export const keeperAttendance: Record<string, string> = { Unknown: 'Не отмечен', Present: 'Присутствует', Late: 'Опоздал', Absent: 'Отсутствует', Excused: 'Уважительная причина' }
export const keeperStages = ['Разминка', 'Основная часть', 'Игровая часть', 'Заминка']
export const keeperCategories = ['Техника', 'Ловля мяча', 'Игра ногами', 'Реакция', 'Выходы', 'Игра один в один', 'Координация', 'Тактика', 'Физическая подготовка']
export const keeperName = (player: { firstName: string; lastName: string }) => `${player.firstName} ${player.lastName}`
export function keeperDate(value: Date) { return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}` }
export function keeperLocalTime(value: string) { const date = new Date(value); return `${keeperDate(date)}T${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}` }
export function keeperCalendarDays(value: string, mode: 'month' | 'week') {
  const date = new Date(`${value}T12:00:00`)
  if (mode === 'month') date.setDate(1)
  date.setDate(date.getDate() - (date.getDay() + 6) % 7)
  const count = mode === 'week' ? 7 : Math.ceil(((new Date(`${value.slice(0, 7)}-01T12:00:00`).getDay() + 6) % 7 + new Date(Number(value.slice(0, 4)), Number(value.slice(5, 7)), 0).getDate()) / 7) * 7
  return Array.from({ length: count }, (_, index) => { const day = new Date(date); day.setDate(day.getDate() + index); return keeperDate(day) })
}
export function newKeeperPlan(date: string, players: KeeperPlayer[]): KeeperPlan {
  return { title: 'Тренировка вратарей', scheduledAt: `${date}T18:00`, venue: '', objective: '', intensity: 'Средняя', plan: [], players: players.map(p => ({ playerId: p.playerId, attendance: 'Unknown', rating: null, notes: '' })), notes: '', version: 0, status: 'Planned' }
}
