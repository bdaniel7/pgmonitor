<script>
  import { onMount } from 'svelte'
  import Router, { replace } from 'svelte-spa-router'
  import { isLoggedIn } from './stores/auth.js'
  import { startHub } from './stores/hub.js'

  import Login       from './components/Login.svelte'
  import Sidebar     from './components/Sidebar.svelte'
  import Overview    from './components/Overview.svelte'
  import LiveQueries from './components/LiveQueries.svelte'
  import SlowQueries from './components/SlowQueries.svelte'
  import SqlEditor   from './components/SqlEditor.svelte'
  import SqlRunner         from './components/SqlRunner.svelte'
  import Replication from './components/Replication.svelte'
  import Locks       from './components/Locks.svelte'
  import Vacuum      from './components/Vacuum.svelte'
  import Alerts      from './components/Alerts.svelte'
  import UserManagement    from './components/UserManagement.svelte'
  import ConnectionManager from './components/ConnectionManager.svelte'

  const routes = {
    '/':              Overview,      // redirect handled by onMount below
    '/overview':      Overview,
    '/live-queries':  LiveQueries,
    '/slow-queries':  SlowQueries,
    '/sql-editor':    SqlEditor,
    '/sql-runner':    SqlRunner,
    '/replication':   Replication,
    '/locks':         Locks,
    '/vacuum':        Vacuum,
    '/alerts':        Alerts,
    '/users':         UserManagement,
    '/connections':   ConnectionManager,
  }

  onMount(async () => {
    if ($isLoggedIn) {
      await startHub()
      // Redirect bare / to /overview
      if (window.location.hash === '' || window.location.hash === '#/') {
        replace('/overview')
      }
    }
  })
</script>

{#if !$isLoggedIn}
  <Login />
{:else}
  <div class="flex h-screen overflow-hidden bg-bg">
    <Sidebar />
    <main class="flex-1 overflow-hidden">
      <Router {routes} />
    </main>
  </div>
{/if}
