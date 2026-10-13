# SA Beef Resolver

This repository contains independent services for the Beef Resolver system.

| Service | Technology | Role | Local port |
| --- | --- | --- | --- |
| `EvidenceAndArgumentSubmission` | .NET gRPC, PostgreSQL, RabbitMQ | Authenticated argument/evidence submission and AI-queue publishing | 50051 |
| `AIProblemResolution` | Python, RabbitMQ, MongoDB, Ollama | Consumes arguments and produces AI-assisted resolution results | — |
| `BeefResolverCrud` | Node.js, SQLite | REST CRUD API for beef relationships | 3000 |

## Run the composed system

```bash
docker compose up --build
```

The evidence gRPC service is available at `127.0.0.1:50051`; the Beef CRUD API is available at `http://127.0.0.1:3000/api/beefs`. Compose persists their PostgreSQL, MongoDB, RabbitMQ, Ollama, and SQLite data in named volumes.

## Authentication

`EvidenceAndArgumentSubmission` exposes `Register` and `Login` gRPC methods. They issue JWT access tokens; every `ArgumentService` method requires `authorization: Bearer <token>` metadata. The author is derived from that token, never from caller-supplied IDs.

See each service's README for its API and local development workflow.
