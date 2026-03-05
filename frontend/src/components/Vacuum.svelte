<script>
  import { onMount, onDestroy } from 'svelte'
  import { apiFetch } from '../stores/auth.js'

  let activity = []
  let bloat    = []
  let loading  = true
  let tab      = 'activity'
  let interval

  async function load() {
    try {
      const [r1, r2] = await Promise.all([
        apiFetch('/vacuum/activity'),
        apiFetch('/vacuum/bloat')
      ])
      if (r1.ok) activity = await r1.json()
      if (r2.ok) bloat    = await r2.json()
    } finally { loading = false }
  }

  onMount(() => { load(); interval = setInterval(load, 5000) })
  onDestroy(() => clearInterval(interval))

  function fmt(b) {
    if (b > 1e9) return (b/1e9).toFixed(1) + ' GB'
    if (b > 1e6) return (b/1e6).toFixed(1) + ' MB'
    if (b > 1e3) return (b/1e3).toFixed(1) + ' KB'
    return b + ' B'
  }

  function fmtDate(d) {
    if (!d) return 'Never'
    return new Date(d).toLocaleString()
  }

  function pct(scanned, total) {
    if (!total) return 0
    return Math.min(100, (scanned / total) * 100)
  }
</script>

<div class="p-6 flex flex-col h-full overflow-hidden">
  <div class="flex items-center justify-between mb-4 flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">Vacuum Tracking</h1>
    <div class="flex items-center gap-3">
      <div class="flex rounded border border-border overflow-hidden">
        <button
          on:click={() => tab = 'activity'}
          class="px-3 py-1.5 text-xs font-display transition-colors
            {tab === 'activity' ? 'bg-accent/20 text-accent' : 'text-muted hover:text-text'}">
          Active ({activity.length})
        </button>
        <button
          on:click={() => tab = 'bloat'}
          class="px-3 py-1.5 text-xs font-display transition-colors
            {tab === 'bloat' ? 'bg-accent/20 text-accent' : 'text-muted hover:text-text'}">
          Bloat Analysis
        </button>
      </div>
      <button on:click={load} class="text-xs px-3 py-1.5 rounded border border-border text-muted hover:text-accent transition-colors font-display">↺</button>
    </div>
  </div>

  {#if loading}
    <div class="flex-1 flex items-center justify-center text-muted">Loading…</div>
  {:else if tab === 'activity'}
    {#if activity.length === 0}
      <div class="flex-1 flex items-center justify-center text-muted">
        <div class="text-center">
          <div class="text-3xl mb-2 opacity-20">🧹</div>
          <div class="font-display">No vacuum operations running</div>
        </div>
      </div>
    {:else}
      <div class="flex-1 overflow-y-auto space-y-4">
        {#each activity as v}
          <div class="bg-surface border border-accent/20 rounded-lg p-4">
            <div class="flex items-center justify-between mb-3">
              <div class="flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-accent blink"></span>
                <span class="font-mono text-accent">{v.schema}.{v.table}</span>
              </div>
              <div class="flex items-center gap-3 text-xs text-muted">
                <span>PID: <span class="font-mono text-text">{v.pid}</span></span>
                <span>DB: <span class="font-mono text-text">{v.database}</span></span>
                <span class="font-display text-accent">{v.phase}</span>
              </div>
            </div>
            <div class="space-y-2">
              <div>
                <div class="flex justify-between text-xs text-muted mb-1">
                  <span>Heap scanned</span>
                  <span class="font-mono">{v.heapBlksScanned} / {v.heapBlksTotal}</span>
                </div>
                <div class="h-2 bg-border rounded-full overflow-hidden">
                  <div class="h-full bg-accent rounded-full transition-all"
                       style="width: {pct(v.heapBlksScanned, v.heapBlksTotal).toFixed(1)}%"></div>
                </div>
              </div>
              <div>
                <div class="flex justify-between text-xs text-muted mb-1">
                  <span>Heap vacuumed</span>
                  <span class="font-mono">{v.heapBlksvacuumed} / {v.heapBlksTotal}</span>
                </div>
                <div class="h-2 bg-border rounded-full overflow-hidden">
                  <div class="h-full bg-green rounded-full transition-all"
                       style="width: {pct(v.heapBlksvacuumed, v.heapBlksTotal).toFixed(1)}%"></div>
                </div>
              </div>
            </div>
            <div class="mt-3 grid grid-cols-3 gap-3 text-xs">
              <div><span class="text-muted">Dead tuples: </span><span class="font-mono text-red">{v.numDeadTuples}</span></div>
              <div><span class="text-muted">Max dead: </span><span class="font-mono text-muted">{v.maxDeadTuples}</span></div>
              <div><span class="text-muted">Index passes: </span><span class="font-mono text-text">{v.indexVacuumCount}</span></div>
            </div>
          </div>
        {/each}
      </div>
    {/if}
  {:else}
    <div class="flex-1 overflow-y-auto">
      <table class="w-full text-xs">
        <thead class="sticky top-0 bg-bg">
          <tr class="border-b border-border">
            <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Table</th>
            <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Size</th>
            <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Live</th>
            <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Dead</th>
            <th class="text-right py-2 px-3 text-muted font-display uppercase tracking-wider">Bloat%</th>
            <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Last Vacuum</th>
            <th class="text-left py-2 px-3 text-muted font-display uppercase tracking-wider">Last AutoVac</th>
          </tr>
        </thead>
        <tbody>
          {#each bloat as t}
            <tr class="border-b border-border/50 hover:bg-surface/60 transition-colors">
              <td class="py-2 px-3 font-mono text-text">{t.schema}.{t.tableName}</td>
              <td class="py-2 px-3 font-mono text-muted text-right">{fmt(t.sizeBytes)}</td>
              <td class="py-2 px-3 font-mono text-green text-right">{t.liveTuples.toLocaleString()}</td>
              <td class="py-2 px-3 font-mono {t.deadTuples > 10000 ? 'text-red' : 'text-yellow'} text-right">
                {t.deadTuples.toLocaleString()}
              </td>
              <td class="py-2 px-3 font-mono text-right">
                <div class="flex items-center justify-end gap-2">
                  <div class="w-16 h-1.5 bg-border rounded-full overflow-hidden">
                    <div class="h-full rounded-full {t.bloatEstimatePercent > 20 ? 'bg-red' : t.bloatEstimatePercent > 10 ? 'bg-yellow' : 'bg-green'}"
                         style="width: {Math.min(100, t.bloatEstimatePercent).toFixed(1)}%"></div>
                  </div>
                  <span class="{t.bloatEstimatePercent > 20 ? 'text-red' : t.bloatEstimatePercent > 10 ? 'text-yellow' : 'text-muted'}">
                    {t.bloatEstimatePercent.toFixed(1)}%
                  </span>
                </div>
              </td>
              <td class="py-2 px-3 text-muted text-xs">{fmtDate(t.lastVacuum || t.lastAutoVacuum)}</td>
              <td class="py-2 px-3 text-muted text-xs">{fmtDate(t.lastAutoVacuum)}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
