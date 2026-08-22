import { useMemo, useState } from 'react'

type Player = { playerId: number; firstName: string; lastName: string; preferredPosition: string; shirtNumber?: number }
type Team = { tacticFormation?: string; tacticNotes?: string; tacticPlanJson?: string; setPiecesJson?: string; opponentInstructions?: string }
type Props = { data: { team: Team; players: Player[] }; save: (value: unknown) => void }
type Slot = { id: string; x: number; y: number; label: string }
type Lineup = Record<string, number>
type RoleAssignments = Record<string, number[]>
type CornerPlans = Record<'attack' | 'defence', Lineup>
const CUSTOM_FORMATION = 'Своя схема'

const formations: Record<string, Slot[]> = {
  '4-3-3': [slot('gk', 50, 89, 'ВР'), slot('lb', 15, 69, 'ЛЗ'), slot('cb1', 37, 73, 'ЦЗ'), slot('cb2', 63, 73, 'ЦЗ'), slot('rb', 85, 69, 'ПЗ'), slot('cm1', 27, 49, 'ЦП'), slot('cm2', 50, 55, 'ЦП'), slot('cm3', 73, 49, 'ЦП'), slot('lw', 20, 24, 'ЛВ'), slot('st', 50, 15, 'НП'), slot('rw', 80, 24, 'ПВ')],
  '4-2-3-1': [slot('gk', 50, 89, 'ВР'), slot('lb', 15, 69, 'ЛЗ'), slot('cb1', 37, 73, 'ЦЗ'), slot('cb2', 63, 73, 'ЦЗ'), slot('rb', 85, 69, 'ПЗ'), slot('dm1', 37, 54, 'ОП'), slot('dm2', 63, 54, 'ОП'), slot('am1', 22, 34, 'ЛП'), slot('am2', 50, 31, 'ЦАП'), slot('am3', 78, 34, 'ПП'), slot('st', 50, 14, 'НП')],
  '4-4-2': [slot('gk', 50, 89, 'ВР'), slot('lb', 15, 69, 'ЛЗ'), slot('cb1', 37, 73, 'ЦЗ'), slot('cb2', 63, 73, 'ЦЗ'), slot('rb', 85, 69, 'ПЗ'), slot('lm', 17, 47, 'ЛП'), slot('cm1', 39, 50, 'ЦП'), slot('cm2', 61, 50, 'ЦП'), slot('rm', 83, 47, 'ПП'), slot('st1', 39, 17, 'НП'), slot('st2', 61, 17, 'НП')],
  '3-4-3': [slot('gk', 50, 89, 'ВР'), slot('cb1', 24, 73, 'ЦЗ'), slot('cb2', 50, 76, 'ЦЗ'), slot('cb3', 76, 73, 'ЦЗ'), slot('lm', 15, 49, 'ЛП'), slot('cm1', 39, 52, 'ЦП'), slot('cm2', 61, 52, 'ЦП'), slot('rm', 85, 49, 'ПП'), slot('lw', 20, 22, 'ЛВ'), slot('st', 50, 14, 'НП'), slot('rw', 80, 22, 'ПВ')],
  '3-5-2': [slot('gk', 50, 89, 'ВР'), slot('cb1', 24, 73, 'ЦЗ'), slot('cb2', 50, 76, 'ЦЗ'), slot('cb3', 76, 73, 'ЦЗ'), slot('lm', 13, 49, 'ЛП'), slot('cm1', 30, 53, 'ЦП'), slot('cm2', 50, 47, 'ЦП'), slot('cm3', 70, 53, 'ЦП'), slot('rm', 87, 49, 'ПП'), slot('st1', 39, 17, 'НП'), slot('st2', 61, 17, 'НП')]
}

