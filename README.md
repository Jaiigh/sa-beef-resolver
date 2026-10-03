# SA Beef Resolver

Each service is separate, with its own runtime, dependencies and data store. They share nothing in the repo.

| Service | Folder | Style | Stack |
| --- | --- | --- | --- |
| Beef CRUD | [BeefResolverApi](BeefResolverApi) | REST (port 3000) | Node.js + PostgreSQL (Docker) |
| Evidence and Argument Submission | [EvidenceAndArgumentSubmission](EvidenceAndArgumentSubmission) | gRPC (port 50051) | ASP.NET Core (C#) |

See the README inside each folder for how to run it.
