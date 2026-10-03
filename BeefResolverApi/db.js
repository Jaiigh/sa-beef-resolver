const { readFileSync } = require('node:fs');
const path = require('node:path');
const { Pool } = require('pg');

const pool = new Pool({
  connectionString: process.env.DATABASE_URL || 'postgres://beef_user:beef_pass@localhost:5433/beef_db'
});

const sqlFile = (name) => readFileSync(path.join(__dirname, name), 'utf8');

// Creates the table if needed and inserts the mock data only into an empty table.
async function initialiseDatabase() {
  await pool.query(sqlFile('schema.sql'));
  const { rows } = await pool.query('SELECT COUNT(*)::int AS count FROM beefs');
  if (rows[0].count === 0) {
    await pool.query(sqlFile('seed.sql'));
    console.log('Created table and inserted mock data.');
  }
}

async function resetDatabase() {
  await pool.query('DROP TABLE IF EXISTS beefs');
  await initialiseDatabase();
}

module.exports = { pool, initialiseDatabase, resetDatabase };
