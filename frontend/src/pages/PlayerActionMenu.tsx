import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { post, put } from '../api'
import { EmptyState, ErrorState } from '../components'
import { useApiData } from '../hooks'
import { TrainingDialog } from './TrainingWorkspace'
import { intensities, playerAge, type PlayerSettings } from './trainingModel'
import './playerActions.css'

export type ActionPlayer = { playerId: number; firstName: string; lastName: string; dateOfBirth: string; preferredPosition: string; dominantFoot: string; experienceLevel: string; height?: number; weight?: number; appearances?: number; goals?: number; assists?: number; averageRating?: number; inCurrentTeam?: boolean; teams?: string[]; skills?: Record<string, number>; skillsCapturedAt?: string }
type Actions = { teams: { id: number; name: string }[]; players: ActionPlayer[]; training?: PlayerSettings }
const name = (player: ActionPlayer) => `${player.firstName} ${player.lastName}`
const skillLabels = [['speed', 'Скорость'], ['endurance', 'Выносливость'], ['ballControl', 'Контроль мяча'], ['passing', 'Передачи'], ['shooting', 'Удары'], ['agility', 'Ловкость']]

export function similarPlayers(source: ActionPlayer, candidates: ActionPlayer[]) {
  const score = (player: ActionPlayer) => {
    const distance = source.skills && player.skills ? skillLabels.reduce((sum, [key]) => sum + Math.abs(source.skills![key] - player.skills![key]), 0) / 6 : 0
    return (player.preferredPosition === source.preferredPosition ? 400 : 0) + (player.dominantFoot === source.dominantFoot ? 50 : 0) + (player.experienceLevel === source.experienceLevel ? 30 : 0) - Math.abs(playerAge(player.dateOfBirth) - playerAge(source.dateOfBirth)) * 10 - distance
  }
  return candidates.filter(player => player.playerId !== source.playerId).sort((a, b) => score(b) - score(a) || a.lastName.localeCompare(b.lastName))
}

