# syntax=docker/dockerfile:1

###############################################################################
# Imagen base del SDK (compartida por las etapas de build y development)
###############################################################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS sdk
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

###############################################################################
# Etapa de compilación: restaura dependencias y publica la Web API
###############################################################################
FROM sdk AS build
# Copia primero solo los .csproj para aprovechar el caché de capas de Docker
COPY ["HomeschoolAI.Api/HomeschoolAI.Api.csproj", "HomeschoolAI.Api/"]
COPY ["HomeschoolAI.Application/HomeschoolAI.Application.csproj", "HomeschoolAI.Application/"]
COPY ["HomeschoolAI.Adapters/HomeschoolAI.Adapters.csproj", "HomeschoolAI.Adapters/"]
COPY ["HomeschoolAI.Domain/HomeschoolAI.Domain.csproj", "HomeschoolAI.Domain/"]
RUN dotnet restore "HomeschoolAI.Api/HomeschoolAI.Api.csproj"

# Copia el resto del código fuente y publica
COPY . .
RUN dotnet publish "HomeschoolAI.Api/HomeschoolAI.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

###############################################################################
# Etapa de desarrollo: hot reload con el código fuente montado desde el host
###############################################################################
FROM sdk AS development
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_USE_POLLING_FILE_WATCHER=1
# El código fuente se monta en /src vía docker-compose
ENTRYPOINT ["dotnet", "watch", "run", "--project", "HomeschoolAI.Api/HomeschoolAI.Api.csproj", "--non-interactive"]

###############################################################################
# Etapa de ejecución: imagen mínima que ejecuta la Web API publicada
###############################################################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HomeschoolAI.Api.dll"]
