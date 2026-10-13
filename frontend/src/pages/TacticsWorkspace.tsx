import { useMemo, useState } from 'react'

type Player = { playerId: number; firstName: string; lastName: string; dateOfBirth: string; preferredPosition: string; dominantFoot: string; coachNote?: string; shirtNumber?: number; isCaptain: boolean }
type MatchFormat = '11v11' | '9v9' | '8v8' | '7v7' | '5v5'
type Team = { matchFormat?: string; tacticFormation?: string; tacticNotes?: string; tacticPlanJson?: string; setPiecesJson?: string; opponentInstructions?: string }
type Props = { data: { team: Team; players: Player[] }; save: (value: unknown) => void }
type Slot = { id: string; x: number; y: number; label: string }
type Lineup = Record<string, number>
type RoleAssignments = Record<string, number[]>
type CornerPlans = Record<'attack' | 'defence', Lineup>
type CornerLayouts = Record<'attack' | 'defence', Slot[]>
type DrawingArrow = { id: string; x1: number; y1: number; x2: number; y2: number }
type SetPiecePlan = { slots: Slot[]; lineup: Lineup; arrows: DrawingArrow[]; notes: Record<string, string> }
type SetPiecePlans = Record<string, SetPiecePlan>
type TacticPhase = 'withBall' | 'transition' | 'withoutBall'
const CUSTOM_FORMATION = 'Своя схема'

const formations: Record<string, Slot[]> = {
  '4-3-3': [slot('gk', 50, 89, 'ВР'), slot('lb', 15, 69, 'ЛЗ'), slot('cb1', 37, 73, 'ЦЗ'), slot('cb2', 63, 73, 'ЦЗ'), slot('rb', 85, 69, 'ПЗ'), slot('cm1', 27, 49, 'ЦП'), slot('cm2', 50, 55, 'ЦП'), slot('cm3', 73, 49, 'ЦП'), slot('lw', 20, 24, 'ЛВ'), slot('st', 50, 15, 'НП'), slot('rw', 80, 24, 'ПВ')],
  '4-2-3-1': [slot('gk', 50, 89, 'ВР'), slot('lb', 15, 69, 'ЛЗ'), slot('cb1', 37, 73, 'ЦЗ'), slot('cb2', 63, 73, 'ЦЗ'), slot('rb', 85, 69, 'ПЗ'), slot('dm1', 37, 54, 'ОП'), slot('dm2', 63, 54, 'ОП'), slot('am1', 22, 34, 'ЛП'), slot('am2', 50, 31, 'ЦАП'), slot('am3', 78, 34, 'ПП'), slot('st', 50, 14, 'НП')],
  '4-4-2': [slot('gk', 50, 89, 'ВР'), slot('lb', 15, 69, 'ЛЗ'), slot('cb1', 37, 73, 'ЦЗ'), slot('cb2', 63, 73, 'ЦЗ'), slot('rb', 85, 69, 'ПЗ'), slot('lm', 17, 47, 'ЛП'), slot('cm1', 39, 50, 'ЦП'), slot('cm2', 61, 50, 'ЦП'), slot('rm', 83, 47, 'ПП'), slot('st1', 39, 17, 'НП'), slot('st2', 61, 17, 'НП')],
  '3-4-3': [slot('gk', 50, 89, 'ВР'), slot('cb1', 24, 73, 'ЦЗ'), slot('cb2', 50, 76, 'ЦЗ'), slot('cb3', 76, 73, 'ЦЗ'), slot('lm', 15, 49, 'ЛП'), slot('cm1', 39, 52, 'ЦП'), slot('cm2', 61, 52, 'ЦП'), slot('rm', 85, 49, 'ПП'), slot('lw', 20, 22, 'ЛВ'), slot('st', 50, 14, 'НП'), slot('rw', 80, 22, 'ПВ')],
  '3-5-2': [slot('gk', 50, 89, 'ВР'), slot('cb1', 24, 73, 'ЦЗ'), slot('cb2', 50, 76, 'ЦЗ'), slot('cb3', 76, 73, 'ЦЗ'), slot('lm', 13, 49, 'ЛП'), slot('cm1', 30, 53, 'ЦП'), slot('cm2', 50, 47, 'ЦП'), slot('cm3', 70, 53, 'ЦП'), slot('rm', 87, 49, 'ПП'), slot('st1', 39, 17, 'НП'), slot('st2', 61, 17, 'НП')]
}

