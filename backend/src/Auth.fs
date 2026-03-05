module PgMonitor.Auth

open System
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open Microsoft.IdentityModel.Tokens
open Npgsql.FSharp
open Models

// ── Validation ────────────────────────────────────────────────────────────────

let validateCredentials (cs: string) (username: string) (password: string) : Async<bool> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query "SELECT password_hash FROM pgm_users WHERE username = @u"
            |> Sql.parameters ["u", Sql.string username]
            |> Sql.executeAsync (fun r -> r.string "password_hash")
            |> Async.AwaitTask
        return
            match rows with
            | [hash] -> BCrypt.Net.BCrypt.Verify(password, hash)
            | _      -> false
    }

let private getRole (cs: string) (username: string) : Async<string> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query "SELECT role FROM pgm_users WHERE username = @u"
            |> Sql.parameters ["u", Sql.string username]
            |> Sql.executeAsync (fun r -> r.string "role")
            |> Async.AwaitTask
        return rows |> List.tryHead |> Option.defaultValue "viewer"
    }

// ── Token generation ──────────────────────────────────────────────────────────

let generateToken (cs: string) (username: string) (secret: string) : Async<LoginResponse> =
    async {
        let! role  = getRole cs username
        let key    = SymmetricSecurityKey(Text.Encoding.UTF8.GetBytes(secret))
        let creds  = SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        let exp    = DateTime.UtcNow.AddHours(8.0)
        let claims = [|
            Claim(ClaimTypes.Name,   username)
            Claim(ClaimTypes.Role,   role)
            Claim(JwtRegisteredClaimNames.Sub, username)
            Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        |]
        let token  =
            JwtSecurityToken(
                issuer             = "pgmonitor",
                audience           = "pgmonitor",
                claims             = claims,
                expires            = exp,
                signingCredentials = creds)
        return { Token = JwtSecurityTokenHandler().WriteToken(token); ExpiresAt = exp }
    }

// ── User management ───────────────────────────────────────────────────────────

let listUsers (cs: string) : Async<AppUser list> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query "SELECT id, username, role, created_at FROM pgm_users ORDER BY created_at"
            |> Sql.executeAsync (fun r -> {
                Id        = r.uuid "id"
                Username  = r.string "username"
                Role      = r.string "role"
                CreatedAt = r.datetimeOffset "created_at" |> fun d -> d.UtcDateTime
            })
            |> Async.AwaitTask
        return rows
    }

let createUser (cs: string) (req: CreateUserRequest) : Async<AppUser> =
    async {
        let id   = Guid.NewGuid()
        let hash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query """
                INSERT INTO pgm_users (id, username, password_hash, role)
                VALUES (@id, @u, @h, @r)
            """
            |> Sql.parameters [
                "id", Sql.uuid id
                "u",  Sql.string req.Username
                "h",  Sql.string hash
                "r",  Sql.string req.Role
            ]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        return { Id = id; Username = req.Username; Role = req.Role; CreatedAt = DateTime.UtcNow }
    }

let deleteUser (cs: string) (id: Guid) : Async<unit> =
    async {
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query "DELETE FROM pgm_users WHERE id = @id"
            |> Sql.parameters ["id", Sql.uuid id]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        ()
    }

let changePassword (cs: string) (id: Guid) (newPassword: string) : Async<unit> =
    async {
        let hash = BCrypt.Net.BCrypt.HashPassword(newPassword)
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query "UPDATE pgm_users SET password_hash = @h WHERE id = @id"
            |> Sql.parameters [
                "h",  Sql.string hash
                "id", Sql.uuid id
            ]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        ()
    }
