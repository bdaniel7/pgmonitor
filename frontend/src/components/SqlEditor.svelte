<script>
  import { apiFetch } from '../stores/auth.js'

  let query    = 'SELECT * FROM pg_stat_activity LIMIT 10;'
  let database = 'postgres'
  let result   = null
  let error    = null
  let loading  = false

  async function runExplain() {
    loading = true; result = null; error = null
    try {
      const res = await apiFetch('/queries/explain', {
        method: 'POST',
        body: JSON.stringify({ query, database })
      })
      if (res.ok) {
        result = await res.json()
      } else {
        const e = await res.json()
        error = e.error
      }
    } catch(e) {
      error = e.message
    } finally {
      loading = false
    }
  }

  // Syntax highlight helpers
  const keywords = ['SELECT','FROM','WHERE','JOIN','ON','LEFT','RIGHT','INNER','OUTER','GROUP BY','ORDER BY','HAVING','LIMIT','OFFSET','INSERT','UPDATE','DELETE','WITH','UNION','EXCEPT','INTERSECT','AS','AND','OR','NOT','IN','EXISTS','BETWEEN','LIKE','IS','NULL','TRUE','FALSE','DISTINCT','COUNT','SUM','AVG','MAX','MIN']

  function highlight(plan) {
    if (!plan) return ''
    return plan
      .replace(/Seq Scan/g, '<span class="text-yellow">Seq Scan</span>')
      .replace(/Index Scan/g, '<span class="text-green">Index Scan</span>')
      .replace(/Index Only Scan/g, '<span class="text-green">Index Only Scan</span>')
      .replace(/Hash Join/g, '<span class="text-accent">Hash Join</span>')
      .replace(/Nested Loop/g, '<span class="text-accent">Nested Loop</span>')
      .replace(/Merge Join/g, '<span class="text-accent">Merge Join</span>')
      .replace(/cost=[\d.]+\.\.[\d.]+/g, m => `<span class="text-muted">${m}</span>`)
      .replace(/rows=\d+/g, m => `<span class="text-text">${m}</span>`)
      .replace(/actual time=[\d.]+\.\.[\d.]+/g, m => `<span class="text-green">${m}</span>`)
      .replace(/Execution Time: [\d.]+/g, m => `<span class="text-accent font-bold">${m}</span>`)
  }

  const examples = [
    { label: 'Active sessions',   q: 'SELECT pid, usename, state, query FROM pg_stat_activity LIMIT 20;' },
    { label: 'Table sizes',       q: "SELECT relname, pg_size_pretty(pg_total_relation_size(oid)) AS size FROM pg_class WHERE relkind='r' ORDER BY pg_total_relation_size(oid) DESC LIMIT 20;" },
    { label: 'Index usage',       q: 'SELECT relname, indexrelname, idx_scan, idx_tup_read FROM pg_stat_user_indexes ORDER BY idx_scan DESC LIMIT 20;' },
    { label: 'Unused indexes',    q: 'SELECT relname, indexrelname, idx_scan FROM pg_stat_user_indexes WHERE idx_scan = 0;' },
    { label: 'Cache hit ratio',   q: 'SELECT blks_hit::float/(blks_read+blks_hit+1)*100 AS ratio, datname FROM pg_stat_database ORDER BY ratio DESC;' },
  ]
</script>

<div class="p-6 flex flex-col h-full overflow-hidden gap-4">
  <div class="flex items-center justify-between flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">SQL Editor</h1>
    <span class="text-xs text-muted">Runs EXPLAIN (ANALYZE, BUFFERS)</span>
  </div>

  <!-- Examples -->
  <div class="flex gap-2 flex-wrap flex-shrink-0">
    {#each examples as ex}
      <button
        on:click={() => query = ex.q}
        class="text-xs px-2 py-1 rounded border border-border/60 text-muted hover:text-accent hover:border-accent/40 transition-colors font-display"
      >{ex.label}</button>
    {/each}
  </div>

  <!-- Editor -->
  <div class="flex-shrink-0">
    <div class="border border-border rounded-lg overflow-hidden">
      <div class="flex items-center justify-between bg-surface px-4 py-2 border-b border-border">
        <div class="flex items-center gap-3">
          <span class="text-xs text-muted font-display">Database:</span>
          <input bind:value={database} class="text-xs px-2 py-0.5 rounded w-32" />
        </div>
        <button
          on:click={runExplain}
          disabled={loading}
          class="flex items-center gap-2 px-4 py-1.5 rounded bg-accent text-bg text-xs font-display font-semibold
                 hover:bg-accent/90 transition-colors disabled:opacity-50"
        >
          {#if loading}
            <span class="blink">▸</span> Running…
          {:else}
            ▸ Explain Analyze
          {/if}
        </button>
      </div>
      <textarea
        bind:value={query}
        rows="6"
        class="w-full p-4 text-sm font-mono resize-none border-0 bg-bg focus:outline-none"
        spellcheck="false"
        placeholder="Enter SQL…"
      ></textarea>
    </div>
  </div>

  <!-- Result -->
  <div class="flex-1 overflow-hidden">
    {#if error}
      <div class="h-full overflow-y-auto">
        <div class="bg-red/10 border border-red/30 rounded-lg p-4">
          <div class="font-display text-xs text-red uppercase tracking-wider mb-2">Error</div>
          <pre class="font-mono text-xs text-red/80">{error}</pre>
        </div>
      </div>
    {:else if result}
      <div class="h-full flex flex-col overflow-hidden border border-border rounded-lg">
        <div class="flex items-center justify-between px-4 py-2 bg-surface border-b border-border flex-shrink-0">
          <span class="font-display text-xs font-semibold text-text uppercase tracking-wider">Execution Plan</span>
          {#if result.executionTime}
            <span class="font-mono text-xs text-green">{result.executionTime?.toFixed(3)}ms</span>
          {/if}
        </div>
        <div class="flex-1 overflow-y-auto p-4 bg-bg">
          <pre class="font-mono text-xs leading-relaxed">{@html highlight(result.plan)}</pre>
        </div>
      </div>
    {:else}
      <div class="h-full flex items-center justify-center text-muted border border-dashed border-border/50 rounded-lg">
        <div class="text-center">
          <div class="text-4xl opacity-20 mb-2">▸</div>
          <div class="font-display text-sm">Run EXPLAIN ANALYZE to see the query plan</div>
        </div>
      </div>
    {/if}
  </div>
</div>
