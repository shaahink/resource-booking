# ── Stage 1: Angular build ──────────────────────────────────────────────
FROM node:24-alpine AS client-build
WORKDIR /src/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npx ng build

# ── Stage 2: API build ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY Api/Api.csproj Api/
RUN dotnet restore Api/Api.csproj
COPY Api/ Api/
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish

# ── Stage 3: runtime — one container, SPA served from wwwroot ───────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api-build /app/publish ./
COPY --from=client-build /src/client/dist/client/browser ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The aspnet image ships an unprivileged user; nothing here writes to disk (keys live in Postgres).
USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
