FROM node:20-alpine AS build
WORKDIR /app

COPY src/Advertified.Web/package*.json ./
RUN npm install

COPY src/Advertified.Web/ ./

ARG VITE_API_BASE_URL=https://dev.advertified.com/api
ARG VITE_MAPBOX_ACCESS_TOKEN=

ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
ENV VITE_MAPBOX_ACCESS_TOKEN=${VITE_MAPBOX_ACCESS_TOKEN}

RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY .deploy-v4-dev/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
