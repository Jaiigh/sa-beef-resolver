const http = require('node:http');
const { execFileSync } = require('node:child_process');
const { existsSync } = require('node:fs');
const path = require('node:path');

const PORT = process.env.PORT || 3000;
const DB_PATH = path.join(__dirname, 'beef-resolver.db');
const SCHEMA_PATH = path.join(__dirname, 'schema.sql');

function initialiseDatabase() {
  if (!existsSync(DB_PATH)) {
    execFileSync('sqlite3', [DB_PATH, `.read ${SCHEMA_PATH}`]);
    console.log('Created database and inserted mock data.');
  }
}

function sqlValue(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

function query(sql) {
  const result = execFileSync('sqlite3', ['-json', DB_PATH, sql], { encoding: 'utf8' });
  return result.trim() ? JSON.parse(result) : [];
}

function run(sql) {
  execFileSync('sqlite3', [DB_PATH, sql], { encoding: 'utf8' });
}

function send(response, statusCode, body) {
  response.writeHead(statusCode, { 'Content-Type': 'application/json; charset=utf-8' });
  response.end(JSON.stringify(body, null, 2));
}

function readJson(request) {
  return new Promise((resolve, reject) => {
    let raw = '';
    request.on('data', (chunk) => { raw += chunk; });
    request.on('end', () => {
      try { resolve(raw ? JSON.parse(raw) : {}); }
      catch { reject(new Error('Request body must be valid JSON.')); }
    });
  });
}

function validateBeef(body) {
  const { person_a, person_b, reason, status = 'active' } = body;
  if (!person_a || !person_b || !reason) return 'person_a, person_b, and reason are required.';
  if (person_a === person_b) return 'person_a and person_b must be different people.';
  if (!['active', 'resolved'].includes(status)) return 'status must be active or resolved.';
  return null;
}

initialiseDatabase();

const server = http.createServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`);
  const route = url.pathname.match(/^\/api\/beefs(?:\/(\d+))?\/?$/);

  if (request.method === 'GET' && url.pathname === '/') {
    return send(response, 200, { message: 'Beef Resolver API is running.', endpoints: '/api/beefs' });
  }
  if (!route) return send(response, 404, { error: 'Route not found.' });

  const id = route[1] ? Number(route[1]) : null;
  try {
    if (request.method === 'GET' && !id) {
      const person = url.searchParams.get('person');
      const where = person ? ` WHERE person_a = ${sqlValue(person)}` : '';
      const beefs = query(`SELECT * FROM beefs${where} ORDER BY id`);
      return send(response, 200, {
        count: beefs.length,
        beefs,
        ...(person ? { summary: `${person} has beef with: ${beefs.map((beef) => beef.person_b).join(', ') || 'nobody'}` } : {})
      });
    }

    if (request.method === 'GET' && id) {
      const [beef] = query(`SELECT * FROM beefs WHERE id = ${id}`);
      return beef ? send(response, 200, beef) : send(response, 404, { error: `Beef #${id} was not found.` });
    }

    if (request.method === 'POST' && !id) {
      const body = await readJson(request);
      const problem = validateBeef(body);
      if (problem) return send(response, 400, { error: problem });
      const [beef] = query(`INSERT INTO beefs (person_a, person_b, reason, status) VALUES (${sqlValue(body.person_a)}, ${sqlValue(body.person_b)}, ${sqlValue(body.reason)}, ${sqlValue(body.status || 'active')}); SELECT * FROM beefs WHERE id = last_insert_rowid()`);
      return send(response, 201, beef);
    }

    if (request.method === 'PUT' && id) {
      const body = await readJson(request);
      const problem = validateBeef(body);
      if (problem) return send(response, 400, { error: problem });
      const existing = query(`SELECT id FROM beefs WHERE id = ${id}`)[0];
      if (!existing) return send(response, 404, { error: `Beef #${id} was not found.` });
      run(`UPDATE beefs SET person_a = ${sqlValue(body.person_a)}, person_b = ${sqlValue(body.person_b)}, reason = ${sqlValue(body.reason)}, status = ${sqlValue(body.status || 'active')}, updated_at = CURRENT_TIMESTAMP WHERE id = ${id}`);
      const [beef] = query(`SELECT * FROM beefs WHERE id = ${id}`);
      return send(response, 200, beef);
    }

    if (request.method === 'DELETE' && id) {
      const [beef] = query(`SELECT * FROM beefs WHERE id = ${id}`);
      if (!beef) return send(response, 404, { error: `Beef #${id} was not found.` });
      run(`DELETE FROM beefs WHERE id = ${id}`);
      return send(response, 200, { message: `Beef #${id} deleted.`, deleted: beef });
    }

    return send(response, 405, { error: 'Method not allowed for this route.' });
  } catch (error) {
    const message = error.stderr?.toString().trim() || error.message;
    return send(response, 500, { error: 'Database operation failed.', detail: message });
  }
});

server.listen(PORT, () => console.log(`Beef Resolver API: http://localhost:${PORT}`));
