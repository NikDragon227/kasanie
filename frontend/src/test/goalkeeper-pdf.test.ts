// @vitest-environment node
/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { createKeeperPlanPdf, keeperPdfFilename, type KeeperPdfInput } from '../pages/goalkeeperPdf'

const fonts = {
  regular: readFileSync(new URL('../assets/pdf-fonts/NotoSans-Regular.ttf', import.meta.url)).toString('base64'),
  bold: readFileSync(new URL('../assets/pdf-fonts/NotoSans-Bold.ttf', import.meta.url)).toString('base64'),
}
const input: KeeperPdfInput = {
  team: { school: 'Плазма СПОРТ', name: 'U17 - Первый состав', coach: 'Демо тренер' },
  names: [{ playerId: 1, firstName: 'Иван', lastName: 'Вратарь (демо)' }],
  plan: {
    title: 'Ловля мяча и игра на выходах', scheduledAt: '2026-10-22T18:00', venue: 'Поле школы',
    objective: 'Улучшить выбор позиции и технику ловли.', intensity: 'Средняя', status: 'Planned', version: 1,
    plan: [
      { exerciseId: null, title: 'Координационная разминка', stage: 'Разминка', durationMinutes: 10, instructions: 'Передвижения приставными шагами. Две серии по пять повторений.' },
      { exerciseId: 1, title: 'Ловля высокого мяча', stage: 'Основная часть', durationMinutes: 25, instructions: 'Работа в парах.\nКонтролировать положение рук и приземление.' },
      { exerciseId: null, title: 'Восстановление дыхания', stage: 'Заминка', durationMinutes: 10, instructions: 'Лёгкий бег и растяжка.' },
    ],
    players: [{ playerId: 1, attendance: 'Unknown', rating: null, notes: 'Личные наблюдения не входят в PDF' }], notes: '',
  },
}

describe('goalkeeper PDF', () => {
  it('creates a real A4 PDF with embedded Cyrillic fonts without mutating the saved plan', () => {
    const before = structuredClone(input)
    const doc = createKeeperPlanPdf(input, fonts)
    const bytes = new Uint8Array(doc.output('arraybuffer'))
    expect(new TextDecoder().decode(bytes.slice(0, 8))).toBe('%PDF-1.3')
    expect(doc.getNumberOfPages()).toBe(1)
    expect(doc.internal.pageSize.getWidth()).toBeCloseTo(210)
    expect(doc.getFontList().NotoSans).toEqual(['normal', 'bold'])
    expect(bytes.length).toBeGreaterThan(15000)
    expect(input).toEqual(before)
  })

  it('paginates long Cyrillic instructions and unbroken strings instead of clipping', () => {
    const long = structuredClone(input)
    long.plan.plan = Array.from({ length: 5 }, (_, index) => ({ exerciseId: null, title: `Этап ${index + 1} - ${'Длинное название '.repeat(8)}`, stage: 'Основная часть', durationMinutes: 10, instructions: 'Инструкция для вратаря с переносом строк. '.repeat(80) + '\n' + 'ДлинноеСлово'.repeat(15) }))
    const doc = createKeeperPlanPdf(long, fonts)
    expect(doc.getNumberOfPages()).toBeGreaterThan(3)
    expect(doc.getNumberOfPages()).toBeLessThan(15)
  })

  it('rejects an incomplete plan and produces safe dated filenames', () => {
    expect(() => createKeeperPlanPdf({ ...input, plan: { ...input.plan, plan: [] } }, fonts)).toThrow('дата, тема и этапы')
    expect(() => createKeeperPlanPdf({ ...input, plan: { ...input.plan, scheduledAt: 'invalid' } }, fonts)).toThrow()
    expect(keeperPdfFilename({ title: '../План: <ловля> / мяч?', scheduledAt: '2026-10-22T18:00' })).toBe('План тренировки 2026-10-22 - План ловля мяч.pdf')
  })
})
