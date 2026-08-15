# build frontend
FROM node:20-alpine AS frontend-builder
WORKDIR /app
COPY ./stankin-schedule/package*.json ./
RUN npm install
RUN npm ci --silent
COPY ./stankin-schedule/ .
RUN npm run build

# caddy serving static + reverse proxy, TLS certs automated
FROM caddy:2-alpine
COPY --from=frontend-builder /app/dist /srv
COPY ./deploy/caddy/Caddyfile /etc/caddy/Caddyfile

EXPOSE 80 443
