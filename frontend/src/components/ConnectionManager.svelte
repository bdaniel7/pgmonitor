<script>
  import { onMount } from 'svelte'
  import { connections, loadConnections, addConnection, deleteConnection, testConnection } from '../stores/connections.js'

  let loading = true
  let error   = null

  // Form
  let form = { name: '', host: 'localhost', port: 5432, database: 'postgres', username: 'postgres', password: '' }
  let saving    = false
  let formError = null
  let testResult = null

  onMount(async () => {
    try { await loadConnections() }
    catch(e) { error = e.message }
    finally { loading = false }
  })

  async function handleAdd() {
    saving = true; formError = null; testResult = null
    try {
      await addConnection(form)
      form = { name: '', host: 'localhost', port: 5432, database: 'postgres', username: 'postgres', password: '' }
    } catch(e) { formError = e.message }
    finally { saving = false }
  }

  async function handleTest() {
    testResult = null
    const ok = await testConnection(form)
    testResult = ok ? 'success' : 'fail'
  }

  async function handleDelete(id) {
    if (!confirm('Remove this connection?')) return
    await deleteConnection(id)
  }
</script>

<div class="p-6 flex flex-col h-full overflow-y-auto gap-6">
  <h1 class="font-display text-xl font-bold text-text flex-shrink-0">Database Connections</h1>

  <!-- Add connection form -->
  <div class="bg-surface border border-border rounded-lg p-4 flex-shrink-0">
    <h2 class="font-display text-sm font-semibold text-text mb-3">Add Connection</h2>
    <div class="grid grid-cols-2 gap-3 mb-3">
      <div class="flex flex-col gap-1">
        <label for="n" class="text-xs text-muted font-display">Name</label>
        <input id="n" bind:value={form.name} class="px-3 py-1.5 text-sm rounded" placeholder="e.g. Production" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="h" class="text-xs text-muted font-display">Host</label>
        <input id="h" bind:value={form.host} class="px-3 py-1.5 text-sm rounded" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="p" class="text-xs text-muted font-display">Port</label>
        <input id="p" bind:value={form.port} type="number" class="px-3 py-1.5 text-sm rounded" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="db" class="text-xs text-muted font-display">Database</label>
        <input id="db" bind:value={form.database} class="px-3 py-1.5 text-sm rounded" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="u" class="text-xs text-muted font-display">Username</label>
        <input id="u" bind:value={form.username} class="px-3 py-1.5 text-sm rounded" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="pw" class="text-xs text-muted font-display">Password</label>
        <input id="pw" bind:value={form.password} type="password" class="px-3 py-1.5 text-sm rounded" />
      </div>
    </div>
    <div class="flex gap-2 items-center">
      <button on:click={handleTest}
        class="px-3 py-1.5 rounded border border-border text-sm font-display text-muted hover:text-text hover:border-accent/40 transition-colors">
        Test
      </button>
      {#if testResult === 'success'}
        <span class="text-xs text-green font-display">✓ Connected</span>
      {:else if testResult === 'fail'}
        <span class="text-xs text-red font-display">✗ Failed</span>
      {/if}
      <button on:click={handleAdd} disabled={saving || !form.name || !form.host}
        class="ml-auto px-4 py-1.5 rounded bg-accent text-white text-sm font-display font-semibold
               hover:bg-accent/90 disabled:opacity-40 transition-colors">
        Add
      </button>
    </div>
    {#if formError}<p class="text-xs text-red mt-2">{formError}</p>{/if}
  </div>

  <!-- Connection list -->
  {#if loading}
    <p class="text-muted text-sm">Loading…</p>
  {:else if error}
    <p class="text-red text-sm">{error}</p>
  {:else if $connections.length === 0}
    <p class="text-muted text-sm">No additional connections. The primary connection is configured in appsettings.json.</p>
  {:else}
    <div class="bg-surface border border-border rounded-lg overflow-hidden">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-left">
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Name</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Host</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Database</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">User</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold"></th>
          </tr>
        </thead>
        <tbody>
          {#each $connections as conn}
            <tr class="border-b border-border/50 hover:bg-border/20">
              <td class="px-4 py-2 font-display text-text font-medium">{conn.name}</td>
              <td class="px-4 py-2 font-mono text-sm text-muted">{conn.host}:{conn.port}</td>
              <td class="px-4 py-2 font-mono text-sm text-muted">{conn.database}</td>
              <td class="px-4 py-2 font-mono text-sm text-muted">{conn.username}</td>
              <td class="px-4 py-2">
                {#if !conn.isPrimary}
                  <button on:click={() => handleDelete(conn.id)}
                    class="text-xs px-2 py-1 rounded border border-border text-muted hover:text-red hover:border-red/40 transition-colors">
                    Remove
                  </button>
                {:else}
                  <span class="text-xs text-muted font-display">primary</span>
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