const cornerSlots: Record<'attack' | 'defence', Slot[]> = {
  attack: [slot('taker', 8, 8, 'Подающий'), slot('near', 32, 14, 'Ближняя'), slot('far', 68, 15, 'Дальняя'), slot('penalty', 50, 24, '11 м'), slot('edge', 50, 39, 'Подбор'), slot('leftCover', 20, 48, 'Страховка'), slot('rightCover', 80, 48, 'Страховка'), slot('mid1', 36, 63, 'Баланс'), slot('mid2', 64, 63, 'Баланс'), slot('back', 50, 76, 'Назад'), slot('gk', 50, 90, 'ВР')],
  defence: [slot('taker', 8, 8, 'Подающий'), slot('near', 31, 16, 'Ближняя'), slot('zone1', 50, 15, 'Зона 1'), slot('zone2', 69, 16, 'Зона 2'), slot('mark1', 28, 34, 'Опека'), slot('mark2', 50, 34, 'Опека'), slot('mark3', 72, 34, 'Опека'), slot('edge', 50, 52, 'Подбор'), slot('leftOut', 22, 66, 'Выход'), slot('rightOut', 78, 66, 'Выход'), slot('gk', 50, 90, 'ВР')]
}

const roleDefinitions = [
  ['captain', 'Капитан', 1], ['penalty', 'Пенальти', 1],
  ['freeKickLeft', 'Штрафные · слева', 2], ['freeKickRight', 'Штрафные · справа', 2],
  ['cornerLeft', 'Угловые · слева', 2], ['cornerRight', 'Угловые · справа', 2],
  ['throwLeft', 'Ауты · слева', 2], ['throwRight', 'Ауты · справа', 2]
] as const

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

