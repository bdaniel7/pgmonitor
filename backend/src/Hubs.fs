module PgMonitor.Hubs

open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.Authorization
open System.Threading.Tasks

[<Authorize>]
type MonitorHub() =
    inherit Hub()

    override this.OnConnectedAsync() : Task =
        base.OnConnectedAsync()

    override this.OnDisconnectedAsync(exn) : Task =
        base.OnDisconnectedAsync(exn)