const formationsByFormat: Record<MatchFormat, Record<string, Slot[]>> = {
  '11v11': formations,
  '9v9': {
    '3-3-2': [slot('gk', 50, 89, 'ВР'), slot('lb', 20, 70, 'ЛЗ'), slot('cb', 50, 74, 'ЦЗ'), slot('rb', 80, 70, 'ПЗ'), slot('lm', 20, 48, 'ЛП'), slot('cm', 50, 52, 'ЦП'), slot('rm', 80, 48, 'ПП'), slot('st1', 36, 19, 'НП'), slot('st2', 64, 19, 'НП')],
    '3-2-3': [slot('gk', 50, 89, 'ВР'), slot('lb', 20, 70, 'ЛЗ'), slot('cb', 50, 74, 'ЦЗ'), slot('rb', 80, 70, 'ПЗ'), slot('cm1', 36, 51, 'ЦП'), slot('cm2', 64, 51, 'ЦП'), slot('lw', 20, 25, 'ЛВ'), slot('st', 50, 16, 'НП'), slot('rw', 80, 25, 'ПВ')],
    '2-4-2': [slot('gk', 50, 89, 'ВР'), slot('cb1', 36, 73, 'ЦЗ'), slot('cb2', 64, 73, 'ЦЗ'), slot('lm', 16, 50, 'ЛП'), slot('cm1', 38, 53, 'ЦП'), slot('cm2', 62, 53, 'ЦП'), slot('rm', 84, 50, 'ПП'), slot('st1', 36, 19, 'НП'), slot('st2', 64, 19, 'НП')]
  },
  '8v8': {
    '3-3-1': [slot('gk', 50, 89, 'ВР'), slot('lb', 20, 70, 'ЛЗ'), slot('cb', 50, 74, 'ЦЗ'), slot('rb', 80, 70, 'ПЗ'), slot('lm', 20, 49, 'ЛП'), slot('cm', 50, 53, 'ЦП'), slot('rm', 80, 49, 'ПП'), slot('st', 50, 18, 'НП')],
    '2-3-2': [slot('gk', 50, 89, 'ВР'), slot('cb1', 35, 72, 'ЦЗ'), slot('cb2', 65, 72, 'ЦЗ'), slot('lm', 20, 49, 'ЛП'), slot('cm', 50, 54, 'ЦП'), slot('rm', 80, 49, 'ПП'), slot('st1', 36, 19, 'НП'), slot('st2', 64, 19, 'НП')],
    '3-2-2': [slot('gk', 50, 89, 'ВР'), slot('lb', 20, 70, 'ЛЗ'), slot('cb', 50, 74, 'ЦЗ'), slot('rb', 80, 70, 'ПЗ'), slot('cm1', 36, 51, 'ЦП'), slot('cm2', 64, 51, 'ЦП'), slot('st1', 36, 19, 'НП'), slot('st2', 64, 19, 'НП')]
  },
  '7v7': {
    '2-3-1': [slot('gk', 50, 89, 'ВР'), slot('cb1', 35, 72, 'ЦЗ'), slot('cb2', 65, 72, 'ЦЗ'), slot('lm', 20, 49, 'ЛП'), slot('cm', 50, 54, 'ЦП'), slot('rm', 80, 49, 'ПП'), slot('st', 50, 18, 'НП')],
    '3-2-1': [slot('gk', 50, 89, 'ВР'), slot('lb', 20, 70, 'ЛЗ'), slot('cb', 50, 74, 'ЦЗ'), slot('rb', 80, 70, 'ПЗ'), slot('cm1', 36, 51, 'ЦП'), slot('cm2', 64, 51, 'ЦП'), slot('st', 50, 18, 'НП')],
    '2-2-2': [slot('gk', 50, 89, 'ВР'), slot('cb1', 35, 72, 'ЦЗ'), slot('cb2', 65, 72, 'ЦЗ'), slot('cm1', 36, 51, 'ЦП'), slot('cm2', 64, 51, 'ЦП'), slot('st1', 36, 19, 'НП'), slot('st2', 64, 19, 'НП')]
  },
  '5v5': {
    '1-2-1': [slot('gk', 50, 90, 'ВР'), slot('fixo', 50, 69, 'Фиксо'), slot('alaLeft', 23, 47, 'Л. ала'), slot('alaRight', 77, 47, 'П. ала'), slot('pivot', 50, 23, 'Пивот')],
    '2-2': [slot('gk', 50, 90, 'ВР'), slot('backLeft', 30, 67, 'ЛЗ'), slot('backRight', 70, 67, 'ПЗ'), slot('frontLeft', 30, 29, 'ЛН'), slot('frontRight', 70, 29, 'ПН')],
    '3-1': [slot('gk', 50, 90, 'ВР'), slot('fixo', 50, 69, 'Фиксо'), slot('alaLeft', 23, 57, 'Л. ала'), slot('alaRight', 77, 57, 'П. ала'), slot('pivot', 50, 23, 'Пивот')]
  }
}
const normalizeMatchFormat = (format?: string): MatchFormat => format === '9v9' || format === '8v8' || format === '7v7' || format === '5v5' ? format : '11v11'
const formatLabel = (format: MatchFormat) => format === '5v5' ? 'Футзал · 5 × 5' : format.replace('v', ' × ')

const cornerSlots: Record<'attack' | 'defence', Slot[]> = {
  attack: [slot('taker', 8, 8, 'Подающий'), slot('near', 32, 14, 'Ближняя'), slot('far', 68, 15, 'Дальняя'), slot('penalty', 50, 24, '11 м'), slot('edge', 50, 39, 'Подбор'), slot('leftCover', 20, 48, 'Страховка'), slot('rightCover', 80, 48, 'Страховка'), slot('mid1', 36, 63, 'Баланс'), slot('mid2', 64, 63, 'Баланс'), slot('back', 50, 76, 'Назад'), slot('gk', 50, 90, 'ВР')],
  defence: [slot('taker', 8, 8, 'Подающий'), slot('near', 31, 16, 'Ближняя'), slot('zone1', 50, 15, 'Зона 1'), slot('zone2', 69, 16, 'Зона 2'), slot('mark1', 28, 34, 'Опека'), slot('mark2', 50, 34, 'Опека'), slot('mark3', 72, 34, 'Опека'), slot('edge', 50, 52, 'Подбор'), slot('leftOut', 22, 66, 'Выход'), slot('rightOut', 78, 66, 'Выход'), slot('gk', 50, 90, 'ВР')]
}

const freeKickSlots = [
  slot('taker', 50, 80, 'Исполнитель'), slot('wall1', 35, 43, 'Стенка'), slot('wall2', 43, 43, 'Стенка'), slot('wall3', 51, 43, 'Стенка'), slot('wall4', 59, 43, 'Стенка'),
  slot('near', 28, 28, 'Ближняя'), slot('far', 72, 28, 'Дальняя'), slot('rebound', 50, 33, 'Подбор'), slot('cover1', 20, 57, 'Страховка'), slot('cover2', 80, 57, 'Страховка'), slot('gk', 50, 10, 'ВР')
]
const throwSlots = [
  slot('thrower', 8, 54, 'Исполнитель'), slot('short', 25, 48, 'Коротко'), slot('mid', 42, 40, 'Поддержка'), slot('far', 63, 29, 'Дальняя'), slot('back', 30, 70, 'Назад'), slot('cover', 58, 66, 'Страховка'), slot('box1', 73, 41, 'Штрафная'), slot('box2', 83, 29, 'Штрафная'), slot('gk', 50, 10, 'ВР')
]
const setPieceDefaults: Record<string, Slot[]> = {
  cornerAttack: cornerSlots.attack,
  cornerDefence: cornerSlots.defence,
  freeKickDirect: freeKickSlots,
  freeKickLow: freeKickSlots,
  freeKickWide: freeKickSlots,
  freeKickDeep: freeKickSlots,
  throwAttack: throwSlots,
  throwDefence: throwSlots
}

