<script>
  import { onMount, onDestroy } from 'svelte'
  import { snapshot } from '../stores/hub.js'
  import { apiFetch } from '../stores/auth.js'

  // Rolling history for sparklines
  let cpuHistory  = []
  let hitHistory  = []
  const MAX_PTS   = 60

  $: if ($snapshot) {
    cpuHistory = [...cpuHistory, $snapshot.cpu?.usagePercent ?? 0].slice(-MAX_PTS)
    hitHistory = [...hitHistory, $snapshot.io?.hitRatio ?? 0].slice(-MAX_PTS)
  }

  function fmt(bytes) {
    if (bytes > 1e9) return (bytes/1e9).toFixed(1) + ' GB'
    if (bytes > 1e6) return (bytes/1e6).toFixed(1) + ' MB'
    if (bytes > 1e3) return (bytes/1e3).toFixed(1) + ' KB'
    return bytes + ' B'
  }

  function pct(v) { return Math.round(v ?? 0) + '%' }

  function sparkPath(data, w=140, h=40) {
    if (!data.length) return ''
    const max = Math.max(...data, 1)
    const pts = data.map((v, i) => [
      (i / (data.length - 1)) * w,
      h - (v / max) * h
    ])
    return 'M' + pts.map(p => p.join(',')).join(' L')
  }
</script>

