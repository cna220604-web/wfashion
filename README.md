# WebApplication1 Studio

Fashion storefront and role-based commerce application using ASP.NET Core MVC, ASP.NET Core Identity, and EF Core 10. Local development uses SQLite; hosted deployments can use PostgreSQL.

## Run locally

```powershell
dotnet restore
dotnet run
```

The application uses one database for the storefront, customer accounts, admin catalog, staff inventory, orders, reviews, and support chat. Locally it uses SQLite at `App_Data/wstudio.db`; Render is configured for PostgreSQL so database records are independent of deploy instances. On first startup, EF Core creates the schema and the app seeds missing sample catalog products without replacing existing products, inventory, orders, or accounts.

## Create the first administrator

Public registration always creates a Customer account. Configure the first administrator through environment variables; never commit the password or put it in `appsettings.json`.

```powershell
$env:AdminBootstrap__Email = "admin@example.com"
$env:AdminBootstrap__Password = "Use-A-Unique-Strong-Password-1!"
dotnet run
```

The password must contain at least 8 characters, including uppercase, lowercase, a digit, and a special character. After creating the administrator, unset the bootstrap password. Existing accounts can be promoted or assigned the Employee/Customer role from Admin → Tài khoản & vai trò. Administrators cannot change their own role or demote the last administrator.

## Install as an app (PWA)

W Studio can be installed from a supported browser on a phone or computer. Serve the site over HTTPS (localhost is also supported for development). In Chrome/Edge, open the storefront and choose **CÀI ỨNG DỤNG** when the install button appears, or use the browser menu's **Install app** option. On iPhone/iPad, open the HTTPS site in Safari, tap Share, then **Add to Home Screen**.

The app shell and static storefront assets can be reused offline. Account pages, customer data, chat, and orders are never cached; sign-in, checkout, and other server features still require an internet connection. If offline, the app shows a reconnect page instead of stale account or order information.

## Role capabilities

- **Customer:** register/login/logout, edit profile, browse/search/filter/sort active products with pagination, select sizes, save favorites and cart in the browser, place COD orders, follow order progress, review products from delivered orders, receive configured order emails, cancel pending orders, use the AI stylist, and hand the conversation to employee support.
- **Employee:** dedicated operations dashboard with order counts and low-stock alerts; go online to receive customer chats, claim waiting conversations, reply and close assigned chats; filter orders by status, review customer and delivery notes, progress orders through valid fulfillment states, notify customers by configured email, cancel eligible orders with stock restoration, and update product inventory.
- **Admin:** view store metrics, create employee accounts with the Employee role, create/edit/hide products, manage user roles, review orders, and upload, hide, or delete MP4/WebM fashion videos linked to active products. The separate Video fashion page preserves portrait and landscape framing, lets customers enable sound, and links each clip to its buyable product.

Passwords use ASP.NET Core Identity hashing. Login failures are locked out after five attempts for 15 minutes. Mutating forms and JSON endpoints require antiforgery tokens. Product prices and stock are reloaded and validated server-side when an order is placed; orders retain product/price snapshots.

## Configure the AI stylist and fallback providers

The chat service tries providers in ascending index order and switches to the next provider when a request fails, times out, is rate-limited, or returns an unreadable response. It supports Gemini and OpenAI-compatible chat-completions APIs (for example Groq and OpenRouter). Configure keys only in User Secrets or the hosting platform's secret environment settings, never in browser JavaScript, `appsettings.json`, or source control.

For local development, create a Gemini primary provider and Groq fallback (replace the placeholders with your own values):

```powershell
dotnet user-secrets set "Ai:Providers:0:Name" "Gemini main" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:0:Type" "Gemini" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:0:Model" "gemini-3-flash-preview" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:0:ApiKey" "YOUR_GEMINI_API_KEY" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:1:Name" "Groq backup" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:1:Type" "Groq" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:1:Model" "YOUR_GROQ_MODEL_ID" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:1:Endpoint" "https://api.groq.com/openai/v1/chat/completions" --project .\WebApplication1.csproj
dotnet user-secrets set "Ai:Providers:1:ApiKey" "YOUR_GROQ_API_KEY" --project .\WebApplication1.csproj
```

For Render, use environment variables such as `Ai__Providers__0__Type`, `Ai__Providers__0__Model`, `Ai__Providers__0__Endpoint`, and `Ai__Providers__0__ApiKey`. OpenAI-compatible providers require the full HTTPS chat-completions URL and the exact model ID supported by that provider; use `Type=Groq` for Groq so the service omits JSON mode and disables reasoning output, leaving tokens for the final answer. `Name` is optional; the default is the numeric provider index. Legacy `Ai__ApiKey` and `Ai__Model` settings remain supported as a Gemini provider after the indexed providers. Chat remains limited to 10 requests/minute per client IP and requires a signed-in account. Restart the app after changing secrets or environment variables.

## Deploy a free demo to Render

1. Create a PostgreSQL database with a provider that offers a free plan, then copy its private/internal connection string. Free plans have provider-specific limits and retention rules; review those before storing important customer data.
2. Push this repository to GitHub. In Render, choose **New → Blueprint**, connect the repository, and let Render read `render.yaml`. The Blueprint selects the Free web-service plan.
3. In the Render web service's **Environment** settings, set `Database__Provider` to `PostgreSQL` and `ConnectionStrings__DefaultConnection` to the complete PostgreSQL connection string from your database provider (Neon `postgres://` URLs are accepted). Keep it private; do not commit it or paste it into chat. Add `AdminBootstrap__Email` and `AdminBootstrap__Password` if a new database needs its initial admin, and `Ai__ApiKey` if you want the AI stylist.
4. Deploy and wait for the service to become healthy. Render provides an HTTPS URL. PostgreSQL records (accounts, orders, products) are stored outside the web service, but uploaded files remain on Render's ephemeral filesystem unless separately persisted. Sample catalog products are recreated on an empty database.

## Operational notes

- Product names, descriptions, prices, and inventory are sample catalog data. Replace them with verified store data before launch.
- Sample product photos may use Unsplash and require an internet connection; uploaded images and fashion videos are stored under `Storage__PersistentPath` when configured, or under `wwwroot` locally. Fashion video uploads accept MP4/WebM up to 100 MB. On Render Free, uploaded files are ephemeral and can be lost when the service restarts, sleeps, or redeploys.
- COD orders and inventory are persisted in the configured database. Order confirmation/status emails are sent only when SMTP settings and staff recipients are configured. Online payments, carrier tracking integration, email verification, password reset delivery, and production monitoring still need setup.
- `EnsureCreated` initializes a new database from the current EF model; it does not migrate an already-created PostgreSQL schema after future model changes. Add and apply EF Core migrations when changing the production schema. Existing SQLite databases keep their idempotent compatibility updates. Employee online presence is process-local and expires after 75 seconds without a heartbeat; run a single app instance for live handoff. On Render Free, uploaded files remain ephemeral.
- Configure HTTPS, secure secrets, scheduled backups (including uploaded product images), SMTP, and trusted proxy settings before public deployment. See [OPERATIONS.md](OPERATIONS.md) for setup and restore guidance.
