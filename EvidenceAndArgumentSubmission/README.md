# Evidence and Argument Submission - gRPC only

This implements the functional requirement **“Users can submit arguments and evidence for AI analysis.”** Every argument belongs to one `dispute_id` and one author `person_id`. The gRPC backend is ASP.NET Core (.NET/C#), consistent with ADR-2.

It is a pure gRPC module: there is no REST/Express layer. The service uses unary RPCs because each CRUD request has one request and one response.

## RPC methods

| RPC | Function |
| --- | --- |
| `CreateArgument` | Create an argument, its owner, dispute, statement, and up to 10 evidence references |
| `GetArgument` | Retrieve a single argument by `argument_id` |
| `GetAllArguments` | List arguments for a required `dispute_id`; optionally filter by `person_id` and paginate |
| `UpdateArgument` | Update the statement, evidence and status of the author's argument |
| `DeleteArgument` | Delete the author's argument |

An evidence item stores only metadata and an HTTP(S) `evidence_url`; upload the actual file separately to object storage. This prevents large file uploads from exceeding gRPC message limits.

## Run and test

```bash
cd EvidenceAndArgumentSubmission
dotnet run --project server/HumanResolve.ArgumentService
# In another terminal, while the server is still running:
npm install
npm run start:client
```

The server uses `127.0.0.1:50051` by default. Set `GRPC_PORT` to override its port; the Node demo client additionally accepts `GRPC_HOST`.

## Important design decisions

- Uses `proto3`, a versioned protobuf package (`humanresolve.argument.v1`), explicit request/response messages, and stable field numbers.
- Validates required IDs, statement length, evidence URL/type, page sizes, and enum values. Failures return `INVALID_ARGUMENT`.
- Returns `NOT_FOUND` for a missing argument and `PERMISSION_DENIED` when a different `person_id` tries to edit or delete it.
- Uses `expected_version` for optimistic concurrency. An old version returns `ABORTED`, so a caller cannot silently overwrite a newer edit.
- The included C# `InMemoryArgumentRepository` is a repository boundary, so PostgreSQL/EF Core can replace it later without changing the `.proto` contract.
- The Node.js sample client sets a five-second deadline and proves that gRPC is cross-language. For deployment, replace insecure localhost transport with TLS and derive `person_id` from authenticated identity rather than client input.
