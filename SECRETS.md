# Secrets & configuration

Tracker never reads secrets from the repo. The committed `appsettings.json` only contains
non-sensitive defaults and empty placeholders — it's safe to commit and is the schema
the rest of the layers override.

Layering, in order of increasing precedence (later wins):

1. `backend/appsettings.json` — committed defaults & placeholders.
2. `backend/appsettings.Development.json` / `appsettings.Production.json` — committed,
   environment-specific non-sensitive overrides.
3. **Local dev:** `dotnet user-secrets` (encrypted-ish JSON file outside the repo).
4. **Azure prod:** App Service > Configuration > Application Settings (env vars at runtime),
   optionally backed by Azure Key Vault references.
5. Process environment variables (always win — useful for container/CI overrides).

---

## 1. Local development — `dotnet user-secrets`

User-secrets live at `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
(the ID is wired into [`backend/Tracker.csproj`](backend/Tracker.csproj)). The file is
outside the repo and is `.gitignore`d.

### One-time setup

From the `backend/` directory:

```powershell
# Optional sanity check — the UserSecretsId is already in the csproj
dotnet user-secrets init

# JWT signing key — must be >= 32 chars. Generate one:
$key = -join ((1..64) | ForEach-Object { [char](Get-Random -Min 33 -Max 126) })
dotnet user-secrets set "Jwt:Key" "$key"

# Seed admin passwords (only used on first run to create the demo users)
dotnet user-secrets set "Seed:TenantAdmin:Password"   "<choose a strong password>"
dotnet user-secrets set "Seed:PlatformAdmin:Password" "<choose a strong password>"

# Optional — only if you're exercising the OAuth flows locally
dotnet user-secrets set "Google:ClientId"    "<your-google-client-id>.apps.googleusercontent.com"
dotnet user-secrets set "Microsoft:ClientId" "<your-app-reg-client-id>"

# Optional — only if you want real email instead of the console sink
dotnet user-secrets set "Email:Host"        "smtp.example.com"
dotnet user-secrets set "Email:Username"    "noreply@example.com"
dotnet user-secrets set "Email:Password"    "<smtp-password>"
dotnet user-secrets set "Email:FromAddress" "noreply@example.com"
```

### Useful commands

```powershell
dotnet user-secrets list           # show what's currently set
dotnet user-secrets remove "Jwt:Key"
dotnet user-secrets clear          # nuke everything for this project
```

### What happens if you skip this

- `Jwt:Key` missing → app throws on startup with a pointer to this file.
- `Seed:TenantAdmin:Password` / `Seed:PlatformAdmin:Password` missing → the corresponding
  seed user is silently skipped (no broken empty-hash user is created).
  You can either set the password and restart, or create the admin via the API later.

---

## 2. Azure production — App Service Configuration

On Azure App Service, environment variables override `appsettings.*.json` automatically.
**Nested keys use `__` (double underscore) instead of `:`.**

### Required App Settings

| Setting | Notes |
| --- | --- |
| `ConnectionStrings__Default` | Set on the **Connection Strings** tab with `Type = SQLAzure`. |
| `Jwt__Key` | 64+ random chars. Treat as the crown-jewel secret — rotate via App Settings, redeploy not required. |
| `Cors__AllowedOrigins__0` | Your frontend origin, e.g. `https://app.example.com`. Add `__1`, `__2`, … for additional origins. |
| `Seed__TenantAdmin__Password` | Only needed on first deploy. Remove after the demo tenant is bootstrapped. |
| `Seed__PlatformAdmin__Password` | Same — first-deploy only. |
| `Email__Host`, `Email__Port`, `Email__UseSsl`, `Email__Username`, `Email__Password`, `Email__FromAddress` | Only if outbound email is needed in prod. |
| `Google__ClientId`, `Microsoft__ClientId` | Only if OAuth is enabled. |

`ASPNETCORE_ENVIRONMENT` defaults to `Production` on App Service. Leave it.

### Setting them

Portal: App Service → Settings → **Environment variables** → New application setting.
Save, then **Continue** to restart.

Or via CLI:

```bash
az webapp config appsettings set \
  --resource-group <rg> \
  --name <app> \
  --settings Jwt__Key="<value>" Seed__TenantAdmin__Password="<value>"
```

---

## 3. Upgrade path — Azure Key Vault references

Once App Settings are working, move the highest-value secrets (`Jwt__Key`, DB password,
SMTP password) into Key Vault and reference them from App Settings. The app code does
not change — Azure resolves the reference and injects the plaintext at runtime.

Sketch:

1. Create a Key Vault, add the secret (e.g. `tracker-jwt-key`).
2. Enable **System-assigned managed identity** on the App Service.
3. Grant that identity `get` on Key Vault secrets (RBAC role *Key Vault Secrets User*).
4. In App Settings, set the value to:
   ```
   @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/tracker-jwt-key/)
   ```
5. The "Source" column in the portal flips to **Key Vault Reference** with a green check.

Docs: <https://learn.microsoft.com/azure/app-service/app-service-key-vault-references>

---

## 4. Frontend

The Angular SPA does **not** need `.env` files. The values in
[`frontend/src/environments/environment.ts`](frontend/src/environments/environment.ts) and
[`environment.prod.ts`](frontend/src/environments/environment.prod.ts) — API base URL,
OAuth client IDs — are public-by-design (the OAuth client IDs are sent to the browser
and visible in network requests; only the **client secret** is sensitive, and that lives
on the backend, never in the SPA).

If you need different per-environment SPA config without rebuilding, the standard
pattern is to host a `/config.json` from the backend (or Azure Static Web App config)
and fetch it on app bootstrap. Not wired up today; ask if you want this.

For personal-machine overrides during dev, copy `environment.ts` to
`environment.local.ts` — that path is already `.gitignore`d.

---

## 5. Pre-flight checklist before first prod deploy

- [ ] `Jwt__Key` set to a fresh 64+ char random string (not reused from dev).
- [ ] `ConnectionStrings__Default` points to Azure SQL with a non-trivial password.
- [ ] `Cors__AllowedOrigins__0` is the real frontend URL — not `http://localhost:4200`.
- [ ] `Seed__TenantAdmin__Password` / `Seed__PlatformAdmin__Password` are temporary;
      log in, change them via the UI, then remove the App Settings entries.
- [ ] HTTPS only is on (App Service → TLS/SSL → HTTPS Only = On).
- [ ] No `appsettings.*.local.json` or `secrets.json` made it into the deployment artifact
      (`dotnet publish` excludes them by default — verify with `Get-ChildItem` against the
      published output).
