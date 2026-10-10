# EuroleagueQuiz deployment guide

This guide deploys the application for testing with:

- Angular frontend on Azure Static Web Apps Free
- ASP.NET Core .NET 8 API on Azure App Service
- SQL Server database on Azure SQL Database

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

## 2. Create the Azure SQL database

1. Open the Azure Portal and create **SQL databases**.
2. Create a new logical SQL server if you do not already have one.
3. Select the **Free offer** when available.
4. Use SQL authentication for the server administrator.
5. In the SQL server networking/firewall settings, enable **Allow Azure services and resources to access this server**.
6. Copy the ADO.NET connection string and replace the password placeholder.

The connection string should resemble:

```text
Server=tcp:your-server.database.windows.net,1433;Initial Catalog=EuroleagueQuiz;Persist Security Info=False;User ID=your-admin;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

## 3. Apply EF Core migrations

From the solution directory, temporarily set the Azure connection string in the current PowerShell session:

```powershell
$env:ConnectionStrings__SqlServer = "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=EuroleagueQuiz;User ID=your-admin;Password=your-password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

dotnet ef database update `
  --project EuroleagueQuiz.Infrastructure `
  --startup-project EuroleagueQuiz.Api
```

The double underscore maps to the ASP.NET Core configuration key `ConnectionStrings:SqlServer`.

Verify the tables in the Azure SQL Query Editor. The repository already contains the existing migrations under:

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
ConnectionStrings__SqlServer=<Azure SQL connection string>
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
- The deployed API must use Azure SQL, not the local Windows-authenticated SQL Server.
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
