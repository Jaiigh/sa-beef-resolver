const assert = require("node:assert/strict");
const { client, call } = require("./client");

async function demo() {
  const dispute = process.env.DEMO_DISPUTE_ID || `ai-demo-${Date.now()}`;
  const a = await call("createArgument", {
    dispute_id: dispute, person_id: "person-a",
    statement: "เพื่อนยืมเงิน 500 บาทแล้วไม่คืนตามวันที่ตกลง",
    status: "ARGUMENT_STATUS_SUBMITTED",
    evidence: [{ type: "EVIDENCE_TYPE_LINK", evidence_url: "https://example.com/receipt", description: "ข้อมูลอ้างอิงการโอน" }],
  });
  const b = await call("createArgument", {
    dispute_id: dispute, person_id: "person-b",
    statement: "ฉันขอเลื่อนคืนไปวันจันทร์และจะแบ่งคืนก่อน 200 บาท",
    status: "ARGUMENT_STATUS_SUBMITTED",
  });
  const request = { argument_id: a.argument_id, requester_person_id: a.person_id, people: 1, expected_version: a.version };
  const first = await call("submitArgumentForAi", request);
  assert.equal(first.queued, true);
  const duplicate = await call("submitArgumentForAi", request);
  assert.equal(duplicate.message_id, first.message_id);
  await assert.rejects(call("submitArgumentForAi", { ...request, requester_person_id: "person-b" }), e => e.code === 7);
  await assert.rejects(call("submitArgumentForAi", { ...request, expected_version: 999 }), e => e.code === 10);
  await assert.rejects(call("submitArgumentForAi", { ...request, people: 2 }), e => e.code === 9);
  await assert.rejects(call("submitArgumentForAi", { ...request, people: 3 }), e => e.code === 3);
  const second = await call("submitArgumentForAi", {
    argument_id: b.argument_id, requester_person_id: b.person_id, people: 2, expected_version: b.version,
  });
  assert.equal(second.queued, true);
  const draft = await call("createArgument", {
    dispute_id: dispute, person_id: "person-a", statement: "ข้อความ draft ยังไม่พร้อมให้ AI วิเคราะห์",
    status: "ARGUMENT_STATUS_DRAFT",
  });
  await assert.rejects(call("submitArgumentForAi", {
    argument_id: draft.argument_id, requester_person_id: draft.person_id, people: 1, expected_version: draft.version,
  }), e => e.code === 9);
  console.log(JSON.stringify({ sessionId: dispute, first, second }, null, 2));
  const resultCommand = process.env.GRPC_HOST === "evidence-service"
    ? "docker compose exec ai-worker-0 python result.py"
    : "python result.py";
  console.log(`Read AI result: ${resultCommand} --session ${dispute} --min-version 2 --wait 180`);
}

demo().catch(error => {
  console.error(`gRPC ${error.code}: ${error.details || error.message}`);
  process.exitCode = 1;
}).finally(() => client.close());
