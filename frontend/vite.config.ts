import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Docker Compose ではバックエンドが別コンテナになるため、
// プロキシ先を BACKEND_ORIGIN で上書きできるようにする(既定はローカル起動)。
const backendOrigin = process.env.BACKEND_ORIGIN ?? 'http://localhost:5100'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: backendOrigin,
        changeOrigin: true,
      },
    },
  },
})
