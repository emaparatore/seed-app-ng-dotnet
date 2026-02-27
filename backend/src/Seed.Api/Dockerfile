FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia i file di progetto per sfruttare la cache dei layer
COPY Seed.slnx .
COPY src/Seed.Api/Seed.Api.csproj src/Seed.Api/
COPY src/Seed.Application/Seed.Application.csproj src/Seed.Application/
COPY src/Seed.Domain/Seed.Domain.csproj src/Seed.Domain/
COPY src/Seed.Infrastructure/Seed.Infrastructure.csproj src/Seed.Infrastructure/
COPY src/Seed.Shared/Seed.Shared.csproj src/Seed.Shared/

RUN dotnet restore src/Seed.Api/Seed.Api.csproj

COPY . .

RUN dotnet publish src/Seed.Api/Seed.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Seed.Api.dll"]