<div class="p-6 space-y-6 overflow-y-auto h-full">
  <div class="flex items-center justify-between">
    <h1 class="font-display text-xl font-bold text-text">System Overview</h1>
    <div class="text-xs text-muted font-mono">
      {#if $snapshot}
        Updated {new Date().toLocaleTimeString()}
      {:else}
        <span class="text-yellow">Waiting for data…</span>
      {/if}
    </div>
  </div>

  {#if $snapshot}
    <!-- KPI row -->
    <div class="grid grid-cols-4 gap-4">
      <!-- CPU -->
      <div class="bg-surface border border-border rounded-lg p-4">
        <div class="flex items-start justify-between mb-3">
          <div>
            <div class="text-xs text-muted font-display uppercase tracking-wider">CPU</div>
            <div class="text-3xl font-display font-bold mt-1
              {($snapshot.cpu?.usagePercent ?? 0) > 80 ? 'text-red' : ($snapshot.cpu?.usagePercent ?? 0) > 60 ? 'text-yellow' : 'text-green'}">
              {pct($snapshot.cpu?.usagePercent)}
            </div>
          </div>
          <div class="text-xs text-muted font-mono text-right">
            <div>{($snapshot.cpu?.loadAvg1 ?? 0).toFixed(2)}</div>
            <div>{($snapshot.cpu?.loadAvg5 ?? 0).toFixed(2)}</div>
            <div>{($snapshot.cpu?.loadAvg15 ?? 0).toFixed(2)}</div>
          </div>
        </div>
        <svg width="140" height="40" class="w-full">
          <path d={sparkPath(cpuHistory)} fill="none" stroke="#00ff9d" stroke-width="1.5" opacity="0.8"/>
          <path d={sparkPath(cpuHistory) + ' L140,40 L0,40 Z'} fill="url(#cpuGrad)" opacity="0.15"/>
          <defs>
            <linearGradient id="cpuGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stop-color="#00ff9d"/>
              <stop offset="100%" stop-color="#00ff9d" stop-opacity="0"/>
            </linearGradient>
          </defs>
        </svg>
      </div>

      <!-- Cache Hit -->
      <div class="bg-surface border border-border rounded-lg p-4">
        <div class="flex items-start justify-between mb-3">
          <div>
            <div class="text-xs text-muted font-display uppercase tracking-wider">Cache Hit</div>
            <div class="text-3xl font-display font-bold mt-1
              {($snapshot.io?.hitRatio ?? 0) < 90 ? 'text-red' : ($snapshot.io?.hitRatio ?? 0) < 95 ? 'text-yellow' : 'text-accent'}">
              {($snapshot.io?.hitRatio ?? 0).toFixed(1)}%
            </div>
          </div>
          <div class="text-xs text-muted font-mono text-right">
            <div class="text-green">↓ {fmt($snapshot.io?.blocksBytesRead ?? 0)}</div>
          </div>
        </div>
        <svg width="140" height="40" class="w-full">
          <path d={sparkPath(hitHistory)} fill="none" stroke="#00d4ff" stroke-width="1.5" opacity="0.8"/>
          <path d={sparkPath(hitHistory) + ' L140,40 L0,40 Z'} fill="url(#hitGrad)" opacity="0.15"/>
          <defs>
            <linearGradient id="hitGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stop-color="#00d4ff"/>
              <stop offset="100%" stop-color="#00d4ff" stop-opacity="0"/>
            </linearGradient>
          </defs>
        </svg>
      </div>

      <!-- Connections -->
      <div class="bg-surface border border-border rounded-lg p-4">
        <div class="text-xs text-muted font-display uppercase tracking-wider">Connections</div>
        <div class="text-3xl font-display font-bold mt-1
          {($snapshot.connections / $snapshot.maxConnections) > 0.8 ? 'text-red' : 'text-text'}">
          {$snapshot.connections}
          <span class="text-sm text-muted">/ {$snapshot.maxConnections}</span>
        </div>
        <div class="mt-3 h-2 bg-border rounded-full overflow-hidden">
          <div
            class="h-full rounded-full transition-all duration-500
              {($snapshot.connections / $snapshot.maxConnections) > 0.8 ? 'bg-red' : 'bg-accent'}"
            style="width: {Math.min(100, ($snapshot.connections / $snapshot.maxConnections) * 100).toFixed(1)}%"
          ></div>
        </div>
        <div class="mt-1 text-xs text-muted font-mono">
          {(($snapshot.connections / $snapshot.maxConnections) * 100).toFixed(1)}% utilized
        </div>
      </div>

      <!-- DB Size -->
      <div class="bg-surface border border-border rounded-lg p-4">
        <div class="text-xs text-muted font-display uppercase tracking-wider">Database Size</div>
        <div class="text-3xl font-display font-bold mt-1 text-accent">
          {fmt($snapshot.databaseSize ?? 0)}
        </div>
        <div class="mt-3 text-xs text-muted font-mono space-y-1">
          <div>Temp files: {$snapshot.io?.tempFilesCreated ?? 0}</div>
          <div>Temp bytes: {fmt($snapshot.io?.tempBytesWritten ?? 0)}</div>
        </div>
      </div>
    </div>

    <!-- Disk I/O -->
    {#if $snapshot.disks?.length}
      <div>
        <h2 class="font-display text-sm font-semibold text-text mb-3 uppercase tracking-wider">Disk I/O</h2>
        <div class="grid grid-cols-3 gap-4">
          {#each $snapshot.disks as disk}
            <div class="bg-surface border border-border rounded-lg p-4">
              <div class="flex items-center justify-between mb-3">
                <span class="font-mono text-accent text-sm">/dev/{disk.device}</span>
                <span class="text-xs text-muted">{disk.utilizationPercent?.toFixed(1)}% util</span>
              </div>
              <div class="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <div class="text-muted mb-0.5">Read</div>
                  <div class="font-mono text-green">{fmt(disk.readBytesPerSec)}/s</div>
                  <div class="text-muted">{disk.readOpsPerSec?.toFixed(0)} iops</div>
                </div>
                <div>
                  <div class="text-muted mb-0.5">Write</div>
                  <div class="font-mono text-yellow">{fmt(disk.writeBytesPerSec)}/s</div>
                  <div class="text-muted">{disk.writeOpsPerSec?.toFixed(0)} iops</div>
                </div>
              </div>
              <div class="mt-3 h-1.5 bg-border rounded-full overflow-hidden">
                <div class="h-full bg-yellow rounded-full" style="width: {Math.min(100, disk.utilizationPercent ?? 0).toFixed(1)}%"></div>
              </div>
            </div>
          {/each}
        </div>
      </div>
    {/if}
  {:else}
    <div class="flex items-center justify-center h-64 text-muted">
      <div class="text-center">
        <div class="text-4xl mb-3 opacity-30">◈</div>
        <div class="font-display">Waiting for metrics…</div>
        <div class="text-xs mt-1">Ensure the backend is running and PostgreSQL is accessible</div>
      </div>
    </div>
  {/if}
</div>
