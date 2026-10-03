const http = require('node:http');
const { pool, initialiseDatabase } = require('./db');

const PORT = process.env.PORT || 3000;

async function query(sql, params = []) {
  const { rows } = await pool.query(sql, params);
  return rows;
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
      const beefs = person
        ? await query('SELECT * FROM beefs WHERE person_a = $1 ORDER BY id', [person])
        : await query('SELECT * FROM beefs ORDER BY id');
      return send(response, 200, {
        count: beefs.length,
        beefs,
        ...(person ? { summary: `${person} has beef with: ${beefs.map((beef) => beef.person_b).join(', ') || 'nobody'}` } : {})
      });
    }

    if (request.method === 'GET' && id) {
      const [beef] = await query('SELECT * FROM beefs WHERE id = $1', [id]);
      return beef ? send(response, 200, beef) : send(response, 404, { error: `Beef #${id} was not found.` });
    }

    if (request.method === 'POST' && !id) {
      const body = await readJson(request);
      const problem = validateBeef(body);
      if (problem) return send(response, 400, { error: problem });
      const [beef] = await query(
        'INSERT INTO beefs (person_a, person_b, reason, status) VALUES ($1, $2, $3, $4) RETURNING *',
        [body.person_a, body.person_b, body.reason, body.status || 'active']
      );
      return send(response, 201, beef);
    }

    if (request.method === 'PUT' && id) {
      const body = await readJson(request);
      const problem = validateBeef(body);
      if (problem) return send(response, 400, { error: problem });
      const [beef] = await query(
        'UPDATE beefs SET person_a = $1, person_b = $2, reason = $3, status = $4, updated_at = NOW() WHERE id = $5 RETURNING *',
        [body.person_a, body.person_b, body.reason, body.status || 'active', id]
      );
      return beef ? send(response, 200, beef) : send(response, 404, { error: `Beef #${id} was not found.` });
    }

    if (request.method === 'DELETE' && id) {
      const [beef] = await query('DELETE FROM beefs WHERE id = $1 RETURNING *', [id]);
      if (!beef) return send(response, 404, { error: `Beef #${id} was not found.` });
      return send(response, 200, { message: `Beef #${id} deleted.`, deleted: beef });
    }

    return send(response, 405, { error: 'Method not allowed for this route.' });
  } catch (error) {
    if (error.code === '23505') return send(response, 409, { error: 'That person_a/person_b beef already exists.' });
    return send(response, 500, { error: 'Database operation failed.', detail: error.message });
  }
});

initialiseDatabase()
  .then(() => server.listen(PORT, () => console.log(`Beef Resolver API: http://localhost:${PORT}`)))
  .catch((error) => {
    console.error(`Could not connect to the database: ${error.message}
Start it with: docker compose up -d`);
    process.exit(1);
  });
