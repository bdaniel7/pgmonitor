<script>
  import { onMount, onDestroy } from 'svelte'
  import { apiFetch } from '../stores/auth.js'

  let status  = null
  let loading = true
  let interval

  async function load() {
    try {
      const res = await apiFetch('/replication')
      if (res.ok) status = await res.json()
    } finally { loading = false }
  }

  onMount(() => { load(); interval = setInterval(load, 5000) })
  onDestroy(() => clearInterval(interval))

  function fmtLag(ts) {
    if (!ts) return '—'
    // TimeSpan from JSON: "00:00:00.123456"
    const parts = ts.split(':')
    if (parts.length < 3) return ts
    const [h, m, s] = parts
    const totalMs = parseInt(h)*3600000 + parseInt(m)*60000 + parseFloat(s)*1000
    if (totalMs < 1000) return totalMs.toFixed(0) + 'ms'
    return (totalMs/1000).toFixed(2) + 's'
  }

  function fmt(b) {
    if (!b && b !== 0) return '—'
    if (b > 1e9) return (b/1e9).toFixed(1) + ' GB'
    if (b > 1e6) return (b/1e6).toFixed(1) + ' MB'
    if (b > 1e3) return (b/1e3).toFixed(1) + ' KB'
    return b + ' B'
  }
</script>

<div class="p-6 space-y-6 overflow-y-auto h-full">
  <div class="flex items-center justify-between">
    <h1 class="font-display text-xl font-bold text-text">Replication</h1>
    <button on:click={load} class="text-xs px-3 py-1.5 rounded border border-border text-muted hover:text-accent hover:border-accent/40 transition-colors font-display">↺ Refresh</button>
  </div>

  {#if loading}
    <div class="text-muted text-sm">Loading…</div>
  {:else if status}
    <!-- Role badge -->
    <div class="flex items-center gap-3">
      <div class="inline-flex items-center gap-2 px-3 py-1.5 rounded border
        {status.isReplica ? 'border-yellow/40 bg-yellow/10 text-yellow' : 'border-accent/40 bg-accent/10 text-accent'}">
        <span class="w-2 h-2 rounded-full {status.isReplica ? 'bg-yellow' : 'bg-accent'} blink"></span>
        <span class="font-display font-semibold text-sm">
          {status.isReplica ? 'Replica (Standby)' : 'Primary'}
        </span>
      </div>
    </div>

    <!-- Replicas -->
    {#if status.replicas?.length > 0}
      <div>
        <h2 class="font-display text-sm font-semibold text-text mb-3 uppercase tracking-wider">
          Connected Replicas ({status.replicas.length})
        </h2>
        <div class="space-y-3">
          {#each status.replicas as r}
            <div class="bg-surface border border-border rounded-lg p-4">
              <div class="flex items-center justify-between mb-3">
                <span class="font-mono text-accent">{r.clientAddr}</span>
                <div class="flex items-center gap-2">
                  <span class="text-xs px-2 py-0.5 rounded-full
                    {r.syncState === 'sync' ? 'bg-green/20 text-green border border-green/30' : 'bg-muted/20 text-muted border border-muted/30'}
                    font-display">{r.syncState}</span>
                  <span class="text-xs text-muted">{r.state}</span>
                </div>
              </div>
              <div class="grid grid-cols-3 gap-4 text-xs">
                <div>
                  <div class="text-muted mb-1">Write Lag</div>
                  <div class="font-mono {r.writeLag ? 'text-yellow' : 'text-green'}">{fmtLag(r.writeLag)}</div>
                </div>
                <div>
                  <div class="text-muted mb-1">Flush Lag</div>
                  <div class="font-mono {r.flushLag ? 'text-yellow' : 'text-green'}">{fmtLag(r.flushLag)}</div>
                </div>
                <div>
                  <div class="text-muted mb-1">Replay Lag</div>
                  <div class="font-mono {r.replayLag ? 'text-red' : 'text-green'}">{fmtLag(r.replayLag)}</div>
                </div>
              </div>
              <div class="grid grid-cols-4 gap-4 mt-3 text-xs text-muted font-mono">
                <div><span class="text-muted/60">Sent  </span>{r.sentLsn}</div>
                <div><span class="text-muted/60">Write </span>{r.writeLsn}</div>
                <div><span class="text-muted/60">Flush </span>{r.flushLsn}</div>
                <div><span class="text-muted/60">Replay</span>{r.replayLsn}</div>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {:else}
      <div class="bg-surface border border-border rounded-lg p-6 text-center text-muted">
        <div class="text-2xl mb-2 opacity-30">⇄</div>
        <div class="font-display">No streaming replicas connected</div>
      </div>
    {/if}

    <!-- Replication Slots -->
    {#if status.slots?.length > 0}
      <div>
        <h2 class="font-display text-sm font-semibold text-text mb-3 uppercase tracking-wider">
          Replication Slots ({status.slots.length})
        </h2>
        <div class="space-y-2">
          {#each status.slots as slot}
            <div class="bg-surface border border-border rounded-lg p-4 flex items-center gap-6">
              <div class="flex items-center gap-2">
                <span class="w-2 h-2 rounded-full {slot.active ? 'bg-green blink' : 'bg-red'}"></span>
                <span class="font-mono text-text text-sm">{slot.slotName}</span>
              </div>
              <span class="text-xs text-muted">{slot.slotType}</span>
              {#if slot.plugin}<span class="text-xs text-accent font-mono">{slot.plugin}</span>{/if}
              <div class="ml-auto text-xs">
                {#if slot.lagBytes !== null && slot.lagBytes !== undefined}
                  <span class="font-mono {slot.lagBytes > 10485760 ? 'text-red' : slot.lagBytes > 1048576 ? 'text-yellow' : 'text-green'}">
                    lag: {fmt(slot.lagBytes)}
                  </span>
                {/if}
              </div>
            </div>
          {/each}
        </div>
      </div>
    {/if}
  {:else}
    <div class="text-muted text-sm">Unable to load replication status.</div>
  {/if}
</div>
