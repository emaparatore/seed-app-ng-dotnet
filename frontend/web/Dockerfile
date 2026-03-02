FROM node:22 AS build
WORKDIR /app
ARG ANGULAR_BUILD_CONFIGURATION=production

# Copia i manifest per sfruttare la cache dei layer
COPY package*.json ./
RUN npm ci

COPY . .
# Le librerie devono essere compilate prima dell'app (tsconfig risolve da dist/)
RUN NODE_OPTIONS=--max-old-space-size=4096 npx ng build shared-auth && \
    NODE_OPTIONS=--max-old-space-size=4096 npx ng build shared-core && \
    NODE_OPTIONS=--max-old-space-size=4096 npx ng build shared-ui && \
    NODE_OPTIONS=--max-old-space-size=4096 npm run build -- --configuration=${ANGULAR_BUILD_CONFIGURATION}

FROM node:22-slim AS runtime
WORKDIR /app
COPY --from=build /app/dist ./dist

ENV PORT=80
EXPOSE 80
CMD ["node", "dist/app/server/server.mjs"]