const freeKickTypes = [
  ['direct', 'Прямой штрафной'], ['low', 'Прямой штрафной с низкой траекторией'], ['wide', 'Непрямой штрафной с фланга'], ['deep', 'Непрямой штрафной из глубины']
] as const
const freeKickRole = (type: string, side: 'Left' | 'Right') => `freeKick${type[0].toUpperCase()}${type.slice(1)}${side}`

function slot(id: string, x: number, y: number, label: string): Slot { return { id, x, y, label } }
function parseJson<T>(value: string | undefined, fallback: T): T { try { return value ? JSON.parse(value) : fallback } catch { return fallback } }
function asLineup(value: unknown): Lineup { return value && typeof value === 'object' && !Array.isArray(value) ? Object.fromEntries(Object.entries(value as Record<string, unknown>).filter(([, id]) => typeof id === 'number').map(([key, id]) => [key, id as number])) : {} }
function asRoleAssignments(value: unknown): RoleAssignments {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {}
  return Object.fromEntries(Object.entries(value as Record<string, unknown>).map(([key, ids]) => [key, Array.isArray(ids) ? ids.filter((id): id is number => typeof id === 'number') : typeof ids === 'number' ? [ids] : []]))
}
function asSlots(value: unknown): Slot[] {
  if (!Array.isArray(value)) return []
  return value.filter((item): item is Slot => Boolean(item) && typeof item === 'object' && typeof (item as Slot).id === 'string' && typeof (item as Slot).x === 'number' && typeof (item as Slot).y === 'number' && typeof (item as Slot).label === 'string')
}
function asDrawingArrows(value: unknown): DrawingArrow[] {
  if (!Array.isArray(value)) return []
  return value.filter((item): item is DrawingArrow => Boolean(item) && typeof item === 'object' && typeof (item as DrawingArrow).id === 'string' && ['x1', 'y1', 'x2', 'y2'].every(key => typeof (item as Record<string, unknown>)[key] === 'number'))
}
function asNotes(value: unknown): Record<string, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {}
  return Object.fromEntries(Object.entries(value as Record<string, unknown>).filter(([, note]) => typeof note === 'string')) as Record<string, string>
}

function asPhaseNotes(value: unknown): Record<TacticPhase, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return { withBall: '', transition: '', withoutBall: '' }
  const notes = value as Record<string, unknown>
  return {
    withBall: typeof notes.withBall === 'string' ? notes.withBall : '',
    transition: typeof notes.transition === 'string' ? notes.transition : '',
    withoutBall: typeof notes.withoutBall === 'string' ? notes.withoutBall : ''
  }
}

function playerAge(dateOfBirth: string) {
  const date = new Date(dateOfBirth)
  if (Number.isNaN(date.getTime())) return '—'
  const now = new Date()
  let age = now.getFullYear() - date.getFullYear()
  const beforeBirthday = now.getMonth() < date.getMonth() || now.getMonth() === date.getMonth() && now.getDate() < date.getDate()
  if (beforeBirthday) age -= 1
  return String(age)
}

const positionOrder = ['Вратарь', 'Защитник', 'Полузащитник', 'Нападающий']
const phaseCopy: Record<TacticPhase, { title: string; hint: string }> = {
  withBall: { title: 'С мячом', hint: 'Как строим атаку и создаём моменты.' },
  transition: { title: 'Переход мяча', hint: 'Действия сразу после отбора или потери.' },
  withoutBall: { title: 'Без мяча', hint: 'Оборона, прессинг и защита ворот.' }
}

