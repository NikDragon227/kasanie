import { test, expect, type Page } from '@playwright/test'

const password = 'Kasanie-Demo-2026!'
async function login(page: Page, email: string) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Пароль').fill(password)
  const loginResponse = page.waitForResponse(response => response.url().endsWith('/api/auth/login') && response.request().method() === 'POST')
  await page.getByRole('button', { name: 'Войти' }).click()
  expect((await loginResponse).ok()).toBe(true)
  await page.waitForURL(url => url.pathname !== '/login')
}
async function deactivateFixtures(page: Page, prefix: string) {
  for (;;) {
    const row = page.locator('.simple-list article:not(.inactive)').filter({ hasText: prefix }).first()
    if (await row.count() === 0) return
    await row.getByRole('button', { name: 'Деактивировать запись' }).click()
    await expect(row).toHaveClass(/inactive/)
  }
}

test('admin removes stale E2E fixtures', async ({ page }) => {
  await login(page, 'admin@kasanie.local')
  await page.goto('/admin/exercises'); await deactivateFixtures(page, 'E2E упражнение'); await deactivateFixtures(page, 'Smoke CRUD')
  await page.goto('/admin/programs'); await deactivateFixtures(page, 'E2E программа')
  await page.goto('/admin/municipalities'); await deactivateFixtures(page, 'E2E город')
})

test('player opens a workout', async ({ page }) => {
  await login(page, 'player@kasanie.local')
  await expect(page.getByText('Привет, Артём!')).toBeVisible()
  await page.goto('/player/assessment'); await expect(page.getByText('Шесть тестов — честный старт')).toBeVisible()
  await page.goto('/player/training'); await expect(page.getByText('Твой тренировочный план')).toBeVisible()
  const start = page.getByRole('button', { name: /Начать тренировку|Продолжить|Посмотреть результат/ }).first()
  await start.click(); await expect(page.getByText('Тренировка сохраняется')).toBeVisible()
  await expect(page.getByLabel(/Сложность:/).first()).toBeVisible()
  while (await page.getByRole('button', { name: /Отметить:/ }).count()) {
    const saved = page.waitForResponse(response => response.url().includes('/api/training/sessions/') && response.url().includes('/exercises/') && response.request().method() === 'PUT')
    await page.getByRole('button', { name: /Отметить:/ }).first().click()
    expect((await saved).ok()).toBe(true)
  }
  await page.getByRole('button', { name: 'Завершить тренировку' }).click()
  await expect(page.getByText('Прогресс в цифрах')).toBeVisible()
})

for (const [email, path, marker] of [
  ['coach@kasanie.local', '/coach/players', 'Мои игроки'],
  ['parent@kasanie.local', '/parent', 'Мои дети'],
  ['analyst@kasanie.local', '/analytics', 'Региональная картина'],
] as const) test(`${email} sees role dashboard`, async ({ page }) => { await login(page, email); await page.goto(path); await expect(page.getByRole('heading', { name: marker })).toBeVisible() })

test('coach sees player workout feedback', async ({ page }) => {
  await login(page, 'coach@kasanie.local')
  await page.goto('/coach/players')
  await page.getByRole('link', { name: /артём/i }).first().click()
  await expect(page.getByRole('heading', { name: 'Самооценка игрока' })).toBeVisible()
  await expect(page.locator('select').nth(1)).not.toContainText(/E2E упражнение|Smoke CRUD/)
  await expect(page.locator('select').nth(2)).not.toContainText('E2E программа')
})

test('admin creates an exercise', async ({ page }) => {
  const name = `E2E упражнение ${Date.now()}`
  await login(page, 'admin@kasanie.local'); await page.goto('/admin/exercises'); await page.getByRole('button', { name: '+ Новое' }).click();
  await page.getByLabel('Название').fill(name); await page.getByLabel('Описание').fill('Проверка административного CRUD'); await page.getByLabel('Инструкция').fill('Выполнить тест'); await page.getByLabel('Инвентарь').fill('Мяч'); await page.getByRole('button', { name: 'Сохранить' }).click(); await expect(page.getByText('Сохранено.')).toBeVisible()
  const row = page.locator('.simple-list article').filter({ hasText: name }); await row.getByRole('button', { name: 'Деактивировать запись' }).click(); await expect(row).toHaveClass(/inactive/)
})

test('admin manages cities and training programs', async ({ page }) => {
  const suffix = Date.now()
  await login(page, 'admin@kasanie.local')
  await page.goto('/admin/municipalities'); await page.getByRole('button', { name: '+ Город' }).click()
  const city = `E2E город ${suffix}`; await page.getByLabel('Город').fill(city); await page.getByLabel('Регион').fill('Тестовый регион'); await page.getByRole('button', { name: 'Сохранить' }).click(); await expect(page.getByText(city)).toBeVisible()
  const cityRow = page.locator('.simple-list article').filter({ hasText: city }); await cityRow.getByRole('button', { name: 'Деактивировать запись' }).click(); await expect(cityRow).toHaveClass(/inactive/)
  await page.goto('/admin/programs'); await page.getByRole('button', { name: '+ Новая' }).click()
  const program = `E2E программа ${suffix}`; await page.getByLabel('Название').fill(program); await page.getByLabel('Недель').fill('2'); await page.getByLabel('Описание').fill('Проверка управления программой'); await page.getByRole('button', { name: 'Сохранить' }).click(); await expect(page.getByText(program)).toBeVisible()
  const programRow = page.locator('.simple-list article').filter({ hasText: program }); await programRow.getByRole('button', { name: 'Деактивировать запись' }).click(); await expect(programRow).toHaveClass(/inactive/)
})

test('password recovery responds without exposing account existence', async ({ page }) => {
  await page.goto('/forgot-password'); await page.getByLabel('Email').fill('player@kasanie.local'); await page.getByRole('button', { name: 'Отправить ссылку' }).click()
  await expect(page.getByRole('status')).toContainText('Если такой подтверждённый аккаунт существует')
})
