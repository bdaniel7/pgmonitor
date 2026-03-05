import { writable } from 'svelte/store'
import * as signalR from '@microsoft/signalr'
import { getToken } from './auth.js'

export const snapshot      = writable(null)
export const activeQueries = writable([])
export const liveAlerts    = writable([])
export const connected     = writable(false)

let connection = null

export async function startHub() {
  if (connection) return

  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/monitor', {
      accessTokenFactory: () => getToken()
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  connection.on('snapshot',      data => snapshot.set(data))
  connection.on('activeQueries', data => activeQueries.set(data))
  connection.on('alerts',        data => liveAlerts.update(a => [...data, ...a].slice(0, 50)))

  connection.onreconnected(() => connected.set(true))
  connection.onclose(()      => connected.set(false))

  try {
    await connection.start()
    connected.set(true)
  } catch (e) {
    console.error('SignalR failed:', e)
  }
}

export async function stopHub() {
  if (connection) {
    await connection.stop()
    connection = null
    connected.set(false)
  }
}
