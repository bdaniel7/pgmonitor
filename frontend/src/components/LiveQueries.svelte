<script>
  import { activeQueries } from '../stores/hub.js'
  import { apiFetch } from '../stores/auth.js'

  let filter = ''
  let sortBy = 'duration'

  $: filtered = ($activeQueries || [])
    .filter(q => !filter || q.query?.toLowerCase().includes(filter.toLowerCase())
                         || q.username?.toLowerCase().includes(filter.toLowerCase())
                         || q.database?.toLowerCase().includes(filter.toLowerCase()))
    .sort((a, b) => {
      if (sortBy === 'duration') return durationMs(b.duration) - durationMs(a.duration)
      if (sortBy === 'pid') return a.pid - b.pid
      return 0
    })

  function durationMs(d) {
    if (!d) return 0
    // d is a TimeSpan string like "00:01:23.456"
    const parts = d.split(':')
    if (parts.length === 3) {
      const [h, m, s] = parts
      return parseInt(h)*3600000 + parseInt(m)*60000 + parseFloat(s)*1000
    }
    return 0
  }

  function fmtDuration(d) {
    const ms = durationMs(d)
    if (ms < 1000) return ms.toFixed(0) + 'ms'
    if (ms < 60000) return (ms/1000).toFixed(1) + 's'
    if (ms < 3600000) return Math.floor(ms/60000) + 'm ' + Math.floor((ms%60000)/1000) + 's'
    return Math.floor(ms/3600000) + 'h'
  }

  function stateColor(state) {
    if (state === 'active') return 'text-green'
    if (state === 'idle in transaction') return 'text-yellow'
    if (state === 'idle') return 'text-muted'
    return 'text-text'
  }

  async function terminate(pid) {
    if (!confirm(`Terminate PID ${pid}?`)) return
    await apiFetch(`/queries/terminate/${pid}`, { method: 'POST' })
  }
</script>

<div class="p-6 flex flex-col h-full overflow-hidden">
  <div class="flex items-center justify-between mb-4 flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">Live Queries</h1>
    <div class="flex items-center gap-3">
      <span class="text-xs text-muted font-mono">{filtered.length} active</span>
      <input
        bind:value={filter}
        placeholder="Filter…"
        class="px-3 py-1.5 text-xs rounded w-48"
      />
      <select bind:value={sortBy} class="px-2 py-1.5 text-xs rounded">
        <option value="duration">Sort: Duration</option>
        <option value="pid">Sort: PID</option>
      </select>
    </div>
  </div>

  <div class="flex-1 overflow-y-auto">
    <table class="w-full text-xs">
      <thead class="sticky top-0 bg-bg">
        <tr class="border-b border-border">
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">PID</th>
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">User</th>
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">DB</th>
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">State</th>
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Duration</th>
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Wait</th>
          <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Query</th>
        </tr>
      </thead>
      <tbody>
        {#each filtered as q (q.pid)}
          <tr class="border-b border-border/50 hover:bg-surface/60 transition-colors">
            <td class="py-2 px-3 font-mono text-accent">{q.pid}</td>
            <td class="py-2 px-3 font-mono text-text">{q.username}</td>
            <td class="py-2 px-3 font-mono text-muted">{q.database}</td>
            <td class="py-2 px-3">
              <span class="font-mono {stateColor(q.state)}">{q.state}</span>
            </td>
            <td class="py-2 px-3 font-mono
              {durationMs(q.duration) > 30000 ? 'text-red' : durationMs(q.duration) > 5000 ? 'text-yellow' : 'text-text'}">
              {fmtDuration(q.duration)}
            </td>
            <td class="py-2 px-3 text-muted">
              {q.waitEvent ? `${q.waitEventType}:${q.waitEvent}` : '—'}
            </td>
            <td class="py-2 px-3 font-mono text-text max-w-xs">
              <div class="truncate" title={q.query}>{q.query}</div>
            </td>
          </tr>
        {:else}
          <tr>
            <td colspan="7" class="py-12 text-center text-muted">No active queries</td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
</div>
