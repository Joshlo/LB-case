# Smart Locker ETL

A .NET console application that reads the daily parcel batch (`datasource.csv`), allocates a locker compartment for each parcel, generates a pin code and notifies the recipient by SMS and/or email through the Locker and Notification APIs.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop with Docker Compose v2 (for the mock API)

## 1. Start the mock API

```bash
docker compose pull
docker compose up --wait
```

The API is available at `http://localhost:18080/api/v1/`. Swagger is at `http://localhost:18080/swagger`, and health is at `http://localhost:18080/health`.

The API keeps its state in memory while the container runs. Compartments allocated in an earlier run stay occupied, so to start a fresh simulation, restart the container:

```bash
docker compose down
docker compose up --wait
```

## 2. Run the ETL

The application reads `../datasource.csv` relative to its working directory and calls the API at `http://localhost:18080`. Place `datasource.csv` in `ETL/bin/Debug/` and run the built executable from its output folder:

```bash
dotnet build ETL
cd ETL/bin/Debug/net10.0
dotnet ETL.dll
```

Example output:

```
Allocated PARCEL-001 to LOCKER-CPH-01/C01.
Allocated PARCEL-002 to LOCKER-CPH-01/C03.
...
```

## 3. Run the tests

```bash
dotnet test ETL.Tests
```

The tests use xUnit and FakeItEasy. The API client, code generator and state store are faked, so the tests do not need the mock API to be running.

## How it works

1. **Fetch lockers.** If the Locker API cannot be reached, the run stops.
2. **Read and deduplicate the CSV.** Rows are grouped by `parcel_id` (case-insensitive), and only the latest row per parcel is used (by `timestamp`, then `event_id`).
3. **Process each parcel** based on its stored state:
   - **Older or same-age row than the stored state:** skipped.
   - **New parcel:** if the target locker has a free compartment of the required size or larger, an 8-digit pin code is generated, the compartment is allocated and the recipient is notified on every channel they have (SMS and/or email).
   - **Invalid row** (missing parcel ID, target locker or size, or no phone and no email): stored as `Unknown`, so it is allocated once a valid row arrives.
   - **Already allocated, nothing changed:** only the timestamp is updated.
   - **Already allocated, locker or size changed:** the current compartment is checked first.
     - Compartment is available again: the parcel has been picked up and is marked `Completed`.
     - Sensor reports something in the compartment: treated as a conflict and left for manual handling.
     - Larger size or different locker: a new compartment is allocated **before** the old one is released, so the parcel is never without a compartment, and the recipient gets the new pin code.
     - Smaller size: the parcel keeps its current compartment.

## Project structure

| Path | Contents |
|---|---|
| `ETL/` | The console application |
| `ETL/EtlRunner.cs` | The ETL flow described above |
| `ETL/ApiClient.cs`, `ETL/ApiModels.cs` | HTTP client and models for the Locker and Notification APIs |
| `ETL/StateStore.cs` | State per parcel (in memory) |
| `ETL/CodeGenerator.cs` | Pin code generation |
| `ETL.Tests/` | Unit tests |
| `locker-api.yaml`, `notification-api.yaml` | API specifications |

## Known limitations

- **State is kept in memory.** It is lost when the application exits, so running the same file twice will try to allocate the parcels again.
- **Paths and URLs are hardcoded.** The input file path and API base URL are set in `ETL/Program.cs`.
- **Some skipped parcels are not logged.** A parcel that cannot be allocated because there is no free compartment, or because the allocation call fails, is skipped silently.
