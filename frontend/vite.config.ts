import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: { '/api': 'http://localhost:5080', '/health': 'http://localhost:5080' } },
  test: { environment: 'jsdom', setupFiles: './src/test/setup.ts', css: true, include: ['src/**/*.test.{ts,tsx}'] },
})
