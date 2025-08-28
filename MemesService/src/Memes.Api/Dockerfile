# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# Copy EVERYTHING from MemesService into the image (safe & simplest)
# If you want caching later, we can optimize — for now, make it work reliably.
COPY . .

# Make sure nuget.org is present (diagnostic; safe to leave)
RUN dotnet nuget list source || true

# ✅ Restore the whole solution (no parallel to reduce flakiness in CI)
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