export function PlayerActionMenu({ player, teamId, point, close, run }: { player: ActionPlayer; teamId: number; point: { x: number; y: number }; close: () => void; run: (action: () => Promise<unknown>, message: string) => Promise<boolean> }) {
  const response = useApiData<Actions>(`/api/coach/teams/${teamId}/players/${player.playerId}/actions`)
  const [section, setSection] = useState<'squad' | 'training' | 'report'>('squad')
  const [dialog, setDialog] = useState<'note' | 'trainingNote' | 'compare' | 'similar' | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const root = useRef<HTMLDivElement>(null)
  const onClose = useEffectEvent(() => { if (!pending) close() })
  useEffect(() => {
    const outside = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) onClose() }
    const key = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', key)
    root.current?.querySelector<HTMLButtonElement>('[role="menuitem"]')?.focus()
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', key) }
  }, [])
  const action = async (task: () => Promise<unknown>, message: string) => {
    setPending(true); setError('')
    try { if (await run(task, message)) close(); else setError('Не удалось выполнить действие. Подробности показаны над составом.') }
    finally { setPending(false) }
  }
  const training = (value: { restDays?: number; intensity?: string; notes?: string }, message: string) => action(() => put(`/api/coach/teams/${teamId}/players/${player.playerId}/training`, { ...value, utcOffsetMinutes: -new Date().getTimezoneOffset() }), message)
  const width = Math.min(560, window.innerWidth - 24), height = Math.min(470, window.innerHeight - 24)
  return createPortal(<div ref={root} className="player-action-root">
    {!dialog && <div className="player-context-menu" style={{ left: Math.max(12, Math.min(point.x, window.innerWidth - width - 12)), top: Math.max(12, Math.min(point.y, window.innerHeight - height - 12)), width }} onKeyDown={event => {
      if (!['ArrowDown', 'ArrowUp'].includes(event.key)) return
      const buttons = Array.from(root.current?.querySelectorAll<HTMLButtonElement>('[role="menuitem"]:not(:disabled)') ?? [])
      const index = buttons.indexOf(document.activeElement as HTMLButtonElement)
      event.preventDefault(); buttons[(index + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length]?.focus()
    }}><header><strong>{name(player)}</strong><small>{player.preferredPosition} · {playerAge(player.dateOfBirth)} лет</small><button aria-label="Закрыть меню игрока" onClick={close}>×</button></header><div className="player-menu-columns"><nav role="menu" aria-label={`Действия игрока ${name(player)}`}>{[['squad', 'Состав'], ['training', 'Тренировки'], ['report', 'Отчёт']].map(([id, label]) => <button role="menuitem" aria-expanded={section === id} className={section === id ? 'active' : ''} key={id} onMouseEnter={() => setSection(id as typeof section)} onClick={() => setSection(id as typeof section)}>{label}<span aria-hidden="true">›</span></button>)}<button role="menuitem" onClick={() => setDialog('note')}>Создать заметку</button></nav><div className="player-menu-panel" role="menu" aria-label={section === 'squad' ? 'Перемещение состава' : section === 'training' ? 'Настройки тренировки' : 'Отчёт игрока'}>
      {section === 'squad' && <><small className="player-menu-caption">Переместить в доступный состав</small>{response.loading ? <p>Загрузка составов…</p> : response.data?.teams.length ? response.data.teams.map(team => <button disabled={pending} role="menuitem" key={team.id} onClick={() => { if (confirm(`Переместить ${name(player)} в «${team.name}»? История в прежнем составе сохранится. Если номер занят, новый номер потребуется назначить отдельно.`)) void action(() => post(`/api/coach/teams/${teamId}/players/${player.playerId}/move`, { destinationTeamId: team.id }), 'Игрок перемещён в другой состав.') }}>{team.name}</button>) : <p className="readonly-note">Других доступных составов этой школы нет.</p>}</>}
      {section === 'training' && <><small className="player-menu-caption">Назначить отдых</small><div className="player-rest-shortcuts">{[1, 2, 3, 7].map(days => <button disabled={pending} role="menuitem" key={days} onClick={() => void training({ restDays: days }, `Отдых назначен на ${days} дн.`)}>{days} дн.</button>)}</div><button disabled={pending} role="menuitem" onClick={() => void training({ restDays: 0 }, 'Отдых отменён.')}>Отменить отдых</button><small className="player-menu-caption">Интенсивность тренировки</small>{intensities.map(item => <button disabled={pending} role="menuitem" key={item.id} onClick={() => void training({ intensity: item.id }, 'Интенсивность тренировки сохранена.')}>{item.label === 'Отдых' ? 'Без тренировок (отдых)' : item.label}{response.data?.training?.intensity === item.id ? ' ✓' : ''}</button>)}<button role="menuitem" onClick={() => setDialog('trainingNote')}>Заметка об интенсивности</button><Link role="menuitem" to={`/coach/trainings?team=${teamId}&trainingView=individual&player=${player.playerId}`}>Индивидуальные тренировки →</Link></>}
      {section === 'report' && <><Link role="menuitem" to={`/coach/players/${player.playerId}`}>Отчёт тренера →</Link><button role="menuitem" disabled={!response.data} onClick={() => setDialog('compare')}>Сравнить с игроком</button><button role="menuitem" disabled={!response.data} onClick={() => setDialog('similar')}>Найти похожих игроков</button></>}
    </div></div>{response.error && <ErrorState message={response.error} retry={response.reload} />}{error && <p role="alert" className="error-message">{error}</p>}</div>}
    {(dialog === 'note' || dialog === 'trainingNote') && <TrainingDialog title={`${dialog === 'note' ? 'Создать заметку' : 'Заметка об интенсивности'} · ${name(player)}`} close={close}><form onSubmit={event => { event.preventDefault(); const text = String(new FormData(event.currentTarget).get('text')).trim(); if (!text) return; void (dialog === 'note' ? action(() => post(`/api/coach/players/${player.playerId}/notes`, { text }), 'Заметка об игроке сохранена.') : training({ notes: text }, 'Заметка об интенсивности сохранена.')) }}><label>Заметка<textarea name="text" required maxLength={2000} placeholder={dialog === 'trainingNote' ? 'Причина изменения нагрузки и задача для игрока…' : 'Наблюдение тренера…'} /></label><small>{dialog === 'trainingNote' ? 'Добавится к комментариям в индивидуальных тренировках.' : 'Сохранится в отчёте и профиле игрока.'}</small>{error && <p role="alert" className="error-message">{error}</p>}<div className="training-dialog-actions"><button type="button" disabled={pending} className="button ghost" onClick={close}>Отмена</button><button disabled={pending} className="button">{pending ? 'Сохраняем…' : 'Сохранить заметку'}</button></div></form></TrainingDialog>}
    {(dialog === 'compare' || dialog === 'similar') && response.data && <PlayerComparison source={response.data.players.find(p => p.playerId === player.playerId) ?? player} candidates={response.data.players} similar={dialog === 'similar'} close={close} />}
  </div>, document.body)
}

