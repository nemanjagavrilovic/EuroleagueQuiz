# EuroleagueQuiz deployment guide

This guide deploys the application for testing with:

- Angular frontend on Azure Static Web Apps Free
- ASP.NET Core .NET 8 API on Azure App Service
- PostgreSQL database (for example, Azure Database for PostgreSQL Flexible Server)

The free Azure-generated URLs are enough for testing. A custom domain requires a domain that you own.

## 1. Prerequisites

Install or create:

- An Azure account and subscription
- A GitHub account
- .NET 8 SDK
- Node.js and npm
- `dotnet-ef`

From PowerShell:

```powershell
dotnet tool install --global dotnet-ef
```

Push the project to a GitHub repository. Do not commit production passwords or connection strings.

## 2. Create the PostgreSQL database

1. Open your cloud provider and create a PostgreSQL server and database.
2. Create the `EuroleagueQuiz` database.
3. Create a database user with permission to create tables and indexes.
4. Configure the firewall/network rules to allow the Azure App Service to connect.
5. Copy the PostgreSQL connection string and replace the password placeholder.

The PostgreSQL connection string should resemble:

```text
Host=your-postgres-host;Port=5432;Database=EuroleagueQuiz;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true;
```

## 3. Apply EF Core migrations

From the solution directory, temporarily set the Azure connection string in the current PowerShell session:

```powershell
$env:ConnectionStrings__Postgres = "Host=your-postgres-host;Port=5432;Database=EuroleagueQuiz;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true;"

dotnet ef database update `
  --project EuroleagueQuiz.Infrastructure `
  --startup-project EuroleagueQuiz.Api
```

The double underscore maps to the ASP.NET Core configuration key `ConnectionStrings:Postgres`.

Verify the tables in your PostgreSQL client. The PostgreSQL migration will be created under:

```text
EuroleagueQuiz.Infrastructure/Persistence/Migrations
```

## 4. Deploy the API

1. In Azure Portal, create **App Service** → **Web App**.
2. Choose:
   - Publish: Code
   - Runtime stack: .NET 8
   - Operating system: Linux or Windows
   - Pricing plan: Free for testing, if available in the selected region
3. Give the app a unique name. Its testing URL will be similar to:

```text
https://your-api-name.azurewebsites.net
```

4. Configure the API app settings under **Settings → Environment variables**:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Postgres=<PostgreSQL connection string>
Cors__AllowedOrigins__0=https://your-site-name.azurestaticapps.net
```

5. Deploy the `EuroleagueQuiz.Api` project from GitHub or from a published package.

For a local publish package:

```powershell
dotnet publish EuroleagueQuiz.Api\EuroleagueQuiz.Api.csproj `
  --configuration Release `
  --output .\publish\api
```

Deploy the contents of `publish\api` using the App Service Deployment Center or Zip Deploy.

Test the API:

```text
https://your-api-name.azurewebsites.net/swagger
```

Swagger is currently enabled only in Development. For a temporary test deployment, either set `ASPNETCORE_ENVIRONMENT=Development` or test the controller endpoints directly. For a public deployment, keep Production and do not expose Swagger unless intentionally enabled.

## 5. Import EuroLeague data

After the API and database are available, import the seasons through the API:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri "https://your-api-name.azurewebsites.net/api/import/seasons"
```

For one season:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri "https://your-api-name.azurewebsites.net/api/import/seasons/E2013"
```

The all-seasons import may take time. Do not start multiple imports at the same time. The importer is designed to avoid duplicate player/team/season relationships.

For a public test deployment, protect or temporarily disable the import endpoints after importing so that anyone with the URL cannot trigger a large import.

## 6. Configure the Angular API URL

Edit:

```text
client/src/environments/environment.ts
```

Set the deployed API URL:

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://your-api-name.azurewebsites.net/api'
};
```

Build locally to verify the frontend:

```powershell
cd client
npm install
npm run build
```

The Angular output directory is normally:

```text
client/dist/euroleague-quiz-client/browser
```

## 7. Deploy Angular to Azure Static Web Apps

1. In Azure Portal, create **Static Web App**.
2. Select the **Free** plan.
3. Connect the GitHub repository.
4. Configure the build:

```text
App location: client
Api location: leave empty
Output location: dist/euroleague-quiz-client/browser
```

5. Choose the repository branch used for deployment.
6. Azure creates a GitHub Actions workflow and gives the site a URL similar to:

```text
https://your-site-name.azurestaticapps.net
```

Open that URL and test:

- Home page
- Player Grid
- Team History
- Missing Club
- Career Order
- Mystery Player
- Player autocomplete
- Grid reveal

## 8. CORS

Development allows local Angular origins. Production reads the allowed origin from the App Service setting `Cors__AllowedOrigins__0` and does not allow arbitrary websites.

Set it to the exact Static Web Apps URL:

```text
Cors__AllowedOrigins__0=https://your-site-name.azurestaticapps.net
```

If you later add a custom frontend domain, add another indexed setting such as `Cors__AllowedOrigins__1`.

## 9. Important testing limitations

