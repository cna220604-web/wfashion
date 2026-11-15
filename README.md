# WebApplication1 Studio

Fashion storefront and role-based commerce application using ASP.NET Core MVC, ASP.NET Core Identity, EF Core 10, and SQLite.

## Run locally

```powershell
dotnet restore
dotnet run
```

The application uses one SQLite database for the storefront, customer accounts, admin catalog, staff inventory, orders, reviews, and support chat. Locally it uses `App_Data/wstudio.db`; on Render the Blueprint configures `/var/data/app.db`. On first startup, EF Core creates missing tables and the app seeds any missing sample catalog products without replacing existing products, inventory, orders, or accounts in that SQLite file.

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
- **Admin:** view store metrics, create employee accounts with the Employee role, create/edit/hide products, manage user roles, review orders, and upload, hide, or delete MP4/WebM fashion videos linked to active products. Customers can swipe through the video feed and open the linked product to select a size and buy.

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

1. Push this repository to GitHub. In Render, choose **New → Blueprint**, connect the repository, and let Render read `render.yaml`. The Blueprint selects the Free web-service plan.
2. Render builds the app from `Dockerfile` and uses SQLite plus uploaded media on the service's ephemeral filesystem. This is suitable for a class-project demo, not production: the free service can sleep after inactivity, and the database, accounts, orders, uploaded images, and fashion videos can be lost whenever it sleeps, restarts, or redeploys. The first request after idle may take about a minute. Sample catalog products are recreated on startup.
3. In the new service's **Environment** settings, add `AdminBootstrap__Email` and `AdminBootstrap__Password` if you need an admin account, and `Ai__ApiKey` if you want the AI stylist to work. Use secrets in Render's dashboard; never commit them to GitHub. Keep the bootstrap values configured if the demo database may be reset and you need the admin account recreated. SMTP variables are optional and only needed for email features.
4. Deploy and wait for the service to become healthy. Render provides an HTTPS URL that can be shared for viewing the demo. Avoid entering important personal data because the free service does not provide persistent local storage.

## Operational notes

- Product names, descriptions, prices, and inventory are sample catalog data. Replace them with verified store data before launch.
- Sample product photos may use Unsplash and require an internet connection; uploaded images and fashion videos are stored under `Storage__PersistentPath` when configured, or under `wwwroot` locally. Fashion video uploads accept MP4/WebM up to 100 MB. On Render Free, uploaded files are ephemeral and can be lost when the service restarts, sleeps, or redeploys.
- COD orders and inventory are persisted in the shared SQLite database. Order confirmation/status emails are sent only when SMTP settings and staff recipients are configured. Online payments, carrier tracking integration, email verification, password reset delivery, and production monitoring still need setup.
- `EnsureCreated` initializes an empty SQLite database. Compatibility code adds app-specific columns and support tables idempotently at startup without dropping existing data. Employee online presence is process-local and expires after 75 seconds without a heartbeat; run a single app instance for live handoff. On Render Free, local files are ephemeral; sample catalog products are re-seeded if the database is reset.
- Configure HTTPS, secure secrets, scheduled backups (including uploaded product images), SMTP, and trusted proxy settings before public deployment. See [OPERATIONS.md](OPERATIONS.md) for setup and restore guidance.