function PlayerComparison({ source, candidates, similar, close }: { source: ActionPlayer; candidates: ActionPlayer[]; similar: boolean; close: () => void }) {
  const [scope, setScope] = useState('team'), [search, setSearch] = useState(''), [selected, setSelected] = useState(0)
  const pool = candidates.filter(player => player.playerId !== source.playerId && (scope === 'accessible' || player.inCurrentTeam) && name(player).toLowerCase().includes(search.toLowerCase()))
  const target = pool.find(player => player.playerId === selected) ?? pool[0]
  const rows: [string, (player: ActionPlayer) => string | number][] = [
    ['Возраст', p => playerAge(p.dateOfBirth)], ['Позиция', p => p.preferredPosition], ['Ведущая нога', p => p.dominantFoot], ['Опыт', p => p.experienceLevel],
    ['Рост, см', p => p.height ?? '—'], ['Вес, кг', p => p.weight ?? '—'], ['Игры', p => p.appearances ?? '—'], ['Голы', p => p.goals ?? '—'], ['Голевые передачи', p => p.assists ?? '—'], ['Средняя оценка', p => p.averageRating?.toFixed(2) ?? '—'],
    ...skillLabels.map(([key, label]): [string, (p: ActionPlayer) => string | number] => [label, p => p.skills?.[key] ?? 'Нет оценки'])
  ]
  return <TrainingDialog title={`${similar ? 'Похожие игроки' : 'Сравнение игроков'} · ${name(source)}`} close={close} wide><div className="player-comparison-controls"><label>Область поиска<select value={scope} onChange={e => { setScope(e.target.value); setSelected(0) }}><option value="team">Своя команда</option><option value="accessible">Все доступные тренеру</option></select></label><label>Поиск по имени<input type="search" value={search} onChange={e => setSearch(e.target.value)} /></label>{!similar && <label>Сравнить с<select value={target?.playerId ?? ''} onChange={e => setSelected(Number(e.target.value))}>{pool.length ? pool.map(player => <option value={player.playerId} key={player.playerId}>{name(player)} · {player.preferredPosition}</option>) : <option value="">Нет игроков</option>}</select></label>}</div>{!pool.length ? <EmptyState title="Подходящих доступных игроков нет" /> : similar ? <><p className="readonly-note">Порядок учитывает позицию, возраст, ведущую ногу, опыт и существующие оценки навыков. Это подбор по данным, не оценка таланта.</p><div className="player-similar-list">{similarPlayers(source, pool).slice(0, 20).map(player => <article key={player.playerId}><div><strong>{name(player)}</strong><small>{playerAge(player.dateOfBirth)} лет · {player.preferredPosition} · {player.teams?.join(', ')}</small><p>{player.preferredPosition === source.preferredPosition ? 'Та же позиция · ' : ''}Разница в возрасте: {Math.abs(playerAge(player.dateOfBirth) - playerAge(source.dateOfBirth))} лет{player.dominantFoot === source.dominantFoot ? ' · Та же ведущая нога' : ''}</p></div><Link to={`/coach/players/${player.playerId}`}>Открыть отчёт →</Link></article>)}</div></> : target && <><div className="training-table-scroll"><table className="player-comparison-table"><thead><tr><th>Показатель</th><th>{name(source)}</th><th>{name(target)}</th></tr></thead><tbody>{rows.map(([label, value]) => <tr key={label}><th scope="row">{label}</th><td>{value(source)}</td><td>{value(target)}</td></tr>)}</tbody></table></div><p className="readonly-note">Оценки навыков — последнее сохранённое тестирование. Без тестирования значения не подставляются. Матчевая статистика относится к указанному составу каждого игрока.</p></>}</TrainingDialog>
}
