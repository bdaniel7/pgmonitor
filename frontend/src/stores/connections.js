import { writable } from 'svelte/store'
import { apiFetch } from './auth.js'

export const connections = writable([])

export async function loadConnections() {
  const res = await apiFetch('/connections')
  if (res.ok) connections.set(await res.json())
}

export async function addConnection(data) {
  const res = await apiFetch('/connections', {
    method: 'POST',
    body: JSON.stringify(data)
  })
  if (!res.ok) throw new Error((await res.json()).error)
  const conn = await res.json()
  connections.update(cs => [...cs, conn])
  return conn
}

export async function deleteConnection(id) {
  await apiFetch(`/connections/${id}`, { method: 'DELETE' })
  connections.update(cs => cs.filter(c => c.id !== id))
}

export async function testConnection(data) {
  const res = await apiFetch('/connections/test', {
    method: 'POST',
    body: JSON.stringify(data)
  })
  const json = await res.json()
  return json.ok
}
