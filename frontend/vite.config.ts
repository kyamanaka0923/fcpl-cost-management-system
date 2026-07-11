import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Docker Compose ではバックエンドが別コンテナになるため、
// プロキシ先を BACKEND_ORIGIN で上書きできるようにする(既定はローカル起動)。
const backendOrigin = process.env.BACKEND_ORIGIN ?? 'http://localhost:5100'

export default defineConfig({
  plugins: [react()],
  server: {
    host: true, // IPv4/IPv6両方でlisten(Dev Container等のポートフォワーディングがIPv4を見るため必須)
    port: 5173,
    proxy: {
      '/api': {
        target: backendOrigin,
        changeOrigin: true,
      },
    },
  },
})
