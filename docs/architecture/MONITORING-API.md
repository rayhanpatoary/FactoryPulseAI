# Machine monitoring REST API

The API reads machines and telemetry from the configured SQLite database. Apply the migration before making monitoring requests. Enable simulated acquisition to populate a local database with sample data. The development base URL is `https://localhost:7161`. Configure authentication and authorization at the deployment boundary before exposing these endpoints publicly. [Database setup](DATABASE.md) · [Simulator setup](SIMULATION.md)

## Endpoints

| Request | Success response |
| --- | --- |
| `GET /api/machines` | `200` with all machine summaries sorted by machine code |
| `GET /api/machines/{id}` | `200` with machine metadata, status, and UTC creation and update timestamps |
| `GET /api/machines/{id}/readings/latest` | `200` with the latest reading, or `204` for a registered machine with no readings |
| `GET /api/machines/{id}/readings` | `200` with a page of readings, including an empty `items` array when the page has no matches |
| `GET /api/machines/summary` | `200` with the total machine count and counts by operational status |

Machine IDs are GUIDs. The status strings `Offline`, `Running`, `Idle`, and `Faulted` are stable codes for clients to translate. The summary includes every status count, including zero counts.

Malformed IDs and invalid query parameters return `400` with validation ProblemDetails. A well-formed ID that does not identify a registered machine returns `404` with ProblemDetails. Unexpected failures return `500` with generic ProblemDetails.

## History parameters

| Parameter | Default | Accepted values |
| --- | --- | --- |
| `page` | `1` | Integer from 1 through 10,000 |
| `pageSize` | `25` | Integer from 1 through 100 |
| `fromUtc` | Unbounded | Inclusive lower timestamp bound |
| `toUtc` | Unbounded | Exclusive upper timestamp bound |

Supply ISO 8601 timestamps with seconds, optional fractional seconds, and an explicit UTC suffix of `Z` or `+00:00`. Encode a plus sign as `%2B` in the query string. Either bound may be omitted. When both are present, `fromUtc` must precede `toUtc`.

Readings are ordered by UTC timestamp descending. For a timestamp tie, the reading with the higher database ID comes first. `hasMore` is true when additional matching rows follow the returned items. New readings can shift offsets between requests. A fixed upper timestamp bound lets clients limit a sequence of requests to the same time window. Narrow the time window for deeper history.

## Examples

### Machine list

```http
GET /api/machines
```

```json
[
  {
    "id": "e05831e8-41d0-4ae8-8746-51f890af159e",
    "machineCode": "CNC-001",
    "name": "CNC-001",
    "status": "Running"
  },
  {
    "id": "c2041131-9803-4b57-83ce-fc5b36646d96",
    "machineCode": "CNC-002",
    "name": "CNC-002",
    "status": "Running"
  },
  {
    "id": "579cc6cd-9dfe-4333-ac96-b521140af59a",
    "machineCode": "CNC-003",
    "name": "CNC-003",
    "status": "Idle"
  }
]
```

### Machine details

```http
GET /api/machines/e05831e8-41d0-4ae8-8746-51f890af159e
```

```json
{
  "id": "e05831e8-41d0-4ae8-8746-51f890af159e",
  "machineCode": "CNC-001",
  "name": "CNC-001",
  "description": null,
  "machineType": "CNC",
  "location": "Simulation",
  "status": "Running",
  "createdAtUtc": "2026-01-05T08:00:00Z",
  "updatedAtUtc": "2026-01-05T08:00:00Z"
}
```

### Machine summary

```http
GET /api/machines/summary
```

```json
{"total": 3, "offline": 0, "running": 2, "idle": 1, "faulted": 0}
```

### Latest reading

```http
GET /api/machines/e05831e8-41d0-4ae8-8746-51f890af159e/readings/latest
```

```json
{
  "machineId": "e05831e8-41d0-4ae8-8746-51f890af159e",
  "timestampUtc": "2026-01-05T08:03:00Z",
  "temperature": 50,
  "vibration": 1.1,
  "rpm": 2350,
  "torque": 18,
  "toolWear": 0.07
}
```

Temperature is in °C, vibration in mm/s, torque in N·m, and tool wear is a fraction from 0 to 1.

### History page

```http
GET /api/machines/e05831e8-41d0-4ae8-8746-51f890af159e/readings?page=1&pageSize=1&fromUtc=2026-01-05T08:02:00Z&toUtc=2026-01-05T08:03:00Z
```

```json
{
  "page": 1,
  "pageSize": 1,
  "hasMore": false,
  "items": [
    {
      "machineId": "e05831e8-41d0-4ae8-8746-51f890af159e",
      "timestampUtc": "2026-01-05T08:02:00Z",
      "temperature": 46,
      "vibration": 0.9,
      "rpm": 2200,
      "torque": 16,
      "toolWear": 0.04
    }
  ]
}
```
