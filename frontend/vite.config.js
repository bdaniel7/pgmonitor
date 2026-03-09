import { defineConfig, loadEnv } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

export default defineConfig( ({command, mode}) => {

  const env = loadEnv(mode, process.cwd(), '')

  console.log('=================================')
  console.log('Build Mode:', mode)
  console.log('Command:', command) // 'build' or 'serve'
  console.log('Backend URL:', env.VITE_BACKEND_URL)
  console.log('Frontend PORT:', env.VITE_FRONTEND_PORT)
  console.log('=================================')

  return {
    plugins: [svelte()],
    define: {
      BACKEND_URL: JSON.stringify(env.VITE_BACKEND_URL),
      FRONTEND_PORT: parseInt(env.VITE_FRONTEND_PORT)
    },
    server: {
      port: env.VITE_FRONTEND_PORT,
      proxy: {
        '/api': `${env.VITE_BACKEND_URL}`,
        '/hubs': {target: `${env.VITE_BACKEND_URL}`, ws: true, changeOrigin: true}
      }
    },

    // Environment variable prefix for client-side access
    // By default only VITE_ prefixed variables are available in client code
    envPrefix: 'VITE_',
  }
})
