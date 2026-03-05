<script>
  import { onMount } from 'svelte'
  import { apiFetch } from '../stores/auth.js'

  let queries = []
  let loading = true
  let limit   = 50
  let sortBy  = 'meanTime'

  async function load() {
    loading = true
    try {
      const res = await apiFetch(`/queries/slow?limit=${limit}`)
      if (res.ok) queries = await res.json()
    } finally {
      loading = false
    }
  }

  onMount(load)

  $: sorted = [...queries].sort((a, b) => b[sortBy] - a[sortBy])

  function fmtMs(ms) {
    if (ms >= 1000) return (ms/1000).toFixed(2) + 's'
    return ms.toFixed(2) + 'ms'
  }

  function fmtNum(n) {
    if (n >= 1e9) return (n/1e9).toFixed(1) + 'B'
    if (n >= 1e6) return (n/1e6).toFixed(1) + 'M'
    if (n >= 1e3) return (n/1e3).toFixed(1) + 'K'
    return String(n)
  }

  let selected = null
</script>

<div class="p-6 flex flex-col h-full overflow-hidden">
  <div class="flex items-center justify-between mb-4 flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">Slow Queries <span class="text-xs text-muted font-normal ml-2">via pg_stat_statements</span></h1>
    <div class="flex items-center gap-3">
      <select bind:value={limit} on:change={load} class="px-2 py-1.5 text-xs rounded">
        <option value={25}>Top 25</option>
        <option value={50}>Top 50</option>
        <option value={100}>Top 100</option>
      </select>
      <select bind:value={sortBy} class="px-2 py-1.5 text-xs rounded">
        <option value="meanTime">Sort: Mean Time</option>
        <option value="totalTime">Sort: Total Time</option>
        <option value="calls">Sort: Calls</option>
        <option value="maxTime">Sort: Max Time</option>
      </select>
      <button on:click={load} class="px-3 py-1.5 text-xs rounded border border-accent/30 text-accent hover:bg-accent/10 transition-colors font-display">
        ↺ Refresh
      </button>
    </div>
  </div>

  {#if loading}
    <div class="flex-1 flex items-center justify-center text-muted">Loading…</div>
  {:else}
    <div class="flex-1 overflow-hidden flex flex-col">
      <div class="overflow-y-auto flex-1">
        <table class="w-full text-xs">
          <thead class="sticky top-0 bg-bg">
            <tr class="border-b border-border">
              <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Query</th>
              <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Calls</th>
              <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Mean</th>
              <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Max</th>
              <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Total</th>
              <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Rows</th>
              <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Cache %</th>
            </tr>
          </thead>
          <tbody>
            {#each sorted as q}
              <tr
                class="border-b border-border/50 hover:bg-surface cursor-pointer transition-colors
                  {selected === q ? 'bg-surface' : ''}"
                on:click={() => selected = selected === q ? null : q}
              >
                <td class="py-2 px-3 font-mono text-text max-w-xs">
                  <div class="truncate">{q.query}</div>
                </td>
                <td class="py-2 px-3 font-mono text-muted text-right">{fmtNum(q.calls)}</td>
                <td class="py-2 px-3 font-mono text-right {q.meanTime > 1000 ? 'text-red' : q.meanTime > 100 ? 'text-yellow' : 'text-green'}">
                  {fmtMs(q.meanTime)}
                </td>
                <td class="py-2 px-3 font-mono text-right text-muted">{fmtMs(q.maxTime)}</td>
                <td class="py-2 px-3 font-mono text-right text-muted">{fmtMs(q.totalTime)}</td>
                <td class="py-2 px-3 font-mono text-right text-muted">{fmtNum(q.rows)}</td>
                <td class="py-2 px-3 font-mono text-right {q.hitPercent < 90 ? 'text-red' : 'text-green'}">
                  {q.hitPercent?.toFixed(1)}%
                </td>
              </tr>
              {#if selected === q}
                <tr class="bg-surface/80">
                  <td colspan="7" class="px-4 py-3">
                    <div class="font-display text-xs text-muted uppercase tracking-wider mb-2">Full Query</div>
                    <pre class="font-mono text-xs text-text bg-bg border border-border rounded p-3 overflow-x-auto whitespace-pre-wrap">{q.query}</pre>
                    <div class="grid grid-cols-4 gap-4 mt-3 text-xs">
                      <div class="bg-bg border border-border rounded p-2">
                        <div class="text-muted mb-1">Std Dev</div>
                        <div class="font-mono text-text">{fmtMs(q.stddevTime)}</div>
                      </div>
                      <div class="bg-bg border border-border rounded p-2">
                        <div class="text-muted mb-1">Min Time</div>
                        <div class="font-mono text-text">{fmtMs(q.minTime)}</div>
                      </div>
                      <div class="bg-bg border border-border rounded p-2">
                        <div class="text-muted mb-1">Shared Blks Hit</div>
                        <div class="font-mono text-text">{fmtNum(q.sharedBlksHit)}</div>
                      </div>
                      <div class="bg-bg border border-border rounded p-2">
                        <div class="text-muted mb-1">Shared Blks Read</div>
                        <div class="font-mono text-text">{fmtNum(q.sharedBlksRead)}</div>
                      </div>
                    </div>
                  </td>
                </tr>
              {/if}
            {/each}
          </tbody>
        </table>
      </div>
    </div>
  {/if}
</div>