export function TacticsWorkspace({ data, save }: Props) {
  const matchFormat = normalizeMatchFormat(data.team.matchFormat)
  const availableFormations = formationsByFormat[matchFormat]
  const fallbackFormation = Object.keys(availableFormations)[0]
  const initialPlan = useMemo(() => parseJson<unknown>(data.team.tacticPlanJson, {}), [data.team.tacticPlanJson])
  const initialSetPieces = useMemo(() => parseJson<unknown>(data.team.setPiecesJson, {}), [data.team.setPiecesJson])
  const storedPlan = initialPlan && typeof initialPlan === 'object' && !Array.isArray(initialPlan) ? initialPlan as Record<string, unknown> : {}
  const storedSetPieces = initialSetPieces && typeof initialSetPieces === 'object' && !Array.isArray(initialSetPieces) ? initialSetPieces as Record<string, unknown> : {}
  const [formation, setFormation] = useState(() => {
    const saved = String(storedPlan.formation || data.team.tacticFormation || fallbackFormation)
    return saved === CUSTOM_FORMATION || availableFormations[saved] ? saved : fallbackFormation
  })
  const [lineup, setLineup] = useState<Lineup>(() => asLineup(storedPlan.lineup ?? initialPlan))
  const [customSlots, setCustomSlots] = useState<Slot[]>(() => {
    const saved = asSlots(storedPlan.customSlots)
    return saved.length === Number(matchFormat.split('v')[0]) ? saved : availableFormations[fallbackFormation].map(position => ({ ...position }))
  })
  const [roles, setRoles] = useState<RoleAssignments>(() => {
    const saved = asRoleAssignments(storedSetPieces.roles ?? initialSetPieces)
    if (!saved.freeKickDirectLeft && saved.freeKickLeft) saved.freeKickDirectLeft = saved.freeKickLeft
    if (!saved.freeKickDirectRight && saved.freeKickRight) saved.freeKickDirectRight = saved.freeKickRight
    if (!saved.captain?.length) {
      const captain = data.players.find(player => player.isCaptain)
      if (captain) saved.captain = [captain.playerId]
    }
    return saved
  })
  const [corners] = useState<CornerPlans>(() => ({ attack: asLineup(storedSetPieces.corners && typeof storedSetPieces.corners === 'object' ? (storedSetPieces.corners as Record<string, unknown>).attack : {}), defence: asLineup(storedSetPieces.corners && typeof storedSetPieces.corners === 'object' ? (storedSetPieces.corners as Record<string, unknown>).defence : {}) }))
  const [cornerLayouts] = useState<CornerLayouts>(() => {
    const saved = storedSetPieces.cornerLayouts && typeof storedSetPieces.cornerLayouts === 'object' ? storedSetPieces.cornerLayouts as Record<string, unknown> : {}
    const attack = asSlots(saved.attack)
    const defence = asSlots(saved.defence)
    return { attack: attack.length ? attack : cornerSlots.attack.map(position => ({ ...position })), defence: defence.length ? defence : cornerSlots.defence.map(position => ({ ...position })) }
  })
  const [setPiecePlans, setSetPiecePlans] = useState<SetPiecePlans>(() => {
    const savedPlans = storedSetPieces.plans && typeof storedSetPieces.plans === 'object' && !Array.isArray(storedSetPieces.plans) ? storedSetPieces.plans as Record<string, unknown> : {}
    const legacyCorners = storedSetPieces.corners && typeof storedSetPieces.corners === 'object' ? storedSetPieces.corners as Record<string, unknown> : {}
    const legacyLayouts = storedSetPieces.cornerLayouts && typeof storedSetPieces.cornerLayouts === 'object' ? storedSetPieces.cornerLayouts as Record<string, unknown> : {}
    return Object.fromEntries(Object.entries(setPieceDefaults).map(([key, defaults]) => {
      const saved = savedPlans[key] && typeof savedPlans[key] === 'object' && !Array.isArray(savedPlans[key]) ? savedPlans[key] as Record<string, unknown> : {}
      const legacyMode = key === 'cornerAttack' ? 'attack' : key === 'cornerDefence' ? 'defence' : undefined
      const savedSlots = asSlots(saved.slots)
      const legacySlots = legacyMode ? asSlots(legacyLayouts[legacyMode]) : []
      let slots = (savedSlots.length ? savedSlots : legacySlots.length ? legacySlots : defaults).map(position => ({ ...position }))
      if (matchFormat === '5v5') {
        const goalkeeper = slots.find(position => position.id === 'gk') ?? slot('gk', 50, 90, 'ВР')
        slots = [...slots.filter(position => position.id !== 'gk').slice(0, 4), goalkeeper].map(position => ({ ...position, label: position.label === '11 м' ? '6 м' : position.label }))
      }
      const savedLineup = Object.keys(asLineup(saved.lineup)).length ? asLineup(saved.lineup) : legacyMode ? asLineup(legacyCorners[legacyMode]) : {}
      return [key, { slots, lineup: Object.fromEntries(Object.entries(savedLineup).filter(([slotId]) => slots.some(position => position.id === slotId))), arrows: asDrawingArrows(saved.arrows), notes: asNotes(saved.notes) }]
    })) as SetPiecePlans
  })
  const [activeTab, setActiveTab] = useState<'lineup' | 'standards' | 'captain' | 'match'>('lineup')
  const [standardsTab, setStandardsTab] = useState<'performers' | 'corners' | 'freeKicks' | 'throws'>('performers')
  const [freeKickType, setFreeKickType] = useState('direct')
  const [setPieceMode, setSetPieceMode] = useState<'attack' | 'defence'>('attack')
  const [selectedPlayerId, setSelectedPlayerId] = useState<number | null>(null)
  const [phaseNotes, setPhaseNotes] = useState<Record<TacticPhase, string>>(() => asPhaseNotes(storedPlan.phases))
  const [gamePlan, setGamePlan] = useState(() => data.team.tacticNotes ?? '')
  const [opponentInstructions, setOpponentInstructions] = useState(() => data.team.opponentInstructions ?? '')
  const playerById = (id?: number) => data.players.find(player => player.playerId === id)
  const sortedPlayers = [...data.players].sort((left, right) => (positionOrder.indexOf(left.preferredPosition) - positionOrder.indexOf(right.preferredPosition)) || left.lastName.localeCompare(right.lastName, 'ru'))
  const currentSlots = formation === CUSTOM_FORMATION && customSlots.length ? customSlots : availableFormations[formation] ?? availableFormations[fallbackFormation]
  const assign = (set: React.Dispatch<React.SetStateAction<Lineup>>, slotId: string, playerId: number) => set(current => ({ ...Object.fromEntries(Object.entries(current).filter(([id, value]) => id !== slotId && value !== playerId)), [slotId]: playerId }))
  const selectFormation = (next: string) => {
    const assignedPlayers = Object.values(lineup)
    if (next === CUSTOM_FORMATION) {
      const source = (customSlots.length ? customSlots : currentSlots).map(position => ({ ...position }))
      setCustomSlots(source)
      setFormation(next)
      setLineup(Object.fromEntries(source.map((position, index) => [position.id, assignedPlayers[index]]).filter(([, playerId]) => playerId != null)))
      return
    }
    setFormation(next)
    setLineup(Object.fromEntries((availableFormations[next] ?? availableFormations[fallbackFormation]).map((position, index) => [position.id, assignedPlayers[index]]).filter(([, playerId]) => playerId != null)))
  }
  const assignRole = (roleId: string, playerId: number, max: number) => setRoles(current => ({ ...current, [roleId]: max === 1 ? [playerId] : [...(current[roleId] ?? []).filter(id => id !== playerId), playerId].slice(-max) }))
  const removeRole = (roleId: string, playerId: number) => setRoles(current => ({ ...current, [roleId]: (current[roleId] ?? []).filter(id => id !== playerId) }))
  const updateSetPiecePlan = (key: string, update: (plan: SetPiecePlan) => SetPiecePlan) => setSetPiecePlans(current => ({ ...current, [key]: update(current[key] ?? { slots: (setPieceDefaults[key] ?? []).map(slot => ({ ...slot })), lineup: {}, arrows: [], notes: {} }) }))
  const handleDrop = (slotId: string, event: React.DragEvent<HTMLButtonElement>) => {
    event.stopPropagation()
    event.preventDefault()
    const playerId = Number(event.dataTransfer.getData('playerId'))
    if (!Number.isFinite(playerId) || !playerById(playerId)) return
    assign(setLineup, slotId, playerId)
    setSelectedPlayerId(null)
  }
  const moveCustomToken = (event: React.DragEvent<HTMLElement>) => {
    if (formation !== CUSTOM_FORMATION) return
    const slotId = event.dataTransfer.getData('slotId')
    if (!slotId) return
    event.preventDefault()
    const bounds = event.currentTarget.getBoundingClientRect()
    const x = Math.max(7, Math.min(93, (event.clientX - bounds.left) / bounds.width * 100))
    const y = Math.max(9, Math.min(91, (event.clientY - bounds.top) / bounds.height * 100))
    setCustomSlots(current => current.map(slot => slot.id === slotId ? { ...slot, x, y } : slot))
  }
  const clickSlot = (slotId: string) => {
    const selected = selectedPlayerId
    if (selected) { assign(setLineup, slotId, selected); setSelectedPlayerId(null); return }
    setLineup(current => { const next = { ...current }; delete next[slotId]; return next })
  }
  const submit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    save({ formation, notes: gamePlan, planJson: JSON.stringify({ formation, lineup, customSlots: formation === CUSTOM_FORMATION ? customSlots : undefined, phases: phaseNotes }), setPiecesJson: JSON.stringify({ roles, plans: setPiecePlans, corners: { attack: setPiecePlans.cornerAttack?.lineup ?? corners.attack, defence: setPiecePlans.cornerDefence?.lineup ?? corners.defence }, cornerLayouts: { attack: setPiecePlans.cornerAttack?.slots ?? cornerLayouts.attack, defence: setPiecePlans.cornerDefence?.slots ?? cornerLayouts.defence } }), opponentInstructions })
  }

  return <form className="tactics-workspace" onSubmit={submit}>
    <nav className="tactic-tabs" aria-label="Разделы тактики">
      <button type="button" className={activeTab === 'lineup' ? 'active' : ''} onClick={() => setActiveTab('lineup')}>Расстановка</button>
      <button type="button" className={activeTab === 'standards' ? 'active' : ''} onClick={() => setActiveTab('standards')}>Стандарты</button>
      <button type="button" className={activeTab === 'captain' ? 'active' : ''} onClick={() => setActiveTab('captain')}>Капитан</button>
      <button type="button" className={activeTab === 'match' ? 'active' : ''} onClick={() => setActiveTab('match')}>План на игру</button>
    </nav>

    {activeTab === 'lineup' && <section className="tactics-command">
      <section className="tactical-board">
        <header className="pitch-toolbar"><div><span className="eyebrow">Стартовый состав · {formatLabel(matchFormat)}</span><h2>{formation}</h2></div><label>Схема<select value={formation} onChange={event => selectFormation(event.target.value)}>{Object.keys(availableFormations).map(item => <option key={item}>{item}</option>)}<option>{CUSTOM_FORMATION}</option></select></label></header>
        {formation === CUSTOM_FORMATION && <p className="custom-formation-note">Своя схема: перетаскивайте фишки с игроками в любую точку поля.</p>}
        <article className={`card tactical-pitch format-${matchFormat}`} onDragOver={event => formation === CUSTOM_FORMATION && event.preventDefault()} onDrop={moveCustomToken}><Pitch slots={currentSlots} lineup={lineup} playerById={playerById} selectedPlayerId={selectedPlayerId} onDrop={slotId => event => handleDrop(slotId, event)} onClick={slotId => () => clickSlot(slotId)} /></article>
      </section>
      <aside className="card tactic-squad"><div className="card-heading"><div><h2>Игроки</h2><p>Нажмите на игрока и фишку или перетащите его на поле.</p></div><span className="count-chip">{data.players.length}</span></div>{sortedPlayers.map(player => <button type="button" draggable key={player.playerId} className={selectedPlayerId === player.playerId ? 'active' : ''} onClick={() => setSelectedPlayerId(current => current === player.playerId ? null : player.playerId)} onDragStart={event => event.dataTransfer.setData('playerId', String(player.playerId))}><b>{player.shirtNumber || '—'}</b><span><strong>{player.lastName}</strong><small>{player.firstName} · {player.preferredPosition} · {playerAge(player.dateOfBirth)} лет</small></span></button>)}</aside>
      <aside className="card phase-notes"><div className="card-heading"><div><span className="eyebrow">Тактические акценты</span><h2>Модель игры</h2></div></div>{(Object.keys(phaseCopy) as TacticPhase[]).map(phase => <label key={phase}><strong>{phaseCopy[phase].title}</strong><small>{phaseCopy[phase].hint}</small><textarea value={phaseNotes[phase]} onChange={event => setPhaseNotes(current => ({ ...current, [phase]: event.target.value }))} placeholder="Добавить установку…" /></label>)}</aside>
    </section>}

    {activeTab === 'standards' && <section className="standards-workspace">
      <nav className="segment-control tactic-subtabs" aria-label="Разделы стандартов"><button type="button" className={standardsTab === 'performers' ? 'active' : ''} onClick={() => setStandardsTab('performers')}>Исполнители</button><button type="button" className={standardsTab === 'corners' ? 'active' : ''} onClick={() => setStandardsTab('corners')}>Угловые</button><button type="button" className={standardsTab === 'freeKicks' ? 'active' : ''} onClick={() => setStandardsTab('freeKicks')}>Штрафные</button><button type="button" className={standardsTab === 'throws' ? 'active' : ''} onClick={() => setStandardsTab('throws')}>Ауты</button></nav>
      {standardsTab === 'performers' && <StandardPerformerBoard players={sortedPlayers} roles={roles} playerById={playerById} onAssign={assignRole} onRemove={removeRole} freeKickType={freeKickType} setFreeKickType={setFreeKickType} />}
      {standardsTab === 'corners' && <SetPiecePlanner title="Угловые" description="Расставьте игроков, затем при необходимости проведите стрелки движения." matchFormat={matchFormat} plan={setPiecePlans[`corner${setPieceMode === 'attack' ? 'Attack' : 'Defence'}`]} players={sortedPlayers} playerById={playerById} selectedPlayerId={selectedPlayerId} setSelectedPlayerId={setSelectedPlayerId} onChange={update => updateSetPiecePlan(`corner${setPieceMode === 'attack' ? 'Attack' : 'Defence'}`, update)} controls={<ScenarioSwitch value={setPieceMode} onChange={setSetPieceMode} />} />}
      {standardsTab === 'freeKicks' && <SetPiecePlanner title="Штрафные" description="Задайте расстановку на вертикальном поле, движение стрелками и индивидуальные указания." matchFormat={matchFormat} plan={setPiecePlans[`freeKick${freeKickType[0].toUpperCase()}${freeKickType.slice(1)}`]} players={sortedPlayers} playerById={playerById} selectedPlayerId={selectedPlayerId} setSelectedPlayerId={setSelectedPlayerId} onChange={update => updateSetPiecePlan(`freeKick${freeKickType[0].toUpperCase()}${freeKickType.slice(1)}`, update)} controls={<label>Тип эпизода<select value={freeKickType} onChange={event => setFreeKickType(event.target.value)}>{freeKickTypes.map(([id, label]) => <option value={id} key={id}>{label}</option>)}</select></label>} />}
      {standardsTab === 'throws' && <SetPiecePlanner title="Ауты" description="Выберите фазу, поставьте исполнителя и добавьте движения для обороны или атаки." matchFormat={matchFormat} plan={setPiecePlans[`throw${setPieceMode === 'attack' ? 'Attack' : 'Defence'}`]} players={sortedPlayers} playerById={playerById} selectedPlayerId={selectedPlayerId} setSelectedPlayerId={setSelectedPlayerId} onChange={update => updateSetPiecePlan(`throw${setPieceMode === 'attack' ? 'Attack' : 'Defence'}`, update)} controls={<ScenarioSwitch value={setPieceMode} onChange={setSetPieceMode} />} />}
    </section>}

    {activeTab === 'captain' && <section className="card captain-workspace"><div><span className="eyebrow">Лидер команды</span><h2>Капитан</h2><p>Капитан выводится в состав и может быть изменён перед матчем.</p></div><RoleAssignment label="Капитан команды" max={1} players={sortedPlayers} selected={roles.captain ?? []} playerById={playerById} onAssign={playerId => assignRole('captain', playerId, 1)} onRemove={playerId => removeRole('captain', playerId)} /></section>}

    {activeTab === 'match' && <section className="card tactic-settings"><label>План на игру<textarea value={gamePlan} onChange={event => setGamePlan(event.target.value)} placeholder="Принципы игры, приоритеты и замены." /></label><label>Инструкции по сопернику<textarea value={opponentInstructions} onChange={event => setOpponentInstructions(event.target.value)} placeholder="Кого и где прессингуем, слабые зоны соперника." /></label></section>}

    <div className="tactics-save-bar"><span>Изменения сохраняются одной кнопкой.</span><button className="button">Сохранить тактику</button></div>
  </form>
}

