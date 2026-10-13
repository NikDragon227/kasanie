import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { TacticsWorkspace } from '../pages/TacticsWorkspace'

describe('team tactics formats', () => {
  it.each([['11v11', 11], ['9v9', 9], ['8v8', 8], ['7v7', 7]] as const)('uses %s for every available formation', (matchFormat, count) => {
    const { container } = render(<TacticsWorkspace data={{ team: { matchFormat }, players: [] }} save={vi.fn()} />)
    const select = screen.getByLabelText('Схема') as HTMLSelectElement
    for (const option of Array.from(select.options)) {
      fireEvent.change(select, { target: { value: option.value } })
      expect(container.querySelector(`.tactical-pitch.format-${matchFormat}`)).not.toBeNull()
      expect(container.querySelectorAll('.tactical-pitch button')).toHaveLength(count)
    }
    expect(screen.getByText(`Стартовый состав · ${matchFormat.replace('v', ' × ')}`)).toBeInTheDocument()
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
})
