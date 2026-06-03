# syntax=docker/dockerfile:1.7
#
# Multi-stage Dockerfile cho Rpom.Api.
# Build context: repo root (Rpom-backend/).
#
#   docker build -f src/Rpom.Api/Dockerfile -t rpom-api .
#   docker compose up --build api
#
# Hint: copy .csproj + restore TRƯỚC khi copy source code →
#   layer NuGet restore được cache khi chỉ code thay đổi.

# -------- 1. Restore (cache NuGet) -----------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

# Solution-level files trước (apply cho mọi project)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY Rpom.slnx ./

# Csproj từng project (giữ folder structure để ProjectReference work)
COPY src/Rpom.Domain/Rpom.Domain.csproj                       src/Rpom.Domain/
COPY src/Rpom.Application/Rpom.Application.csproj             src/Rpom.Application/
COPY src/Rpom.Infrastructure/Rpom.Infrastructure.csproj       src/Rpom.Infrastructure/
COPY src/Rpom.Api/Rpom.Api.csproj                             src/Rpom.Api/
COPY src/Rpom.Worker/Rpom.Worker.csproj                       src/Rpom.Worker/
COPY tests/Rpom.Domain.Tests/Rpom.Domain.Tests.csproj         tests/Rpom.Domain.Tests/
COPY tests/Rpom.Application.Tests/Rpom.Application.Tests.csproj tests/Rpom.Application.Tests/

RUN dotnet restore src/Rpom.Api/Rpom.Api.csproj

# -------- 2. Build + publish -----------------------------------------------
FROM restore AS build
COPY . .
RUN dotnet publish src/Rpom.Api/Rpom.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish

# -------- 3. Runtime --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# `aspnet:10.0` already ships a non-root user `app` (UID 1654 in .NET 10).
# Use it instead of creating our own.
USER app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

ENTRYPOINT ["dotnet", "Rpom.Api.dll"]
