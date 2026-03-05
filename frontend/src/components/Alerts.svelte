<script>
  import { onMount } from 'svelte'
  import { apiFetch } from '../stores/auth.js'
  import { liveAlerts } from '../stores/hub.js'

  let rules   = []
  let alerts  = []
  let tab     = 'alerts'
  let loading = true

  // New rule form
  let newRule = { name: '', metric: 'cpu_percent', threshold: 80, severity: 'Warning', enabled: true }

  const metrics = [
    { value: 'cpu_percent',      label: 'CPU %' },
    { value: 'cache_hit_ratio',  label: 'Cache Hit Ratio %' },
    { value: 'connection_pct',   label: 'Connection %' },
    { value: 'query_duration_s', label: 'Query Duration (s)' },
    { value: 'repl_lag_bytes',   label: 'Replication Lag (bytes)' },
  ]

  async function loadRules() {
    const res = await apiFetch('/alerts/rules')
    if (res.ok) rules = await res.json()
  }

  async function loadAlerts() {
    const res = await apiFetch('/alerts')
    if (res.ok) alerts = await res.json()
    loading = false
  }

  onMount(async () => {
    await Promise.all([loadRules(), loadAlerts()])
  })

  // Merge live alerts
  $: allAlerts = [
    ...$liveAlerts.filter(a => !alerts.some(e => e.id === a.id)),
    ...alerts
  ].sort((a, b) => new Date(b.triggeredAt) - new Date(a.triggeredAt))

  async function ack(id) {
    await apiFetch(`/alerts/${id}/acknowledge`, { method: 'POST' })
    await loadAlerts()
  }

  async function addRule() {
    if (!newRule.name) return
    const res = await apiFetch('/alerts/rules', {
      method: 'POST',
      body: JSON.stringify(newRule)
    })
    if (res.ok) {
      await loadRules()
      newRule = { name: '', metric: 'cpu_percent', threshold: 80, severity: 'Warning', enabled: true }
    }
  }

  async function deleteRule(id) {
    if (!confirm('Delete this rule?')) return
    await apiFetch(`/alerts/rules/${id}`, { method: 'DELETE' })
    await loadRules()
  }

  function severityStyle(s) {
    if (s === 'Critical') return 'bg-red/20 text-red border-red/30'
    if (s === 'Warning')  return 'bg-yellow/20 text-yellow border-yellow/30'
    return 'bg-accent/20 text-accent border-accent/30'
  }

  function fmtDate(d) {
    if (!d) return '—'
    return new Date(d).toLocaleString()
  }
</script>

<div class="p-6 flex flex-col h-full overflow-hidden">
  <div class="flex items-center justify-between mb-4 flex-shrink-0">
    <h1 class="font-display text-xl font-bold text-text">Alerting</h1>
    <div class="flex rounded border border-border overflow-hidden">
      <button
        on:click={() => tab = 'alerts'}
        class="px-3 py-1.5 text-xs font-display transition-colors
          {tab === 'alerts' ? 'bg-accent/20 text-accent' : 'text-muted hover:text-text'}">
        Alerts ({allAlerts.filter(a => !a.acknowledged && !a.resolvedAt).length})
      </button>
      <button
        on:click={() => tab = 'rules'}
        class="px-3 py-1.5 text-xs font-display transition-colors
          {tab === 'rules' ? 'bg-accent/20 text-accent' : 'text-muted hover:text-text'}">
        Rules ({rules.length})
      </button>
    </div>
  </div>

  {#if tab === 'alerts'}
    <div class="flex-1 overflow-y-auto space-y-2">
      {#if allAlerts.length === 0}
        <div class="flex items-center justify-center h-48 text-muted">
          <div class="text-center">
            <div class="text-3xl mb-2 opacity-20">✓</div>
            <div class="font-display">All clear — no alerts</div>
          </div>
        </div>
      {:else}
        {#each allAlerts as alert}
          <div class="bg-surface border rounded-lg p-4 {alert.acknowledged || alert.resolvedAt ? 'opacity-50' : ''}
            {alert.severity === 'Critical' ? 'border-red/40' : alert.severity === 'Warning' ? 'border-yellow/40' : 'border-accent/40'}">
            <div class="flex items-start justify-between gap-4">
              <div class="flex items-start gap-3">
                <span class="mt-0.5 inline-block px-2 py-0.5 rounded border text-xs font-display font-semibold {severityStyle(alert.severity)}">
                  {alert.severity}
                </span>
                <div>
                  <div class="font-display font-semibold text-text text-sm">{alert.ruleName}</div>
                  <div class="text-xs text-muted mt-0.5 font-mono">{alert.message}</div>
                  <div class="text-xs text-muted mt-1">{fmtDate(alert.triggeredAt)}</div>
                </div>
              </div>
              <div class="flex items-center gap-2 flex-shrink-0">
                {#if alert.resolvedAt}
                  <span class="text-xs text-green font-display">✓ Resolved</span>
                {:else if alert.acknowledged}
                  <span class="text-xs text-muted font-display">Acknowledged</span>
                {:else}
                  <button
                    on:click={() => ack(alert.id)}
                    class="text-xs px-2 py-1 rounded border border-border text-muted hover:text-text transition-colors font-display"
                  >Acknowledge</button>
                {/if}
              </div>
            </div>
          </div>
        {/each}
      {/if}
    </div>
  {:else}
    <div class="flex-1 overflow-hidden flex flex-col gap-4">
      <!-- Add rule form -->
      <div class="bg-surface border border-border rounded-lg p-4 flex-shrink-0">
        <h3 class="font-display text-sm font-semibold text-text mb-3">Add Rule</h3>
        <div class="grid grid-cols-5 gap-3">
          <input bind:value={newRule.name} placeholder="Rule name" class="px-2 py-1.5 text-xs rounded" />
          <select bind:value={newRule.metric} class="px-2 py-1.5 text-xs rounded">
            {#each metrics as m}
              <option value={m.value}>{m.label}</option>
            {/each}
          </select>
          <input bind:value={newRule.threshold} type="number" placeholder="Threshold" class="px-2 py-1.5 text-xs rounded" />
          <select bind:value={newRule.severity} class="px-2 py-1.5 text-xs rounded">
            <option>Info</option>
            <option>Warning</option>
            <option>Critical</option>
          </select>
          <button
            on:click={addRule}
            class="px-3 py-1.5 text-xs rounded bg-accent/20 text-accent border border-accent/30 hover:bg-accent/30 transition-colors font-display"
          >+ Add</button>
        </div>
      </div>

      <!-- Rules list -->
      <div class="flex-1 overflow-y-auto space-y-2">
        {#each rules as rule}
          <div class="bg-surface border border-border rounded-lg p-3 flex items-center gap-4">
            <div class="w-2 h-2 rounded-full {rule.enabled ? 'bg-green' : 'bg-muted'}"></div>
            <div class="flex-1">
              <div class="font-display text-sm text-text font-medium">{rule.name}</div>
              <div class="text-xs text-muted font-mono">{rule.metric} {rule.metric === 'cache_hit_ratio' ? '<' : '>'} {rule.threshold}</div>
            </div>
            <span class="px-2 py-0.5 rounded border text-xs {severityStyle(rule.severity)} font-display">{rule.severity}</span>
            <button
              on:click={() => deleteRule(rule.id)}
              class="text-muted hover:text-red transition-colors text-sm"
            >✕</button>
          </div>
        {/each}
      </div>
    </div>
  {/if}
</div>
