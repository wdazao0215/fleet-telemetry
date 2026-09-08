# Imagen del dashboard. Multi-stage para que el contenedor final no lleve ni el código fuente ni las
# dependencias de desarrollo.

FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
# npm ci y no npm install: instala exactamente lo que fija el lockfile, así el build es
# reproducible y no se cuela una versión menor distinta entre la máquina local y el CI.
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Next incrusta las NEXT_PUBLIC_* en el bundle durante el build, así que tienen que estar
# disponibles aquí y no en tiempo de arranque.
ARG NEXT_PUBLIC_QUERY_API_URL
ARG NEXT_PUBLIC_INGESTION_API_URL
ARG NEXT_PUBLIC_INGESTION_API_KEY
ENV NEXT_PUBLIC_QUERY_API_URL=$NEXT_PUBLIC_QUERY_API_URL \
    NEXT_PUBLIC_INGESTION_API_URL=$NEXT_PUBLIC_INGESTION_API_URL \
    NEXT_PUBLIC_INGESTION_API_KEY=$NEXT_PUBLIC_INGESTION_API_KEY \
    NEXT_TELEMETRY_DISABLED=1

RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
# HOSTNAME=0.0.0.0 no es opcional. El servidor standalone de Next hace bind a process.env.HOSTNAME,
# y Docker define esa variable con el id del contenedor, que resuelve a su IP privada. El resultado
# es un servidor que escucha SOLO en 172.x.y.z: el mapeo de puertos sigue funcionando desde fuera,
# pero cualquier conexión desde dentro del contenedor -- incluido el healthcheck de compose -- falla
# con "connection refused", y el contenedor queda marcado unhealthy sin que la aplicación tenga nada
# malo.
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

RUN apk add --no-cache curl

# La salida standalone incluye solo los node_modules que el servidor usa de verdad: reduce la
# imagen de cientos de megas a decenas.
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

USER node
EXPOSE 3000

ENTRYPOINT ["node", "server.js"]
