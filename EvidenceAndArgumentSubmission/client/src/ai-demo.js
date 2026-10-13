// Manual submission client: one user-selected message per invocation.
const { parseArgs } = require('node:util');
const readline = require('node:readline/promises');
const { client, call } = require('./client');

async function main() {
  const { values } = parseArgs({ options: {
    session: {type:'string'}, people: {type:'string'}, person: {type:'string'},
    message: {type:'string'}, 'evidence-url': {type:'string'},
    'evidence-description': {type:'string'}, 'evidence-type': {type:'string'},
    'argument-id': {type:'string'}, help: {type:'boolean'},
  }});
  if (values.help) {
    console.log('Interactive: docker compose run --rm --build --no-deps demo-client');
    console.log('One submission: node client/src/ai-demo.js --session demo-001 --people 1 --message "your argument"');
    console.log('Optional: --person ID --evidence-url URL --evidence-description TEXT --evidence-type LINK|IMAGE|DOCUMENT|OTHER');
    console.log('Retry a saved argument: --argument-id UUID --people 1 (does not create another argument)');
    return;
  }
  const interactive = !values['argument-id'] && !(values.session && values.people && values.message);
  let rl;
  try {
    if (interactive) {
      if (!process.stdin.isTTY) throw new Error('Interactive input requires a terminal; use docker compose run without -T, or provide --session --people --message.');
      rl = readline.createInterface({input:process.stdin,output:process.stdout});
      console.log('ส่งข้อมูลครั้งละหนึ่งฝ่าย ผ่าน ASP.NET Core → RabbitMQ → AI');
      values.session ||= (await rl.question('Session ID (ใช้ค่าเดียวกันทั้งสองฝ่าย): ')).trim();
      values.people ||= (await rl.question('ฝ่ายที่ส่ง [1/2]: ')).trim();
    }
    const people = Number(values.people);
    if (![1,2].includes(people)) throw new Error('people must be 1 or 2');
    let record;
    if (values['argument-id']) {
      record = await call('getArgument',{argument_id:values['argument-id']});
      if (values.person && values.person !== record.person_id) throw new Error('person does not match the saved argument author');
    } else {
      if (!/^[A-Za-z0-9_-]{1,100}$/.test(values.session || '')) throw new Error('Session ID must contain 1-100 letters/digits/_/-');
      const defaultPerson = people === 1 ? 'person-a' : 'person-b';
      if (interactive) {
        values.person ||= (await rl.question(`Person ID [${defaultPerson}]: `)).trim() || defaultPerson;
        values.message ||= await rl.question('ข้อความของคุณ: ');
        values['evidence-url'] ||= (await rl.question('Evidence URL (Enter เพื่อข้าม): ')).trim();
        if (values['evidence-url']) {
          values['evidence-description'] ||= await rl.question('คำอธิบาย evidence: ');
          values['evidence-type'] ||= (await rl.question('ชนิด [LINK/IMAGE/DOCUMENT/OTHER; default LINK]: ')).trim().toUpperCase() || 'LINK';
        }
      }
      if (!values.message?.trim()) throw new Error('message must not be empty');
      const type = (values['evidence-type'] || 'LINK').toUpperCase();
      if (!['LINK','IMAGE','DOCUMENT','OTHER'].includes(type)) throw new Error('invalid evidence type');
      if (values['evidence-url'] && !/^https?:$/.test(new URL(values['evidence-url']).protocol)) throw new Error('evidence URL must be HTTP(S)');
      record = await call('createArgument',{
        dispute_id:values.session, person_id:values.person || defaultPerson, statement:values.message,
        status:'ARGUMENT_STATUS_SUBMITTED', evidence:values['evidence-url'] ? [{
          type:'EVIDENCE_TYPE_'+type,evidence_url:values['evidence-url'],description:values['evidence-description'] || '',
        }] : [],
      });
    }
    console.log(`Saved argument: ${record.argument_id} (version ${record.version})`);
    const submitCommand = process.env.GRPC_HOST === 'evidence-service'
      ? 'docker compose run --rm --no-deps demo-client node client/src/ai-demo.js'
      : 'node client/src/ai-demo.js';
    console.log(`If queue submission fails, retry: ${submitCommand} --argument-id ${record.argument_id} --people ${people}`);
    const submitted = await call('submitArgumentForAi',{
      argument_id:record.argument_id,requester_person_id:record.person_id,people,expected_version:record.version,
    });
    console.log(JSON.stringify({sessionId:record.dispute_id,people,argumentId:record.argument_id,...submitted},null,2));
    console.log('ส่งเฉพาะข้อความของคุณแล้ว ระบบไม่ได้ส่งข้อความของอีกฝ่ายให้');
    const command = process.env.GRPC_HOST === 'evidence-service'
      ? 'docker compose exec ai-worker-0 python result.py' : 'python result.py';
    console.log(`Latest status/result: ${command} --session ${record.dispute_id} --wait 180`);
    console.log(`All AI rounds: ${command} --session ${record.dispute_id} --history`);
  } finally { rl?.close(); }
}
main().catch(error=>{
  console.error(`Submission failed: ${error.details || error.message}`);
  process.exitCode=1;
}).finally(()=>client.close());
