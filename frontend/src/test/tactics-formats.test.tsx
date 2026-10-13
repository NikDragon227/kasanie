import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { TacticsWorkspace } from '../pages/TacticsWorkspace'

describe('team tactics formats', () => {
  it.each(['11v11', '9v9', '8v8', '7v7', '5v5'])('shows only assigned shirt numbers on every %s board', matchFormat => {
    const players = [
      { playerId: 1, firstName: 'Миша', lastName: 'Волков', dateOfBirth: '2014-01-01', preferredPosition: 'Нападающий', dominantFoot: 'Правая', shirtNumber: 9, isCaptain: false },
      { playerId: 2, firstName: 'Артём', lastName: 'Соколов', dateOfBirth: '2010-01-01', preferredPosition: 'Нападающий', dominantFoot: 'Правая', isCaptain: false }
    ]
    const { container } = render(<TacticsWorkspace data={{ team: { matchFormat }, players }} save={vi.fn()} />)
    for (const section of ['Расстановка', 'Угловые', 'Штрафные', 'Ауты']) {
      if (section === 'Угловые') fireEvent.click(screen.getByRole('button', { name: 'Стандарты' }))
      if (section !== 'Расстановка') fireEvent.click(screen.getByRole('button', { name: section }))
      const tokens = container.querySelectorAll<HTMLButtonElement>('.tactical-pitch button')
      expect(Array.from(tokens, token => token.textContent)).toEqual(Array(tokens.length).fill('＋'))
      fireEvent.drop(tokens[0], { dataTransfer: { getData: () => '1' } })
      expect(tokens[0]).toHaveTextContent(/^9$/)
      expect(tokens[0]).toHaveAccessibleName('Миша Волков, номер 9')
      expect(tokens[0].querySelector('small')).toBeNull()
      fireEvent.drop(tokens[0], { dataTransfer: { getData: () => '2' } })
      expect(tokens[0]).toHaveTextContent(/^—$/)
    }
  })

  it.each([['11v11', 11], ['9v9', 9], ['8v8', 8], ['7v7', 7], ['5v5', 5]] as const)('uses %s for every available formation', (matchFormat, count) => {
    const { container } = render(<TacticsWorkspace data={{ team: { matchFormat }, players: [] }} save={vi.fn()} />)
    const select = screen.getByLabelText('Схема') as HTMLSelectElement
    for (const option of Array.from(select.options)) {
      fireEvent.change(select, { target: { value: option.value } })
      expect(container.querySelector(`.tactical-pitch.format-${matchFormat}`)).not.toBeNull()
      expect(container.querySelectorAll('.tactical-pitch button')).toHaveLength(count)
    }
    const label = matchFormat === '5v5' ? 'Футзал · 5 × 5' : matchFormat.replace('v', ' × ')
    expect(screen.getByText(`Стартовый состав · ${label}`)).toBeInTheDocument()
  })

  it('replaces an incompatible saved formation with a seven-player formation', () => {
    const { container } = render(<TacticsWorkspace data={{ team: { matchFormat: '7v7', tacticFormation: '4-4-2' }, players: [] }} save={vi.fn()} />)
    expect(screen.getByLabelText('Схема')).toHaveValue('2-3-1')
    expect(container.querySelectorAll('.tactical-pitch button')).toHaveLength(7)
  })

  it('fits a saved custom formation to the new team format', () => {
    const customSlots = Array.from({ length: 11 }, (_, index) => ({ id: String(index), label: 'ЦП', x: 50, y: 50 }))
    const { container } = render(<TacticsWorkspace data={{ team: { matchFormat: '7v7', tacticPlanJson: JSON.stringify({ formation: 'Своя схема', customSlots }) }, players: [] }} save={vi.fn()} />)
    expect(screen.getByLabelText('Схема')).toHaveValue('Своя схема')
    expect(container.querySelectorAll('.tactical-pitch button')).toHaveLength(7)
  })

  it('uses five players including a goalkeeper on every futsal set-piece board', () => {
    const { container } = render(<TacticsWorkspace data={{ team: { matchFormat: '5v5' }, players: [] }} save={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: 'Стандарты' }))
    for (const section of ['Угловые', 'Штрафные', 'Ауты']) {
      fireEvent.click(screen.getByRole('button', { name: section }))
      expect(container.querySelectorAll('.tactical-pitch.format-5v5 button')).toHaveLength(5)
      expect(screen.getByRole('button', { name: 'Назначить игрока: ВР' })).toBeInTheDocument()
    }
  })
})
