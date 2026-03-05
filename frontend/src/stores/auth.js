import { writable, derived } from 'svelte/store'

function createAuthStore() {
  const stored = localStorage.getItem('pgm_token')
  const { subscribe, set, update } = writable(stored ? { token: stored } : null)

  return {
    subscribe,
    login(token) {
      localStorage.setItem('pgm_token', token)
      set({ token })
    },
    logout() {
      localStorage.removeItem('pgm_token')
      set(null)
    }
  }
}

export const auth = createAuthStore()
export const isLoggedIn = derived(auth, $a => !!$a?.token)

export function getToken() {
  return localStorage.getItem('pgm_token')
}

export async function apiFetch(path, opts = {}) {
  const token = getToken()
  const res = await fetch('/api' + path, {
    ...opts,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(opts.headers || {})
    }
  })
  if (res.status === 401) {
    auth.logout()
    throw new Error('Unauthorized')
  }
  return res
}
