# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# (optional) quieter, deterministic builds
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Copy solution + project files first (cache-friendly)
COPY Memes.sln ./
COPY src/Memes.Api/Memes.Api.csproj src/Memes.Api/
COPY src/Memes.Application/Memes.Application.csproj src/Memes.Application/
COPY src/Memes.Domain/Memes.Domain.csproj src/Memes.Domain/
COPY src/Memes.Infrastructure/Memes.Infrastructure.csproj src/Memes.Infrastructure/

# If you have a NuGet.config or global.json at solution root, copy them too:
# COPY NuGet.config ./
# COPY global.json ./

# ✅ Restore the whole solution so all project refs (and analyzers) are available
RUN dotnet restore Memes.sln --disable-parallel

# Now copy the rest of the source
COPY src ./src

# Publish the API (no restore again)
RUN dotnet publish src/Memes.Api/Memes.Api.csproj -c Release -o /out --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Memes.Api.dll"]