export function TacticsWorkspace({ data, save }: Props) {
  const initialPlan = useMemo(() => parseJson<unknown>(data.team.tacticPlanJson, {}), [data.team.tacticPlanJson])
  const initialSetPieces = useMemo(() => parseJson<unknown>(data.team.setPiecesJson, {}), [data.team.setPiecesJson])
  const storedPlan = initialPlan && typeof initialPlan === 'object' && !Array.isArray(initialPlan) ? initialPlan as Record<string, unknown> : {}
  const storedSetPieces = initialSetPieces && typeof initialSetPieces === 'object' && !Array.isArray(initialSetPieces) ? initialSetPieces as Record<string, unknown> : {}
  const [formation, setFormation] = useState(() => String(storedPlan.formation || data.team.tacticFormation || '4-3-3'))
  const [lineup, setLineup] = useState<Lineup>(() => asLineup(storedPlan.lineup ?? initialPlan))
  const [customSlots, setCustomSlots] = useState<Slot[]>(() => {
    const saved = asSlots(storedPlan.customSlots)
    return saved.length || storedPlan.formation !== CUSTOM_FORMATION ? saved : formations['4-3-3'].map(position => ({ ...position }))
  })
  const [roles, setRoles] = useState<RoleAssignments>(() => asRoleAssignments(storedSetPieces.roles ?? initialSetPieces))
  const [corners, setCorners] = useState<CornerPlans>(() => ({ attack: asLineup(storedSetPieces.corners && typeof storedSetPieces.corners === 'object' ? (storedSetPieces.corners as Record<string, unknown>).attack : {}), defence: asLineup(storedSetPieces.corners && typeof storedSetPieces.corners === 'object' ? (storedSetPieces.corners as Record<string, unknown>).defence : {}) }))
  const [cornerMode, setCornerMode] = useState<'attack' | 'defence'>('attack')
  const [selectedPlayerId, setSelectedPlayerId] = useState<number | null>(null)
  const [validationMessage, setValidationMessage] = useState<string | null>(null)
  const playerById = (id?: number) => data.players.find(player => player.playerId === id)
  const currentSlots = formation === CUSTOM_FORMATION && customSlots.length ? customSlots : formations[formation] ?? formations['4-3-3']
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
    setLineup(Object.fromEntries((formations[next] ?? formations['4-3-3']).map((position, index) => [position.id, assignedPlayers[index]]).filter(([, playerId]) => playerId != null)))
  }
  const assignRole = (roleId: string, playerId: number, max: number) => setRoles(current => ({ ...current, [roleId]: max === 1 ? [playerId] : [...(current[roleId] ?? []).filter(id => id !== playerId), playerId].slice(-max) }))
  const removeRole = (roleId: string, playerId: number) => setRoles(current => ({ ...current, [roleId]: (current[roleId] ?? []).filter(id => id !== playerId) }))
  const handleDrop = (target: 'lineup' | 'corner', slotId: string, event: React.DragEvent<HTMLButtonElement>) => {
    event.stopPropagation()
    event.preventDefault()
    const playerId = Number(event.dataTransfer.getData('playerId'))
    if (!Number.isFinite(playerId) || !playerById(playerId)) return
    if (target === 'lineup') assign(setLineup, slotId, playerId)
    else setCorners(current => ({ ...current, [cornerMode]: { ...Object.fromEntries(Object.entries(current[cornerMode]).filter(([id, value]) => id !== slotId && value !== playerId)), [slotId]: playerId } }))
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
  const clickSlot = (target: 'lineup' | 'corner', slotId: string) => {
    const selected = selectedPlayerId
    const update = target === 'lineup' ? setLineup : (update: React.SetStateAction<Lineup>) => setCorners(current => ({ ...current, [cornerMode]: typeof update === 'function' ? update(current[cornerMode]) : update }))
    if (selected) { assign(update, slotId, selected); setSelectedPlayerId(null); return }
    update(current => { const next = { ...current }; delete next[slotId]; return next })
  }
  const submit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!corners.attack.taker || !corners.defence.taker) {
      setValidationMessage('Назначьте подающего для углового в атаке и обороне перед сохранением.')
      return
    }
    setValidationMessage(null)
    const form = new FormData(event.currentTarget)
    save({ formation, notes: form.get('notes'), planJson: JSON.stringify({ formation, lineup, customSlots: formation === CUSTOM_FORMATION ? customSlots : undefined }), setPiecesJson: JSON.stringify({ roles, corners }), opponentInstructions: form.get('opponentInstructions') })
  }

  return <form className="tactics-workspace" onSubmit={submit}>
    <section className="tactics-command">
      <article className="card tactical-pitch" onDragOver={event => formation === CUSTOM_FORMATION && event.preventDefault()} onDrop={moveCustomToken}><div className="pitch-toolbar"><div><span className="eyebrow">Стартовый состав</span><h2>{formation}</h2></div><label>Схема<select value={formation} onChange={event => selectFormation(event.target.value)}>{Object.keys(formations).map(item => <option key={item}>{item}</option>)}<option>{CUSTOM_FORMATION}</option></select></label></div>{formation === CUSTOM_FORMATION && <p className="custom-formation-note">Своя схема: перетаскивайте фишки с игроками в любую точку поля.</p>}<Pitch slots={currentSlots} lineup={lineup} playerById={playerById} selectedPlayerId={selectedPlayerId} onDrop={slotId => event => handleDrop('lineup', slotId, event)} onClick={slotId => () => clickSlot('lineup', slotId)} /></article>
      <aside className="card tactic-squad"><div className="card-heading"><div><h2>Игроки</h2><p>Выберите игрока и нажмите на фишку — либо перетащите его на поле.</p></div></div>{data.players.map(player => <button type="button" draggable key={player.playerId} className={selectedPlayerId === player.playerId ? 'active' : ''} onClick={() => setSelectedPlayerId(current => current === player.playerId ? null : player.playerId)} onDragStart={event => event.dataTransfer.setData('playerId', String(player.playerId))}><b>{player.shirtNumber || '—'}</b><span><strong>{player.lastName}</strong><small>{player.firstName} · {player.preferredPosition}</small></span></button>)}</aside>
    </section>

    <section className="set-piece-layout">
      <article className="card set-piece-board"><div className="card-heading"><div><span className="eyebrow">Угловые</span><h2>Расстановка на стандарт</h2></div><div className="segment-control"><button type="button" className={cornerMode === 'attack' ? 'active' : ''} onClick={() => setCornerMode('attack')}>Атака</button><button type="button" className={cornerMode === 'defence' ? 'active' : ''} onClick={() => setCornerMode('defence')}>Оборона</button></div></div><p>На поле все 11 игроков. Фишка «Подающий» обязательна для сохранения.</p><div className="tactical-pitch set-piece-pitch"><Pitch slots={cornerSlots[cornerMode]} lineup={corners[cornerMode]} playerById={playerById} selectedPlayerId={selectedPlayerId} onDrop={slotId => event => handleDrop('corner', slotId, event)} onClick={slotId => () => clickSlot('corner', slotId)} /></div>{validationMessage && <p className="error-message">{validationMessage}</p>}</article>
      <details className="card role-panel"><summary><span><span className="eyebrow">Роли и исполнители</span><strong>Капитан, стандарты и ауты</strong></span><span>⌄</span></summary><p>Для штрафных, угловых и аутов можно назначить до двух исполнителей на каждую сторону.</p><div className="role-grid">{roleDefinitions.map(([id, label, max]) => <RoleAssignment key={id} label={label} max={max} players={data.players} selected={roles[id] ?? []} playerById={playerById} onAssign={playerId => assignRole(id, playerId, max)} onRemove={playerId => removeRole(id, playerId)} />)}</div></details>
    </section>

    <section className="card tactic-settings"><label>План на игру<textarea name="notes" defaultValue={data.team.tacticNotes} placeholder="Принципы игры, приоритеты и замены." /></label><label>Инструкции по сопернику<textarea name="opponentInstructions" defaultValue={data.team.opponentInstructions} placeholder="Кого и где прессингуем, слабые зоны соперника." /></label><button className="button">Сохранить тактику</button></section>
  </form>
}

