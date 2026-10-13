FROM node:22-bookworm-slim

WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends sqlite3 \
    && rm -rf /var/lib/apt/lists/*
COPY . ./
RUN chown -R node:node /app
USER node
EXPOSE 3000
CMD ["node", "server.js"]
