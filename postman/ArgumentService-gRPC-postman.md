# Testing the gRPC ArgumentService in Postman

Postman's JSON (v2.1) collection files only hold HTTP requests, so the gRPC requests are created once in the app with the steps and messages below. After that you can keep them in a Postman collection and share them through your workspace.

## One-time setup
1. Start the Beef REST API (`BeefResolverApi`, `npm start`) and the gRPC service (`dotnet run --project server/HumanResolve.ArgumentService`).
2. In Postman choose **New -> gRPC**.
3. Server URL: `localhost:50051` (turn **TLS off**; the service is plaintext HTTP/2).
4. On the **Service definition** tab choose **Use server reflection** (the service has reflection enabled, so no `.proto` file is needed). If reflection is unavailable, choose **Import a .proto file** and select `EvidenceAndArgumentSubmission/proto/evidence_argument.proto`.
5. Pick the method from the dropdown, paste the message on the **Message** tab, click **Invoke**.

The service links to the REST API, so `dispute_id` is a beef id and `person_id` is that beef's `person_a` or `person_b`. Run `npm run reset-db` in `BeefResolverApi` first.

## 1. CreateArgument (success -> returns an Argument)
```json
{
  "dispute_id": "1",
  "person_id": "A",
  "statement": "I returned the borrowed headphones on Monday.",
  "status": "ARGUMENT_STATUS_SUBMITTED",
  "evidence": [
    {
      "type": "EVIDENCE_TYPE_IMAGE",
      "evidence_url": "https://storage.example.com/return-receipt.png",
      "description": "Photo of the return receipt"
    }
  ]
}
```
Copy the returned `argument_id` and `version` for the next steps.

## 2. GetArgument
```json
{ "argument_id": "<argument_id from step 1>" }
```

## 3. GetAllArguments
```json
{ "dispute_id": "1", "page_size": 20 }
```

## 4. UpdateArgument
```json
{
  "argument_id": "<argument_id>",
  "requester_person_id": "A",
  "statement": "I returned the headphones on Monday at 10:00.",
  "status": "ARGUMENT_STATUS_SUBMITTED",
  "expected_version": 1
}
```

## 5. DeleteArgument
```json
{ "argument_id": "<argument_id>", "requester_person_id": "A", "expected_version": 2 }
```
Use the `version` returned by the update (2) after step 4.

## Error cases to show (CreateArgument)
Change only the noted fields in the step 1 message.

| Change | Expected status |
| --- | --- |
| `person_id` = `"C"` (not in beef #1) | `PERMISSION_DENIED` |
| `dispute_id` = `"999"` | `NOT_FOUND` |
| `dispute_id` = `"4"` (already resolved) | `FAILED_PRECONDITION` |
| `dispute_id` = `"abc"` | `INVALID_ARGUMENT` |
| Stop the REST API, then invoke | `UNAVAILABLE` |
| Update with the wrong `expected_version` | `ABORTED` |
| Update with `requester_person_id` = `"B"` | `PERMISSION_DENIED` |