- Free services can have quotas and may be slower than paid services.
- The deployed API must use the hosted PostgreSQL connection string, not the local development connection string.
- The game round caches are in memory. Restarting the API clears active rounds and rebuilds the career catalog.
- The current import endpoints are not authenticated.
- Do not place SQL credentials inside Angular files; Angular code is public.
- Add authentication and protect imports before allowing public users to use the application.

## 10. Useful deployment URLs

```text
Frontend: https://your-site-name.azurestaticapps.net
API:      https://your-api-name.azurewebsites.net
Swagger:  https://your-api-name.azurewebsites.net/swagger
```

## 11. Deploy to a DigitalOcean Linux Droplet with Docker

The repository includes a multi-stage `Dockerfile`, `docker-compose.yml`, and an Nginx configuration. Nginx serves the Angular application and proxies `/api` to the ASP.NET Core container. PostgreSQL runs externally on Supabase.

### Prepare the droplet

Create an Ubuntu Linux Droplet, connect through SSH, and install Docker:

```bash
sudo apt update
sudo apt install -y ca-certificates curl git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
exit
```

Reconnect through SSH so the Docker group membership is applied.

### Download and configure the application

```bash
git clone https://github.com/YOUR_USERNAME/YOUR_REPOSITORY.git EuroleagueQuiz
cd EuroleagueQuiz
```

Create a `.env` file on the droplet. Do not commit this file:

```bash
POSTGRES_CONNECTION_STRING='Host=YOUR_SUPABASE_HOST;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true'
PUBLIC_ORIGIN=https://pickandroll.io
```

Use the connection string supplied by Supabase. If the direct Supabase connection is not reachable from the Droplet, use Supabase's transaction/session pooler connection string instead.

### Build and start the containers

```bash
docker compose up -d --build
docker compose ps
docker compose logs -f --tail=100
```

The application container listens privately on `127.0.0.1:8080`. Public traffic should be handled by the host Nginx configuration described below; the API remains reachable only through the web container's `/api` proxy.

### Run database migrations

The API image contains the application but does not automatically run migrations. Run migrations from a temporary SDK container using the same Supabase connection string, or apply the existing PostgreSQL migration through your normal migration workflow before starting the application. Do not expose the migration endpoint publicly.

### Configure DNS and HTTPS with host Nginx

For the root domain, create an `A` record at your DNS provider:

```text
Name: @
Value: YOUR_DROPLET_PUBLIC_IPV4
```

If your Droplet has a public IPv6 address, also create an `AAAA` record for `@`. For `www`, either create a `CNAME` with value `pickandroll.io` or an additional `A` record pointing to the same IPv4 address. A CNAME is not used for the root domain in this setup.

Install Nginx and Certbot on the Droplet:

```bash
sudo apt update
sudo apt install -y nginx certbot python3-certbot-nginx
sudo mkdir -p /var/www/certbot
sudo cp docker/pickandroll.io.nginx.conf /etc/nginx/sites-available/pickandroll.io
sudo ln -s /etc/nginx/sites-available/pickandroll.io /etc/nginx/sites-enabled/pickandroll.io
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
```

Open HTTP and HTTPS in the firewall:

Allow SSH and HTTP initially:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Issue the certificate for both names:

```bash
sudo certbot certonly --webroot -w /var/www/certbot \
  -d pickandroll.io -d www.pickandroll.io
```

Then uncomment the HTTPS server block in `/etc/nginx/sites-available/pickandroll.io`, test, and reload Nginx:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

Do not expose port 8080 publicly. Certbot renewals can be tested with `sudo certbot renew --dry-run`.

### Update the application

```bash
git pull
docker compose up -d --build
docker image prune -f
```

The production Angular build uses `/api` as its API URL, so no Azure API URL needs to be changed in the client.

## 12. Applying EF Core migrations to Supabase

The Docker runtime does not automatically change the database schema. Apply migrations explicitly when the model changes.

### Create a migration locally

After changing an entity or the EF Core configuration, create a migration from the repository root:

```powershell
dotnet ef migrations add DescribeTheChange `
  --project .\EuroleagueQuiz.Infrastructure `
  --startup-project .\EuroleagueQuiz.Api `
  --output-dir Persistence/Migrations
```

Commit the generated files in `EuroleagueQuiz.Infrastructure/Persistence/Migrations`.

### Apply the migration to Supabase

Use a Supabase direct connection or Session Pooler connection. Avoid the Transaction Pooler for EF migration commands.

On the development computer, set the connection string temporarily:

```powershell
$env:ConnectionStrings__Postgres = "Host=YOUR_SUPABASE_HOST;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
dotnet ef database update `
  --project .\EuroleagueQuiz.Infrastructure `
  --startup-project .\EuroleagueQuiz.Api
Remove-Item Env:ConnectionStrings__Postgres
```

The command updates the Supabase database using the migrations already committed to the project. Take a Supabase backup before applying a migration that changes or removes data.

### Recommended release order

For normal additive changes:

1. Create and commit the migration.
2. Push the code to GitHub.
3. Apply `dotnet ef database update` to Supabase.
4. SSH into the Droplet and run `git pull`.
5. Run `docker compose up -d --build`.

For a breaking schema change, first deploy code that supports both the old and new schema, migrate the database, and only then remove the old schema usage.
