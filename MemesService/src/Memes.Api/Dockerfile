# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# Copy only solution + project files first (best for layer caching)
COPY Memes.sln ./
COPY src/Memes.Api/Memes.Api.csproj src/Memes.Api/
COPY src/Memes.Application/Memes.Application.csproj src/Memes.Application/
COPY src/Memes.Domain/Memes.Domain.csproj src/Memes.Domain/
COPY src/Memes.Infrastructure/Memes.Infrastructure.csproj src/Memes.Infrastructure/

# Restore
RUN dotnet restore src/Memes.Api/Memes.Api.csproj

# Now bring in the rest of the source
COPY src ./src

# Publish
RUN dotnet publish src/Memes.Api/Memes.Api.csproj -c Release -o /out --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Memes.Api.dll"c]