function Pitch({ slots, lineup, playerById, selectedPlayerId, onDrop, onClick }: { slots: Slot[]; lineup: Lineup; playerById: (id?: number) => Player | undefined; selectedPlayerId: number | null; onDrop: (slotId: string) => (event: React.DragEvent<HTMLButtonElement>) => void; onClick: (slotId: string) => () => void }) {
  return <><span className="pitch-goal pitch-goal-top" aria-hidden="true" /><span className="pitch-goal pitch-goal-bottom" aria-hidden="true" />{slots.map(slot => { const player = playerById(lineup[slot.id]); return <button type="button" draggable key={slot.id} className={selectedPlayerId && !player ? 'ready' : ''} style={{ left: `${slot.x}%`, top: `${slot.y}%` }} onDragStart={event => { if (player) event.dataTransfer.setData('playerId', String(player.playerId)); event.dataTransfer.setData('slotId', slot.id) }} onDragOver={event => event.preventDefault()} onDrop={onDrop(slot.id)} onClick={onClick(slot.id)}><small>{slot.label}</small><strong>{player ? `${player.shirtNumber || '—'} · ${player.lastName}` : '＋'}</strong></button> })}</>
}

function RoleAssignment({ label, max, players, selected, playerById, onAssign, onRemove }: { label: string; max: number; players: Player[]; selected: number[]; playerById: (id?: number) => Player | undefined; onAssign: (playerId: number) => void; onRemove: (playerId: number) => void }) {
  return <div className="role-assignment"><strong>{label}</strong><div className="role-chips">{selected.length ? selected.map(playerId => <button type="button" key={playerId} onClick={() => onRemove(playerId)}>{playerById(playerId)?.shirtNumber || '—'} · {playerById(playerId)?.lastName} ×</button>) : <span>Не назначен</span>}</div><select aria-label={label} value="" disabled={selected.length >= max} onChange={event => { const playerId = Number(event.target.value); if (playerId) onAssign(playerId) }}><option value="">{selected.length >= max ? `Выбрано ${max}` : 'Добавить игрока'}</option>{players.filter(player => !selected.includes(player.playerId)).map(player => <option value={player.playerId} key={player.playerId}>{player.shirtNumber || '—'} · {player.lastName}</option>)}</select></div>
}
