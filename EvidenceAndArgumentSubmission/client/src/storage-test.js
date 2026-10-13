const assert = require('node:assert/strict');
const {call, client} = require('./client');
const dispute = process.env.TEST_DISPUTE_ID;
if (!dispute) throw new Error('TEST_DISPUTE_ID is required');
const submit = (r, people) => call('submitArgumentForAi', {argument_id:r.argument_id,requester_person_id:r.person_id,people,expected_version:r.version});
async function main() {
 if (process.argv[2] === 'write') {
  const a = await call('createArgument', {dispute_id:dispute,person_id:'storage-a',statement:'เงิน 500 บาทยังไม่ได้คืน',status:'ARGUMENT_STATUS_SUBMITTED',evidence:[{type:'EVIDENCE_TYPE_LINK',evidence_url:'https://example.com/original',description:'original receipt'}]});
  const b = await call('createArgument', {dispute_id:dispute,person_id:'storage-b',statement:'ขอคืนวันจันทร์และจ่ายก่อน 200 บาท',status:'ARGUMENT_STATUS_SUBMITTED'});
  await submit(a,1); await submit(b,2);
  const updated = await call('updateArgument',{argument_id:a.argument_id,requester_person_id:a.person_id,statement:'ตกลงรับ 200 บาทก่อนและรอส่วนที่เหลือวันจันทร์',status:'ARGUMENT_STATUS_SUBMITTED',expected_version:a.version,evidence:[{type:'EVIDENCE_TYPE_DOCUMENT',evidence_url:'https://example.com/updated',description:'updated receipt'}]});
  await submit(updated,1); await submit(updated,1);
  await assert.rejects(call('updateArgument',{argument_id:a.argument_id,requester_person_id:a.person_id,statement:'stale overwrite',expected_version:1}),e=>e.code===10);
  console.log('PASS: saved create/update and submitted AI versions; session='+dispute);
 } else {
  const page=await call('getAllArguments',{dispute_id:dispute,page_size:100});
  assert.equal(page.arguments.length,2);
  const a=page.arguments.find(r=>r.person_id==='storage-a');
  const b=page.arguments.find(r=>r.person_id==='storage-b');
  assert.equal(a.version,2); assert.equal(a.evidence[0].evidence_url,'https://example.com/updated');
  const fetched=await call('getArgument',{argument_id:a.argument_id}); assert.equal(fetched.statement,a.statement);
  await assert.rejects(submit(a,2),e=>e.code===9); // membership survived restart
  await submit(a,1); // duplicate after restart, no new resolution
  const update={argument_id:a.argument_id,requester_person_id:a.person_id,statement:'concurrent update',status:'ARGUMENT_STATUS_SUBMITTED',expected_version:2};
  const raced=await Promise.allSettled([call('updateArgument',update),call('updateArgument',update)]);
  assert.equal(raced.filter(r=>r.status==='fulfilled').length,1);
  assert.equal(raced.find(r=>r.status==='rejected').reason.code,10);
  await assert.rejects(call('deleteArgument',{argument_id:b.argument_id,requester_person_id:a.person_id,expected_version:1}),e=>e.code===7);
  await call('deleteArgument',{argument_id:b.argument_id,requester_person_id:b.person_id,expected_version:1});
  await assert.rejects(call('getArgument',{argument_id:b.argument_id}),e=>e.code===5);
  console.log('PASS: arguments/evidence/membership survive restart; duplicate retry, concurrency, permissions and soft delete');
 }
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>client.close());
