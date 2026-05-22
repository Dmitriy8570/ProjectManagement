# syntax=docker/dockerfile:1.7
# Build context is expected to be the vue-client directory.
ARG NODE_VERSION=20-alpine
ARG NGINX_VERSION=1.27-alpine

FROM node:${NODE_VERSION} AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:${NGINX_VERSION} AS runtime

# API_UPSTREAM is rendered into nginx.conf on container start so the same
# image can point at any backend (e.g. http://api:8080).
ENV API_UPSTREAM=http://api:8080

COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
