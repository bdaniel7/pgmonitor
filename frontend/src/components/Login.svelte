<script>
  import { auth } from '../stores/auth.js'
  import { startHub } from '../stores/hub.js'

  let username = ''
  let password = ''
  let error    = ''
  let loading  = false

  async function login() {
    if (!username || !password) { error = 'Enter credentials'; return }
    loading = true; error = ''
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      })
      if (!res.ok) { error = 'Invalid credentials'; return }
      const data = await res.json()
      auth.login(data.token)
      await startHub()
    } catch(e) {
      error = 'Connection failed'
    } finally {
      loading = false
    }
  }
</script>

<div class="min-h-screen bg-bg flex items-center justify-center">
  <!-- Background grid -->
  <div class="absolute inset-0 opacity-5"
       style="background-image: linear-gradient(#00d4ff 1px, transparent 1px), linear-gradient(90deg, #00d4ff 1px, transparent 1px); background-size: 40px 40px;">
  </div>

  <div class="relative z-10 w-full max-w-sm">
    <!-- Logo -->
    <div class="text-center mb-10">
      <div class="inline-flex items-center gap-3 mb-3">
        <svg width="36" height="36" viewBox="0 0 36 36" fill="none">
          <rect x="1" y="1" width="34" height="34" rx="4" stroke="#00d4ff" stroke-width="1.5"/>
          <circle cx="18" cy="18" r="8" stroke="#00d4ff" stroke-width="1.5" fill="none"/>
          <circle cx="18" cy="18" r="3" fill="#00d4ff"/>
          <line x1="18" y1="1" x2="18" y2="10" stroke="#00d4ff" stroke-width="1.5"/>
          <line x1="18" y1="26" x2="18" y2="35" stroke="#00d4ff" stroke-width="1.5"/>
          <line x1="1" y1="18" x2="10" y2="18" stroke="#00d4ff" stroke-width="1.5"/>
          <line x1="26" y1="18" x2="35" y2="18" stroke="#00d4ff" stroke-width="1.5"/>
        </svg>
        <span class="font-display text-2xl font-bold text-text tracking-tight">
          PG<span class="text-accent">Monitor</span>
        </span>
      </div>
      <p class="text-muted text-sm">PostgreSQL Observability Platform</p>
    </div>

    <div class="bg-surface border border-border rounded-lg p-8">
      <h2 class="font-display text-lg font-semibold text-text mb-6">Sign in</h2>

      {#if error}
        <div class="mb-4 px-3 py-2 rounded bg-red/10 border border-red/30 text-red text-sm">{error}</div>
      {/if}

      <div class="space-y-4">
        <div>
          <label class="block text-xs text-muted mb-1 font-display uppercase tracking-wider">Username</label>
          <input
            bind:value={username}
            type="text"
            class="w-full px-3 py-2 text-sm rounded"
            placeholder="admin"
            on:keydown={e => e.key === 'Enter' && login()}
          />
        </div>
        <div>
          <label class="block text-xs text-muted mb-1 font-display uppercase tracking-wider">Password</label>
          <input
            bind:value={password}
            type="password"
            class="w-full px-3 py-2 text-sm rounded"
            placeholder="••••••••"
            on:keydown={e => e.key === 'Enter' && login()}
          />
        </div>
        <button
          on:click={login}
          disabled={loading}
          class="w-full py-2.5 rounded bg-accent text-bg font-display font-semibold text-sm
                 hover:bg-accent/90 transition-colors disabled:opacity-50 mt-2"
        >
          {loading ? 'Connecting…' : 'Sign In'}
        </button>
      </div>

      <p class="mt-5 text-xs text-muted text-center">
<!--        Default: admin / admin123 &nbsp;·&nbsp; viewer / viewer123-->
      </p>
    </div>
  </div>
</div>
