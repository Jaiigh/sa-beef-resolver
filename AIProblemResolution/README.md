# AI Problem Resolution

RabbitMQ work queues + session-isolated MongoDB history and per-resolution archive + a local Ollama model.

Runtime: ASP.NET Core `SubmitArgumentForAi` -> RabbitMQ -> Python worker -> Ollama.
From repository root, run `docker compose up --build` to start all services.
Then `docker compose run --rm --build --no-deps demo-client` submits through ASP.NET Core.
`tools/test_producer.py` is optional development tooling, not a runtime service.

See [report.md](../report.md) for setup, examples, message contracts, design decisions,
test results, and limitations. Docker mode requires Docker only; native development
requires Python 3.9+ and Ollama.
