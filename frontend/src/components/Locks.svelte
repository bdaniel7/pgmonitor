<script>
  import { onMount, onDestroy } from 'svelte'
  import { apiFetch } from '../stores/auth.js'

  let locks   = []
  let loading = true
  let interval

  async function load() {
    try {
      const res = await apiFetch('/locks')
      if (res.ok) locks = await res.json()
    } finally { loading = false }
  }

  onMount(() => { load(); interval = setInterval(load, 3000) })
  onDestroy(() => clearInterval(interval))

  $: waiting   = locks.filter(l => !l.granted)
  $: blocking  = locks.filter(l => l.granted  && locks.some(b => !b.granted && b.lockType === l.lockType && b.relation === l.relation))

  function durationMs(d) {
    if (!d) return 0
    const parts = d.split(':')
    if (parts.length === 3) {
      const [h, m, s] = parts
      return parseInt(h)*3600000 + parseInt(m)*60000 + parseFloat(s)*1000
    }
    return 0
  }

  function fmtDur(d) {
    const ms = durationMs(d)
    if (ms < 1000) return ms.toFixed(0) + 'ms'
    if (ms < 60000) return (ms/1000).toFixed(1) + 's'
    return Math.floor(ms/60000) + 'm'
  }

  function modeColor(mode) {
    if (mode?.includes('Exclusive') || mode?.includes('Share')) return 'text-red'
    if (mode?.includes('Row')) return 'text-yellow'
    return 'text-muted'
  }
</script>

<div class="p-6 flex flex-col h-full overflow-hidden">
  <div class="flex items-center justify-between mb-4 flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">Lock Detection</h1>
    <div class="flex items-center gap-4">
      {#if waiting.length > 0}
        <div class="flex items-center gap-1.5 text-red text-xs font-display">
          <span class="w-2 h-2 rounded-full bg-red blink"></span>
          {waiting.length} waiting
        </div>
      {/if}
      <button on:click={load} class="text-xs px-3 py-1.5 rounded border border-border text-muted hover:text-accent hover:border-accent/40 transition-colors font-display">↺ Refresh</button>
    </div>
  </div>

  {#if loading}
    <div class="flex-1 flex items-center justify-center text-muted">Loading…</div>
  {:else}
    <!-- Summary row -->
    <div class="grid grid-cols-3 gap-4 mb-4 flex-shrink-0">
      <div class="bg-surface border border-border rounded-lg px-4 py-3 flex items-center justify-between">
        <span class="text-xs text-muted font-display">Total Locks</span>
        <span class="font-mono text-text font-bold">{locks.length}</span>
      </div>
      <div class="bg-surface border {waiting.length ? 'border-red/40' : 'border-border'} rounded-lg px-4 py-3 flex items-center justify-between">
        <span class="text-xs text-muted font-display">Waiting</span>
        <span class="font-mono font-bold {waiting.length ? 'text-red' : 'text-text'}">{waiting.length}</span>
      </div>
      <div class="bg-surface border {blocking.length ? 'border-yellow/40' : 'border-border'} rounded-lg px-4 py-3 flex items-center justify-between">
        <span class="text-xs text-muted font-display">Blocking</span>
        <span class="font-mono font-bold {blocking.length ? 'text-yellow' : 'text-text'}">{blocking.length}</span>
      </div>
    </div>

    {#if locks.length === 0}
      <div class="flex-1 flex items-center justify-center text-muted">
        <div class="text-center">
          <div class="text-3xl mb-2 opacity-20">🔓</div>
          <div class="font-display">No locks detected</div>
        </div>
      </div>
    {:else}
      <div class="flex-1 overflow-y-auto">
        <table class="w-full text-xs">
          <thead class="sticky top-0 bg-bg">
            <tr class="border-b border-border">
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">PID</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">User</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Relation</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Type</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Mode</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Status</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Duration</th>
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Query</th>
            </tr>
          </thead>
          <tbody>
            {#each locks as lock}
              <tr class="border-b border-border/50 hover:bg-surface/60 transition-colors
                {!lock.granted ? 'bg-red/5' : ''}">
                <td class="py-2 px-3 font-mono {!lock.granted ? 'text-red' : 'text-accent'}">{lock.pid}</td>
                <td class="py-2 px-3 font-mono text-muted">{lock.username}</td>
                <td class="py-2 px-3 font-mono text-text">{lock.relation ?? '—'}</td>
                <td class="py-2 px-3 text-muted">{lock.lockType}</td>
                <td class="py-2 px-3 {modeColor(lock.mode)}">{lock.mode}</td>
                <td class="py-2 px-3">
                  <span class="px-1.5 py-0.5 rounded text-xs font-display
                    {lock.granted ? 'bg-green/20 text-green' : 'bg-red/20 text-red'}">
                    {lock.granted ? 'granted' : 'waiting'}
                  </span>
                </td>
                <td class="py-2 px-3 font-mono text-muted">{fmtDur(lock.duration)}</td>
                <td class="py-2 px-3 font-mono max-w-xs">
                  <div class="truncate text-muted" title={lock.query}>{lock.query}</div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
  {/if}
</div>
