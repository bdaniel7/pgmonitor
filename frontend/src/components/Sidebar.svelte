<script>
  import { link, location } from 'svelte-spa-router'
  import { connected, liveAlerts } from '../stores/hub.js'
  import { auth } from '../stores/auth.js'

  const nav = [
    { section: 'Monitor' },
    { path: '/overview',     label: 'Overview',     icon: '◈' },
    { path: '/live-queries', label: 'Live Queries',  icon: '⟳' },
    { path: '/slow-queries', label: 'Slow Queries',  icon: '⏱' },
    { path: '/replication',  label: 'Replication',   icon: '⇄' },
    { path: '/locks',        label: 'Locks',         icon: '🔒' },
    { path: '/vacuum',       label: 'Vacuum',        icon: '🧹' },
    { path: '/alerts',       label: 'Alerts',        icon: '⚠' },
    { section: 'Tools' },
    { path: '/sql-runner',   label: 'SQL Runner',     icon: '▸' },
    { path: '/sql-editor',   label: 'Explain Analyze',icon: '⬡' },
    { section: 'Admin' },
    { path: '/users',        label: 'Users',          icon: '👤' },
    { path: '/connections',  label: 'Connections',    icon: '⛁' },
  ]

  $: pendingAlerts = $liveAlerts.filter(a => !a.acknowledged).length
  $: active = $location   // reactive current path from the router
</script>

<aside class="w-56 flex-shrink-0 bg-surface border-r border-border flex flex-col h-screen">
  <!-- Logo -->
  <div class="px-5 py-4 border-b border-border flex items-center gap-2">
    <div class="w-2 h-2 rounded-full {$connected ? 'bg-green blink' : 'bg-red'}"></div>
    <span class="font-display font-bold text-text tracking-tight">
      PG<span class="text-accent">Monitor</span>
    </span>
  </div>

  <!-- Nav -->
  <nav class="flex-1 p-3 overflow-y-auto">
    {#each nav as item}
      {#if item.section}
        <div class="px-3 pt-4 pb-1 text-xs text-muted font-display font-semibold uppercase tracking-wider">
          {item.section}
        </div>
      {:else}
      <a
        href={item.path}
        use:link
          class="flex items-center gap-3 px-3 py-2 rounded text-sm transition-colors no-underline mb-0.5
               {active === item.path
                 ? 'bg-accent/10 text-accent border border-accent/20'
                 : 'text-muted hover:text-text hover:bg-black/5'}"
      >
        <span class="text-base w-5 text-center opacity-70">{item.icon}</span>
        <span class="font-display">{item.label}</span>
        {#if item.path === '/alerts' && pendingAlerts > 0}
          <span class="ml-auto bg-red text-white text-xs rounded-full w-5 h-5 flex items-center justify-center font-bold">
            {pendingAlerts > 9 ? '9+' : pendingAlerts}
          </span>
        {/if}
      </a>
      {/if}
    {/each}
  </nav>

  <!-- Footer -->
  <div class="p-3 border-t border-border">
    <div class="flex items-center gap-2 px-3 py-2">
      <div class="w-7 h-7 rounded-full bg-accent/20 border border-accent/30 flex items-center justify-center text-accent text-xs font-bold font-mono">A</div>
      <div class="flex-1 min-w-0">
        <div class="text-xs font-display text-text truncate">admin</div>
        <div class="text-xs text-muted">Administrator</div>
      </div>
      <button
        on:click={() => auth.logout()}
        class="text-muted hover:text-red transition-colors text-xs"
        title="Sign out"
      >⏻</button>
    </div>
  </div>
</aside>
