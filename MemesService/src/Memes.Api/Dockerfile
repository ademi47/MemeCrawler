# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution + csproj files (correct src/ paths)
COPY ./Memes.sln ./
COPY ./src/Memes.Domain/Memes.Domain.csproj ./src/Memes.Domain/
COPY ./src/Memes.Application/Memes.Application.csproj ./src/Memes.Application/
COPY ./src/Memes.Infrastructure/Memes.Infrastructure.csproj ./src/Memes.Infrastructure/
COPY ./src/Memes.Api/Memes.Api.csproj ./src/Memes.Api/

RUN dotnet restore ./src/Memes.Api/Memes.Api.csproj

# copy the rest
COPY . .

RUN dotnet publish ./src/Memes.Api/Memes.Api.csproj -c Release -o /app/publish

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Memes.Api.dll"]