# build frontend
FROM node:18-alpine AS frontend-builder
WORKDIR /app
COPY ./Web/package*.json ./
RUN npm ci --silent
COPY ./Web/ .
RUN npm run build

# nginx serving static + reverse proxy
FROM nginx:stable-alpine
COPY --from=frontend-builder /app/dist /usr/share/nginx/html

# ваши конфиги nginx
COPY ./deploy/nginx/conf.d/default.conf /etc/nginx/conf.d/default.conf
COPY ./deploy/nginx/nginx.conf /etc/nginx/nginx.conf

# certs и webroot для certbot
RUN mkdir -p /var/www/certbot /etc/letsencrypt
VOLUME ["/etc/letsencrypt","/var/www/certbot"]

EXPOSE 80 443
CMD ["nginx","-g","daemon off;"]
