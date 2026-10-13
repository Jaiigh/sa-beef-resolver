# Beef Resolver REST API

This homework uses one resource, **beefs**. A row means `person_a has beef with person_b` and stores the reason and resolution status. The mock data shows the requested relationships: A has beef with B, C, D, and E; B has beef with A, C, and X.

## Set up on another computer

Each person should run their own local copy of the API and database. The SQLite
database is stored in `beef-resolver.db`, which is created from `schema.sql` on
the first start and is intentionally not shared through Git.

### Prerequisites

- [Node.js](https://nodejs.org/) 18 or later
- SQLite command-line tools (`sqlite3`)
- Git

On macOS, SQLite is normally already installed. On Windows, install SQLite from
[sqlite.org](https://www.sqlite.org/download.html) and make sure `sqlite3` is
available in a new terminal window.

### First run

```bash
git clone https://github.com/Jaiigh/sa-beef-resolver.git
cd sa-beef-resolver
git switch crud-hw
npm start
```

Then open `http://localhost:3000` in a browser. The API will be available at
`http://localhost:3000/api/beefs`, and the first start creates that computer's
own `beef-resolver.db` with seven sample records.

### Start again or restore the sample data

From the project folder:

```bash
npm start
```

To discard local database changes and recreate the original seven sample
records, stop the server and run:

```bash
npm run reset-db
npm start
```

The database is local to each computer. Changes your friend makes will not
appear on your computer unless you later move the project to a shared hosted
database.

## Run it

Open a terminal in this folder and run:

```bash
npm start
```

The API runs at `http://localhost:3000`. The database file is created automatically on the first start. To restore the original mock data before recording, stop the server and run:

```bash
npm run reset-db
npm start
```

Use Postman, Insomnia, or VS Code's REST Client to make the requests below. Set the request header `Content-Type: application/json` for POST and PUT.

## API endpoints

| Requirement | Method and URL | What to show |
| --- | --- | --- |
| GET ALL | `GET /api/beefs` | All 7 mock records |
| Useful project query | `GET /api/beefs?person=A` | A has beef with B, C, D, E |
| GET ONE | `GET /api/beefs/1` | One selected relationship |
| POST | `POST /api/beefs` | Create a new relationship |
| PUT | `PUT /api/beefs/8` | Change the created relationship |
| DELETE | `DELETE /api/beefs/8` | Remove it |

### POST body

```json
{
  "person_a": "Maya",
  "person_b": "Nina",
  "reason": "They disagree about who should choose the team name.",
  "status": "active"
}
```

### PUT body

```json
{
  "person_a": "Maya",
  "person_b": "Nina",
  "reason": "They discussed the team-name issue and reached an agreement.",
  "status": "resolved"
}
```

## Recording plan (under 5 minutes)

Reset the database immediately before recording so that the new POST always gets ID 8. Keep Postman on the left and a terminal on the right. In the terminal, run `sqlite3 beef-resolver.db "SELECT id, person_a, person_b, status FROM beefs ORDER BY id;"` after every changing request to visibly prove the database changed.

1. **0:00–0:25 — Introduce.** Say: “My resource is `beefs`. Each record represents one direction of an argument: `person_a` has beef with `person_b`. It has a reason and a status. I use SQLite as the database.” Show `schema.sql` and the running server.
2. **0:25–0:55 — GET ALL.** Send `GET http://localhost:3000/api/beefs`. Say: “This is GET ALL. It returns the seven mock rows stored in the database.”
3. **0:55–1:15 — Project scenario.** Send `GET http://localhost:3000/api/beefs?person=A`. Say: “This answers the core question: who does A have beef with? The API returns B, C, D, and E.”
4. **1:15–1:35 — GET ONE.** Send `GET http://localhost:3000/api/beefs/1`. Say: “This is GET ONE; it retrieves only the relationship with ID 1.”
5. **1:35–2:15 — POST.** Send the POST body above to `POST http://localhost:3000/api/beefs`. Say: “POST creates a new beef relationship. The response is 201 Created and the new row has ID 8.” Then run the SQLite command and point out row 8.
6. **2:15–3:00 — PUT.** Send the PUT body above to `PUT http://localhost:3000/api/beefs/8`. Say: “PUT replaces the details of ID 8. I changed both the explanation and status from active to resolved.” Run the SQLite command again and point out the updated row.
7. **3:00–3:30 — DELETE.** Send `DELETE http://localhost:3000/api/beefs/8`. Say: “DELETE removes ID 8. The response includes the deleted record.” Run the SQLite command a final time and show that 8 is gone.
8. **3:30–3:50 — Close.** Send GET ALL again. Say: “The API now has the original seven records again. I have demonstrated GET ALL, GET ONE, POST, PUT, and DELETE, with database changes after each write.”

## Notes for the marker

- `201 Created` is returned for POST.
- `400 Bad Request` is returned if required fields are missing, the people are the same, or status is invalid.
- `404 Not Found` is returned for a missing ID.
- Relationship direction is intentional: `A -> B` and `B -> A` are separate records, matching the “who does this person have beef with?” question.
