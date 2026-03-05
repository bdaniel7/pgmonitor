<script>
  import { onMount } from 'svelte'
  import { apiFetch } from '../stores/auth.js'

  let users    = []
  let loading  = true
  let error    = null

  // New user form
  let newUsername = ''
  let newPassword = ''
  let newRole     = 'viewer'
  let saving      = false
  let formError   = null

  // Change password
  let changingId  = null
  let newPwd      = ''
  let pwdError    = null

  onMount(load)

  async function load() {
    loading = true; error = null
    try {
      const res = await apiFetch('/users')
      if (res.ok) users = await res.json()
      else error = `Failed (${res.status})`
    } catch(e) { error = e.message }
    finally { loading = false }
  }

  async function createUser() {
    saving = true; formError = null
    try {
      const res = await apiFetch('/users', {
        method: 'POST',
        body: JSON.stringify({ username: newUsername, password: newPassword, role: newRole })
      })
      if (res.ok) {
        users = [...users, await res.json()]
        newUsername = ''; newPassword = ''; newRole = 'viewer'
      } else {
        formError = (await res.json()).error
      }
    } catch(e) { formError = e.message }
    finally { saving = false }
  }

  async function deleteUser(id) {
    if (!confirm('Delete this user?')) return
    await apiFetch(`/users/${id}`, { method: 'DELETE' })
    users = users.filter(u => u.id !== id)
  }

  async function savePassword(id) {
    pwdError = null
    try {
      const res = await apiFetch(`/users/${id}/password`, {
        method: 'PATCH',
        body: JSON.stringify({ newPassword: newPwd })
      })
      if (!res.ok) pwdError = 'Failed to change password'
      else { changingId = null; newPwd = '' }
    } catch(e) { pwdError = e.message }
  }
</script>

<div class="p-6 flex flex-col h-full overflow-y-auto gap-6">
  <h1 class="font-display text-xl font-bold text-text flex-shrink-0">User Management</h1>

  <!-- Add user form -->
  <div class="bg-surface border border-border rounded-lg p-4 flex-shrink-0">
    <h2 class="font-display text-sm font-semibold text-text mb-3">Add User</h2>
    <div class="flex gap-3 flex-wrap items-end">
      <div class="flex flex-col gap-1">
        <label for="un" class="text-xs text-muted font-display">Username</label>
        <input id="un" bind:value={newUsername} class="px-3 py-1.5 text-sm rounded w-36" placeholder="username" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="pw" class="text-xs text-muted font-display">Password</label>
        <input id="pw" bind:value={newPassword} type="password" class="px-3 py-1.5 text-sm rounded w-36" placeholder="password" />
      </div>
      <div class="flex flex-col gap-1">
        <label for="roles" class="text-xs text-muted font-display">Role</label>
        <select id="roles" bind:value={newRole} class="px-3 py-1.5 text-sm rounded w-28">
          <option value="viewer">viewer</option>
          <option value="admin">admin</option>
        </select>
      </div>
      <button
        on:click={createUser}
        disabled={saving || !newUsername || !newPassword}
        class="px-4 py-1.5 rounded bg-accent text-white text-sm font-display font-semibold
               hover:bg-accent/90 disabled:opacity-40 transition-colors"
      >Add</button>
    </div>
    {#if formError}<p class="text-xs text-red mt-2">{formError}</p>{/if}
  </div>

  <!-- User list -->
  {#if loading}
    <p class="text-muted text-sm">Loading…</p>
  {:else if error}
    <p class="text-red text-sm">{error}</p>
  {:else}
    <div class="bg-surface border border-border rounded-lg overflow-hidden flex-shrink-0">
      <table class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-left">
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Username</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Role</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Created</th>
            <th class="px-4 py-2 text-xs text-muted font-display font-semibold">Actions</th>
          </tr>
        </thead>
        <tbody>
          {#each users as user}
            <tr class="border-b border-border/50 hover:bg-border/20">
              <td class="px-4 py-2 font-mono text-text">{user.username}</td>
              <td class="px-4 py-2">
                <span class="text-xs px-2 py-0.5 rounded font-display
                  {user.role === 'admin' ? 'bg-accent/10 text-accent border border-accent/20' : 'bg-border text-muted'}">
                  {user.role}
                </span>
              </td>
              <td class="px-4 py-2 text-muted text-xs font-mono">
                {new Date(user.createdAt).toLocaleDateString()}
              </td>
              <td class="px-4 py-2 flex gap-2">
                {#if changingId === user.id}
                  <input
                    bind:value={newPwd}
                    type="password"
                    placeholder="new password"
                    class="text-xs px-2 py-1 rounded w-28"
                  />
                  <button on:click={() => savePassword(user.id)}
                    class="text-xs px-2 py-1 rounded bg-green/10 text-green border border-green/20 hover:bg-green/20">
                    Save
                  </button>
                  <button on:click={() => { changingId = null; newPwd = '' }}
                    class="text-xs px-2 py-1 rounded text-muted hover:text-text">
                    Cancel
                  </button>
                  {#if pwdError}<span class="text-xs text-red">{pwdError}</span>{/if}
                {:else}
                  <button on:click={() => { changingId = user.id; pwdError = null }}
                    class="text-xs px-2 py-1 rounded border border-border text-muted hover:text-text hover:border-accent/40 transition-colors">
                    Change password
                  </button>
                  <button on:click={() => deleteUser(user.id)}
                    class="text-xs px-2 py-1 rounded border border-border text-muted hover:text-red hover:border-red/40 transition-colors">
                    Delete
                  </button>
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>
