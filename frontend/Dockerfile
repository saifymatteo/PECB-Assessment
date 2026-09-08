# Build the Angular app, then serve the static bundle with nginx.
# nginx also proxies /api to the API container, so the browser talks to one origin.
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

FROM nginx:alpine
COPY --from=build /app/dist/support-desk/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
