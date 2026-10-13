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
const authentication = new argument.AuthenticationService(`${host}:${port}`, grpc.credentials.createInsecure());

function call(method, request, accessToken) {
  return new Promise((resolve, reject) => {
    const metadata = new grpc.Metadata();
    if (accessToken) metadata.set("authorization", `Bearer ${accessToken}`);
    client[method](request, metadata, { deadline: new Date(Date.now() + 5000) }, (error, response) => {
      if (error) reject(error);
      else resolve(response);
    });
  });
}

function authCall(method, request, accessToken) {
  return new Promise((resolve, reject) => {
    const metadata = new grpc.Metadata();
    if (accessToken) metadata.set("authorization", `Bearer ${accessToken}`);
    authentication[method](request, metadata, { deadline: new Date(Date.now() + 5000) }, (error, response) => {
      if (error) reject(error);
      else resolve(response);
    });
  });
}

async function getSession(email = process.env.AUTH_EMAIL || "demo@example.com", password = process.env.AUTH_PASSWORD || "CorrectHorseBatteryStaple!", displayName = process.env.AUTH_DISPLAY_NAME || "Demo User") {
  try {
    return await authCall("register", { email, password, display_name: displayName });
  } catch (error) {
    if (error.code !== grpc.status.ALREADY_EXISTS) throw error;
    return authCall("login", { email, password });
  }
}

async function demo() {
  const session = await getSession();
  const token = session.access_token;
  const created = await call("createArgument", {
    dispute_id: "dispute-001",
    statement: "I returned the borrowed book on Monday.",
    status: "ARGUMENT_STATUS_SUBMITTED",
    evidence: [{
      type: "EVIDENCE_TYPE_IMAGE",
      evidence_url: "https://storage.example.com/return-receipt.png",
      description: "Photo of the return receipt",
    }],
  }, token);
  console.log("CREATE", created);
  console.log("GET", await call("getArgument", { argument_id: created.argument_id }, token));
  console.log("GET ALL", await call("getAllArguments", { dispute_id: created.dispute_id, page_size: 20 }, token));
  const updated = await call("updateArgument", {
    argument_id: created.argument_id,
    statement: "I returned the borrowed book on Monday at 10:00.",
    status: "ARGUMENT_STATUS_SUBMITTED",
    evidence: created.evidence,
    expected_version: created.version,
  }, token);
  console.log("UPDATE", updated);
  console.log("DELETE", await call("deleteArgument", {
    argument_id: updated.argument_id,
    expected_version: updated.version,
  }, token));
}

if (require.main === module) {
  demo().catch((error) => {
    console.error(`gRPC ${error.code}: ${error.details || error.message}`);
    process.exitCode = 1;
  });
}

module.exports = { client, authentication, call, authCall, getSession };
