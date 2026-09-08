# Un único Dockerfile para los cinco ejecutables .NET del monorepo. Todos comparten los mismos
# BuildingBlocks, así que cinco Dockerfiles idénticos salvo una ruta serían duplicación que se
# desincroniza. El servicio concreto se elige con PROJECT_PATH desde docker-compose.
#
# El contexto de build es la raíz del repo (no backend/) porque el .editorconfig vive allí y
# EnforceCodeStyleInBuild lo necesita: sin él, el build dentro del contenedor aplicaría reglas
# distintas a las del build local, y un fallo de estilo se descubriría en CI y no en la máquina.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT_PATH
WORKDIR /src

COPY .editorconfig ./
COPY backend/ ./backend/

RUN dotnet restore "backend/${PROJECT_PATH}"
RUN dotnet publish "backend/${PROJECT_PATH}" \
        --configuration Release \
        --no-restore \
        --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG ASSEMBLY_NAME
WORKDIR /app

# curl no viene en la imagen runtime y docker compose necesita un healthcheck real; sin él,
# depends_on solo esperaría a que el proceso arranque, no a que el servicio esté listo.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Usuario sin privilegios: la imagen de .NET define 'app' (uid 1654) precisamente para esto.
USER app

COPY --from=build --chown=app:app /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_ENVIRONMENT=Production \
    APP_DLL=${ASSEMBLY_NAME}.dll

EXPOSE 8080

# Shell form con exec: hace falta expandir APP_DLL, y exec mantiene el proceso como PID 1
# para que reciba SIGTERM y el apagado sea ordenado.
ENTRYPOINT ["sh", "-c", "exec dotnet /app/$APP_DLL"]
