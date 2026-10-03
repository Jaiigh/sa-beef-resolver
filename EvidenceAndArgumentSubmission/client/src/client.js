const path = require("node:path");
const grpc = require("@grpc/grpc-js");
const protoLoader = require("@grpc/proto-loader");

const definition = protoLoader.loadSync(
  path.resolve(__dirname, "../../proto/evidence_argument.proto"),
  { keepCase: true, longs: String, enums: String, defaults: true, oneofs: true },
);
const argument = grpc.loadPackageDefinition(definition).humanresolve.argument.v1;

const host = process.env.GRPC_HOST || "127.0.0.1";
const port = process.env.GRPC_PORT || "50051";
const client = new argument.ArgumentService(`${host}:${port}`, grpc.credentials.createInsecure());

function call(method, request) {
  return new Promise((resolve, reject) => {
    // A deadline prevents callers from waiting forever on an unavailable service.
    client[method](request, { deadline: new Date(Date.now() + 5000) }, (error, response) => {
      if (error) reject(error);
      else resolve(response);
    });
  });
}

async function demo() {
  const created = await call("createArgument", {
    dispute_id: process.env.DISPUTE_ID || "1",
    person_id: process.env.PERSON_ID || "A",
    statement: "I returned the borrowed book on Monday.",
    status: "ARGUMENT_STATUS_SUBMITTED",
    evidence: [{
      type: "EVIDENCE_TYPE_IMAGE",
      evidence_url: "https://storage.example.com/return-receipt.png",
      description: "Photo of the return receipt",
    }],
  });
  console.log("CREATE", created);
  console.log("GET", await call("getArgument", { argument_id: created.argument_id }));
  console.log("GET ALL", await call("getAllArguments", { dispute_id: created.dispute_id, page_size: 20 }));
  const updated = await call("updateArgument", {
    argument_id: created.argument_id,
    requester_person_id: created.person_id,
    statement: "I returned the borrowed book on Monday at 10:00.",
    status: "ARGUMENT_STATUS_SUBMITTED",
    evidence: created.evidence,
    expected_version: created.version,
  });
  console.log("UPDATE", updated);
  console.log("DELETE", await call("deleteArgument", {
    argument_id: updated.argument_id,
    requester_person_id: updated.person_id,
    expected_version: updated.version,
  }));
}

if (require.main === module) {
  demo().catch((error) => {
    console.error(`gRPC ${error.code}: ${error.details || error.message}`);
    process.exitCode = 1;
  });
}

module.exports = { client, call };
