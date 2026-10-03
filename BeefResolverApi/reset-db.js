const { pool, resetDatabase } = require('./db');

resetDatabase()
  .then(() => console.log('Database reset with mock data.'))
  .catch((error) => { console.error(error.message); process.exitCode = 1; })
  .finally(() => pool.end());
