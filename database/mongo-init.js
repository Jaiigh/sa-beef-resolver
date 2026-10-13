try {
  rs.status();
} catch (error) {
  if (error.code !== 94) throw error;
  rs.initiate({_id: "rs0", members: [{_id: 0, host: "mongo:27017"}]});
}
let primary = false;
for (let attempt = 0; attempt < 60; attempt++) {
  if (db.hello().isWritablePrimary) { primary = true; break; }
  sleep(1000);
}
if (!primary) throw new Error("MongoDB replica set did not elect a primary");
const ai = db.getSiblingDB("ai_resolution_db");
for (const name of ["sessions", "jobs", "resolutions"]) {
  if (!ai.getCollectionNames().includes(name)) ai.createCollection(name);
}
ai.resolutions.createIndex({sessionId: 1, version: 1}, {unique: true});
print("MongoDB rs0 ready; ai_resolution_db initialized");
