<script>
  import { onMount } from 'svelte'
  import { apiFetch } from '../stores/auth.js'
  import { connections, loadConnections } from '../stores/connections.js'

  let sql           = 'SELECT * FROM pg_stat_activity LIMIT 10;'
  let selectedConn  = null   // null = primary
  let result        = null
  let loading       = false

  onMount(() => loadConnections())

  async function run() {
    loading = true; result = null
    try {
      const res = await apiFetch('/sql/run', {
        method: 'POST',
        body: JSON.stringify({ connectionId: selectedConn, sql })
      })
      result = await res.json()
    } catch(e) {
      result = { error: e.message, columns: [], rows: [], rowCount: 0, executionTimeMs: 0 }
    } finally {
      loading = false
    }
  }

  function handleKey(e) {
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') run()
  }
</script>

<div class="p-6 flex flex-col h-full overflow-hidden gap-4">
  <div class="flex items-center justify-between flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">SQL Runner</h1>
    <span class="text-xs text-muted">Ctrl+Enter to run</span>
  </div>

  <!-- Toolbar -->
  <div class="flex gap-3 items-center flex-shrink-0">
    <select bind:value={selectedConn} class="text-sm px-3 py-1.5 rounded w-48">
      <option value={null}>Primary connection</option>
      {#each $connections as c}
        <option value={c.id}>{c.name}</option>
      {/each}
    </select>
    <button on:click={run} disabled={loading}
      class="px-4 py-1.5 rounded bg-accent text-white text-sm font-display font-semibold
             hover:bg-accent/90 disabled:opacity-40 transition-colors ml-auto">
      {loading ? 'Running…' : '▸ Run'}
    </button>
  </div>

  <!-- Editor -->
  <div class="flex-shrink-0 border border-border rounded-lg overflow-hidden">
    <textarea
      bind:value={sql}
      on:keydown={handleKey}
      rows="6"
      spellcheck="false"
      class="w-full p-4 text-sm font-mono resize-none bg-bg focus:outline-none"
      placeholder="Enter SQL statement…"
    ></textarea>
  </div>

  <!-- Results -->
  <div class="flex-1 overflow-hidden">
    {#if result?.error}
      <div class="bg-red/10 border border-red/30 rounded-lg p-4">
        <div class="text-xs text-red font-display uppercase tracking-wider mb-1">Error</div>
        <pre class="font-mono text-xs text-red/80 whitespace-pre-wrap">{result.error}</pre>
      </div>

    {:else if result}
      <div class="h-full flex flex-col overflow-hidden border border-border rounded-lg">
        <!-- Stats bar -->
        <div class="flex items-center gap-4 px-4 py-2 bg-surface border-b border-border flex-shrink-0">
          <span class="font-display text-xs text-muted">
            {result.rowCount} row{result.rowCount !== 1 ? 's' : ''}
          </span>
          <span class="font-mono text-xs text-green">{result.executionTimeMs.toFixed(1)}ms</span>
        </div>

        <!-- Table -->
        <div class="flex-1 overflow-auto">
          {#if result.columns.length === 0}
            <div class="p-4 text-sm text-muted font-display">
              Query executed successfully. No rows returned.
            </div>
          {:else}
            <table class="w-full text-xs font-mono border-collapse">
              <thead class="sticky top-0 bg-surface z-10">
                <tr>
                  {#each result.columns as col}
                    <th class="px-3 py-2 text-left text-muted font-display font-semibold
                               border-b border-r border-border whitespace-nowrap">
                      {col}
                    </th>
                  {/each}
                </tr>
              </thead>
              <tbody>
                {#each result.rows as row, i}
                  <tr class="{i % 2 === 0 ? 'bg-bg' : 'bg-surface'} hover:bg-accent/5">
                    {#each row as cell}
                      <td class="px-3 py-1.5 border-b border-r border-border/50 text-text
                                 whitespace-nowrap max-w-xs overflow-hidden text-ellipsis
                                 {cell === 'NULL' ? 'text-muted italic' : ''}">
                        {cell}
                      </td>
                    {/each}
                  </tr>
                {/each}
              </tbody>
            </table>
          {/if}
        </div>
      </div>

    {:else}
      <div class="h-full flex items-center justify-center border border-dashed border-border/50 rounded-lg text-muted">
        <div class="text-center">
          <div class="text-3xl opacity-20 mb-2">▸</div>
          <div class="font-display text-sm">Run a query to see results</div>
        </div>
      </div>
    {/if}
  </div>
</div>
