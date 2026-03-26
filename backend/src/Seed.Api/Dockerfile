FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia i file di progetto per sfruttare la cache dei layer
COPY Seed.slnx .
COPY src/Seed.Api/Seed.Api.csproj src/Seed.Api/
COPY src/Seed.Bootstrap/Seed.Bootstrap.csproj src/Seed.Bootstrap/
COPY src/Seed.Application/Seed.Application.csproj src/Seed.Application/
COPY src/Seed.Domain/Seed.Domain.csproj src/Seed.Domain/
COPY src/Seed.Infrastructure/Seed.Infrastructure.csproj src/Seed.Infrastructure/
COPY src/Seed.Shared/Seed.Shared.csproj src/Seed.Shared/

RUN dotnet restore src/Seed.Api/Seed.Api.csproj
RUN dotnet restore src/Seed.Bootstrap/Seed.Bootstrap.csproj

COPY . .

RUN dotnet publish src/Seed.Api/Seed.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

RUN dotnet publish src/Seed.Bootstrap/Seed.Bootstrap.csproj \
    -c Release \
    -o /app/bootstrap \
    --no-restore

# Build EF Core migration bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/Seed.Infrastructure \
    --startup-project src/Seed.Api \
    --configuration Release \
    --no-build \
    -o /app/efbundle \
    --self-contained \
    -r linux-x64 \
    --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/bootstrap ./bootstrap
COPY --from=build /app/efbundle ./efbundle
EXPOSE 8080
ENTRYPOINT ["dotnet", "Seed.Api.dll"]
