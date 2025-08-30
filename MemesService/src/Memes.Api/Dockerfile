# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

COPY . .

RUN dotnet nuget list source || true

RUN dotnet restore Memes.sln --no-cache --disable-parallel

# Publish the API (no restore again)
RUN dotnet publish src/Memes.Api/Memes.Api.csproj -c Release -o /out --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Memes.Api.dll"]