function StandardPerformerBoard({ players, roles, playerById, onAssign, onRemove, freeKickType, setFreeKickType }: { players: Player[]; roles: RoleAssignments; playerById: (id?: number) => Player | undefined; onAssign: (roleId: string, playerId: number, max: number) => void; onRemove: (roleId: string, playerId: number) => void; freeKickType: string; setFreeKickType: (value: string) => void }) {
  const freeKickLabel = freeKickTypes.find(([id]) => id === freeKickType)?.[1] ?? 'Прямой штрафной'
  return <section className="standard-performers">
    <article className="card standard-player-list"><div className="card-heading"><div><span className="eyebrow">Игроки</span><h2>Доступный состав</h2></div><span className="count-chip">{players.length}</span></div><div className="performer-table-head"><span>Позиция</span><span>Игрок</span><span>Нога</span><span>Заметка тренера</span></div>{players.map(player => <button type="button" draggable className="performer-candidate" key={player.playerId} onDragStart={event => event.dataTransfer.setData('playerId', String(player.playerId))}><span>{player.preferredPosition}</span><strong>{player.firstName} {player.lastName}</strong><span>{player.dominantFoot || '—'}</span><small>{player.coachNote || 'Нет заметки'}</small></button>)}</article>
    <article className="card standard-appointments"><div className="card-heading"><div><span className="eyebrow">Назначения</span><h2>Исполнители стандартов</h2></div></div><div className="appointment-section"><h3>Угловые</h3><div className="standard-side-grid"><StandardAssignmentGroup title="Слева" roleId="cornerLeft" max={4} selected={roles.cornerLeft ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /><StandardAssignmentGroup title="Справа" roleId="cornerRight" max={4} selected={roles.cornerRight ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /></div></div><div className="appointment-section"><div className="appointment-heading"><h3>Штрафные</h3><select aria-label="Тип штрафного" value={freeKickType} onChange={event => setFreeKickType(event.target.value)}>{freeKickTypes.map(([id, label]) => <option value={id} key={id}>{label}</option>)}</select></div><p>{freeKickLabel}</p><div className="standard-side-grid"><StandardAssignmentGroup title="Слева" roleId={freeKickRole(freeKickType, 'Left')} max={3} selected={roles[freeKickRole(freeKickType, 'Left')] ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /><StandardAssignmentGroup title="Справа" roleId={freeKickRole(freeKickType, 'Right')} max={3} selected={roles[freeKickRole(freeKickType, 'Right')] ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /></div></div><div className="appointment-section"><h3>Ауты</h3><div className="standard-side-grid"><StandardAssignmentGroup title="Слева" roleId="throwLeft" max={3} selected={roles.throwLeft ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /><StandardAssignmentGroup title="Справа" roleId="throwRight" max={3} selected={roles.throwRight ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /></div></div><div className="appointment-section"><h3>Пенальти</h3><StandardAssignmentGroup title="Очередность" roleId="penalty" max={3} selected={roles.penalty ?? []} playerById={playerById} onAssign={onAssign} onRemove={onRemove} /></div></article>
  </section>
}

function StandardAssignmentGroup({ title, roleId, max, selected, playerById, onAssign, onRemove }: { title: string; roleId: string; max: number; selected: number[]; playerById: (id?: number) => Player | undefined; onAssign: (roleId: string, playerId: number, max: number) => void; onRemove: (roleId: string, playerId: number) => void }) {
  const drop = (event: React.DragEvent<HTMLElement>) => { event.preventDefault(); const playerId = Number(event.dataTransfer.getData('playerId')); if (Number.isFinite(playerId) && playerById(playerId) && !selected.includes(playerId)) onAssign(roleId, playerId, max) }
  return <section className="standard-assignment" onDragOver={event => event.preventDefault()} onDrop={drop}><h4>{title}</h4>{Array.from({ length: max }, (_, index) => { const playerId = selected[index]; const player = playerById(playerId); return <div className={player ? 'appointment-row assigned' : 'appointment-row'} key={index}>{player ? <><span>{index + 1}</span><strong>{player.firstName} {player.lastName}</strong><button type="button" aria-label={`Убрать ${player.firstName} ${player.lastName}`} onClick={() => onRemove(roleId, playerId)}>×</button></> : <><span>{index + 1}</span><em>Перетащите игрока сюда</em></>}</div> })}</section>
}

function ScenarioSwitch({ value, onChange }: { value: 'attack' | 'defence'; onChange: (value: 'attack' | 'defence') => void }) {
  return <div className="segment-control"><button type="button" className={value === 'attack' ? 'active' : ''} onClick={() => onChange('attack')}>Атака</button><button type="button" className={value === 'defence' ? 'active' : ''} onClick={() => onChange('defence')}>Оборона</button></div>
}

function SetPiecePlanner({ title, description, controls, matchFormat, plan, players, playerById, selectedPlayerId, setSelectedPlayerId, onChange }: { title: string; description: string; controls: React.ReactNode; matchFormat: MatchFormat; plan: SetPiecePlan; players: Player[]; playerById: (id?: number) => Player | undefined; selectedPlayerId: number | null; setSelectedPlayerId: React.Dispatch<React.SetStateAction<number | null>>; onChange: (update: (plan: SetPiecePlan) => SetPiecePlan) => void }) {
  const [tool, setTool] = useState<'move' | 'draw' | 'erase'>('move')
  const [draft, setDraft] = useState<DrawingArrow | null>(null)
  const assignedPlayerIds = [...new Set(Object.values(plan.lineup))]
  const point = (event: React.PointerEvent<HTMLDivElement>) => {
    const bounds = event.currentTarget.getBoundingClientRect()
    return { x: Math.max(0, Math.min(100, (event.clientX - bounds.left) / bounds.width * 100)), y: Math.max(0, Math.min(100, (event.clientY - bounds.top) / bounds.height * 100)) }
  }
  const assign = (slotId: string, playerId: number) => onChange(current => ({ ...current, lineup: { ...Object.fromEntries(Object.entries(current.lineup).filter(([id, value]) => id !== slotId && value !== playerId)), [slotId]: playerId } }))
  const beginDrawing = (event: React.PointerEvent<HTMLDivElement>) => {
    if (tool !== 'draw' || (event.target as HTMLElement).closest('button')) return
    const start = point(event)
    event.currentTarget.setPointerCapture(event.pointerId)
    setDraft({ id: 'draft', x1: start.x, y1: start.y, x2: start.x, y2: start.y })
  }
  const draw = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!draft) return
    const next = point(event)
    setDraft(current => current ? { ...current, x2: next.x, y2: next.y } : null)
  }
  const finishDrawing = () => {
    if (!draft) return
    const distance = Math.hypot(draft.x2 - draft.x1, draft.y2 - draft.y1)
    if (distance > 2) onChange(current => ({ ...current, arrows: [...current.arrows, { ...draft, id: `${Date.now()}-${Math.random().toString(36).slice(2)}` }] }))
    setDraft(null)
  }
  const moveSlot = (event: React.DragEvent<HTMLDivElement>) => {
    const slotId = event.dataTransfer.getData('slotId')
    if (!slotId) return
    event.preventDefault()
    const bounds = event.currentTarget.getBoundingClientRect()
    const x = Math.max(7, Math.min(93, (event.clientX - bounds.left) / bounds.width * 100))
    const y = Math.max(9, Math.min(91, (event.clientY - bounds.top) / bounds.height * 100))
    onChange(current => ({ ...current, slots: current.slots.map(slot => slot.id === slotId ? { ...slot, x, y } : slot) }))
  }
  return <section className="set-piece-planner">
    <article className="card set-piece-board"><div className="card-heading"><div><span className="eyebrow">{title}</span><h2>Расстановка на стандарт</h2></div><div className="set-piece-controls">{controls}</div></div><p>{description}</p><div className="drawing-tools" aria-label="Инструменты доски"><button type="button" className={tool === 'move' ? 'active' : ''} onClick={() => setTool('move')}>Расстановка</button><button type="button" className={tool === 'draw' ? 'active' : ''} onClick={() => setTool('draw')}>Нарисовать стрелку</button><button type="button" className={tool === 'erase' ? 'active' : ''} onClick={() => setTool('erase')}>Ластик</button><button type="button" onClick={() => onChange(current => ({ ...current, arrows: [] }))} disabled={!plan.arrows.length}>Стереть все</button></div><div className={`tactical-pitch set-piece-pitch set-piece-drawing format-${matchFormat} ${tool}`} onDragOver={event => event.preventDefault()} onDrop={moveSlot} onPointerDown={beginDrawing} onPointerMove={draw} onPointerUp={finishDrawing} onPointerCancel={finishDrawing}><svg className="drawing-layer" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true"><defs><marker id="set-piece-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto"><path d="M0,0 L7,3.5 L0,7 Z" /></marker></defs>{[...plan.arrows, ...(draft ? [draft] : [])].map(arrow => <line key={arrow.id} x1={arrow.x1} y1={arrow.y1} x2={arrow.x2} y2={arrow.y2} markerEnd="url(#set-piece-arrow)" className="tactic-arrow" onPointerDown={event => { if (tool === 'erase' && arrow.id !== 'draft') { event.stopPropagation(); onChange(current => ({ ...current, arrows: current.arrows.filter(item => item.id !== arrow.id) })) } }} />)}</svg>{plan.slots.map(slot => { const player = playerById(plan.lineup[slot.id]); return <button type="button" draggable key={slot.id} aria-label={player ? `${player.firstName} ${player.lastName}, номер ${player.shirtNumber ?? 'не назначен'}` : `Назначить игрока: ${slot.label}`} className={selectedPlayerId && !player ? 'ready' : ''} style={{ left: `${slot.x}%`, top: `${slot.y}%` }} onDragStart={event => { event.dataTransfer.setData('slotId', slot.id); if (player) event.dataTransfer.setData('playerId', String(player.playerId)) }} onDragOver={event => event.preventDefault()} onDrop={event => { event.stopPropagation(); event.preventDefault(); const playerId = Number(event.dataTransfer.getData('playerId')); if (Number.isFinite(playerId) && playerById(playerId)) { assign(slot.id, playerId); setSelectedPlayerId(null) } }} onClick={() => { if (tool !== 'move') return; if (selectedPlayerId) { assign(slot.id, selectedPlayerId); setSelectedPlayerId(null) } else if (player) onChange(current => ({ ...current, lineup: Object.fromEntries(Object.entries(current.lineup).filter(([id]) => id !== slot.id)) })) }}><strong className="pitch-token-number">{player ? player.shirtNumber ?? '—' : '＋'}</strong></button> })}</div></article>
    <aside className="card set-piece-squad"><div className="card-heading"><div><span className="eyebrow">Состав</span><h2>Игроки</h2><p>Выберите игрока и фишку или перетащите его на поле.</p></div><span className="count-chip">{players.length}</span></div>{players.map(player => <button type="button" draggable key={player.playerId} className={selectedPlayerId === player.playerId ? 'active' : ''} onClick={() => setSelectedPlayerId(current => current === player.playerId ? null : player.playerId)} onDragStart={event => event.dataTransfer.setData('playerId', String(player.playerId))}><b>{player.shirtNumber || '—'}</b><span><strong>{player.lastName}</strong><small>{player.firstName} · {player.preferredPosition} · {playerAge(player.dateOfBirth)} лет</small></span></button>)}</aside>
    <aside className="card set-piece-instructions"><div className="card-heading"><div><span className="eyebrow">Указания</span><h2>Заметки игрокам</h2><p>Сохраняются вместе с расстановкой и стрелками.</p></div></div>{assignedPlayerIds.length ? assignedPlayerIds.map(playerId => { const player = playerById(playerId); return player ? <label key={playerId}><strong>{player.firstName} {player.lastName}</strong><textarea value={plan.notes[String(playerId)] ?? ''} onChange={event => { const value = event.target.value; onChange(current => ({ ...current, notes: { ...current.notes, [String(playerId)]: value } })) }} placeholder="Добавить указание игроку…" /></label> : null }) : <p className="empty-set-piece-note">Сначала поставьте игроков на поле.</p>}</aside>
  </section>
}

function Pitch({ slots, lineup, playerById, selectedPlayerId, onDrop, onClick }: { slots: Slot[]; lineup: Lineup; playerById: (id?: number) => Player | undefined; selectedPlayerId: number | null; onDrop: (slotId: string) => (event: React.DragEvent<HTMLButtonElement>) => void; onClick: (slotId: string) => () => void }) {
  return <><span className="pitch-goal pitch-goal-top" aria-hidden="true" /><span className="pitch-goal pitch-goal-bottom" aria-hidden="true" />{slots.map(slot => { const player = playerById(lineup[slot.id]); return <button type="button" draggable key={slot.id} aria-label={player ? `${player.firstName} ${player.lastName}, номер ${player.shirtNumber ?? 'не назначен'}` : `Назначить игрока: ${slot.label}`} className={selectedPlayerId && !player ? 'ready' : ''} style={{ left: `${slot.x}%`, top: `${slot.y}%` }} onDragStart={event => { if (player) event.dataTransfer.setData('playerId', String(player.playerId)); event.dataTransfer.setData('slotId', slot.id) }} onDragOver={event => event.preventDefault()} onDrop={onDrop(slot.id)} onClick={onClick(slot.id)}><strong className="pitch-token-number">{player ? player.shirtNumber ?? '—' : '＋'}</strong></button> })}</>
}

function RoleAssignment({ label, max, players, selected, playerById, onAssign, onRemove }: { label: string; max: number; players: Player[]; selected: number[]; playerById: (id?: number) => Player | undefined; onAssign: (playerId: number) => void; onRemove: (playerId: number) => void }) {
  return <div className="role-assignment"><strong>{label}</strong><div className="role-chips">{selected.length ? selected.map(playerId => <button type="button" key={playerId} onClick={() => onRemove(playerId)}>{playerById(playerId)?.shirtNumber || '—'} · {playerById(playerId)?.lastName} ×</button>) : <span>Не назначен</span>}</div><select aria-label={label} value="" disabled={selected.length >= max} onChange={event => { const playerId = Number(event.target.value); if (playerId) onAssign(playerId) }}><option value="">{selected.length >= max ? `Выбрано ${max}` : 'Добавить игрока'}</option>{players.filter(player => !selected.includes(player.playerId)).map(player => <option value={player.playerId} key={player.playerId}>{player.shirtNumber || '—'} · {player.lastName}</option>)}</select></div>
}
