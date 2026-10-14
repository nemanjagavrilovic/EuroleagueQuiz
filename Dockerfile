# Build the Angular application.
FROM node:20-bookworm-slim AS client-build
WORKDIR /src/client

COPY client/package*.json ./
RUN npm ci

COPY client/ ./
RUN npm run build -- --configuration production

# Build the ASP.NET Core API.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
WORKDIR /src

COPY EuroleagueQuiz.sln ./
COPY EuroleagueQuiz.Api/EuroleagueQuiz.Api.csproj EuroleagueQuiz.Api/
COPY EuroleagueQuiz.Application/EuroleagueQuiz.Application.csproj EuroleagueQuiz.Application/
COPY EuroleagueQuiz.Domain/EuroleagueQuiz.Domain.csproj EuroleagueQuiz.Domain/
COPY EuroleagueQuiz.Infrastructure/EuroleagueQuiz.Infrastructure.csproj EuroleagueQuiz.Infrastructure/
RUN dotnet restore EuroleagueQuiz.Api/EuroleagueQuiz.Api.csproj

COPY EuroleagueQuiz.Api/ EuroleagueQuiz.Api/
COPY EuroleagueQuiz.Application/ EuroleagueQuiz.Application/
COPY EuroleagueQuiz.Domain/ EuroleagueQuiz.Domain/
COPY EuroleagueQuiz.Infrastructure/ EuroleagueQuiz.Infrastructure/
RUN dotnet publish EuroleagueQuiz.Api/EuroleagueQuiz.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# API runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS api
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=api-build /app/publish .
ENTRYPOINT ["dotnet", "EuroleagueQuiz.Api.dll"]

# Nginx runtime image for Angular and API reverse proxying.
FROM nginx:1.27-alpine AS web
COPY --from=client-build /src/client/dist/euroleague-quiz-client/browser /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
