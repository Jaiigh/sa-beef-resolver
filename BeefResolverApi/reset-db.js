const { execFileSync } = require('node:child_process');
const { existsSync, unlinkSync, readFileSync } = require('node:fs');
const path = require('node:path');

const databasePath = path.join(__dirname, 'beef-resolver.db');
const schemaPath = path.join(__dirname, 'schema.sql');

if (existsSync(databasePath)) unlinkSync(databasePath);
execFileSync('sqlite3', [databasePath], { input: readFileSync(schemaPath), stdio: ['pipe', 'inherit', 'inherit'] });
console.log('Database reset with mock data.');
