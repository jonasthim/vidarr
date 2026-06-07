# syntax=docker/dockerfile:1.7
# dotnet/sdk does not ship Node — use the official Node image for the SPA build
# and copy the dist/ output into the dotnet stage.
FROM node:20-alpine AS web-build
WORKDIR /web
COPY src/Vidarr.Web/package.json src/Vidarr.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund --silent
COPY src/Vidarr.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Vidarr.slnx ./
COPY .editorconfig ./
COPY src/ src/
COPY tests/ tests/
# Skip the npm step inside dotnet publish; we copy the prebuilt web output below.
ARG BUILD_CONFIGURATION=Release
RUN dotnet restore src/Vidarr.Host/Vidarr.Host.csproj
COPY --from=web-build /web/dist src/Vidarr.Host/wwwroot/
RUN dotnet publish src/Vidarr.Host/Vidarr.Host.csproj \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        -p:UseAppHost=false \
        -p:SkipWebBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl ca-certificates python3 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
    && chmod +x /usr/local/bin/yt-dlp \
    && mkdir -p /config /downloads /library

ENV ASPNETCORE_URLS=http://+:8989 \
    VIDARR_SQLITE_PATH=/config/vidarr.db \
    VIDARR_BACKUP_FOLDER=/config/backups \
    VIDARR_INCOMPLETE=/downloads/incomplete \
    VIDARR_YTDLP_PATH=/usr/local/bin/yt-dlp

EXPOSE 8989
VOLUME ["/config", "/downloads", "/library"]
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Vidarr.Host.dll"]
