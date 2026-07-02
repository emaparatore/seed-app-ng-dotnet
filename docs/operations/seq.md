# Seq Logging

## Overview

Seq is the structured log viewer used by the application through Serilog.

The API writes logs to Seq in Docker environments so operational debugging can use searchable, structured events instead of raw container output.

## Where Seq Runs

| Environment | Compose file | UI access | Ingestion endpoint |
|---|---|---|---|
| Local development | `docker/docker-compose.yml` | `http://localhost:8081` | `http://localhost:5341` |
| Production/staging | `docker/docker-compose.deploy.yml` | `127.0.0.1:${SEQ_PORT}` on the VPS | `http://seq:5341` inside Docker |

In deployed environments, the Seq UI is bound to `127.0.0.1` on the VPS. Do not expose it publicly. Use an SSH tunnel instead.

## Local Development

Start the development stack from `docker/`:

```bash
docker compose up
```

Open Seq at:

```text
http://localhost:8081
```

Development Seq runs without authentication by design:

```yaml
SEQ_FIRSTRUN_NOAUTHENTICATION=true
```

The API sends logs to Seq through the Docker network:

```text
Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
```

When the API is run directly on the host with `dotnet run`, `backend/src/Seed.Api/appsettings.Development.json` points Serilog to:

```text
http://localhost:5341
```

## Production And Staging Access

`docker/docker-compose.deploy.yml` publishes the Seq UI only on localhost of the VPS:

```yaml
ports:
  - "127.0.0.1:${SEQ_PORT:-8081}:80"
```

Recommended ports:

| Environment | `SEQ_PORT` |
|---|---:|
| Production | `8081` |
| Staging | `8082` |

Open an SSH tunnel from your local machine:

```bash
ssh -L 8081:127.0.0.1:8081 deploy@YOUR_VPS_HOST
```

Then open:

```text
http://localhost:8081
```

For staging, use the staging port:

```bash
ssh -L 8082:127.0.0.1:8082 deploy@YOUR_VPS_HOST
```

Then open:

```text
http://localhost:8082
```

## Authentication Setup

Production starts with `SEQ_NOAUTH=true` so the first administrator can be created in the Seq UI.

Initial `.env` values:

```env
SEQ_PORT=8081
SEQ_NOAUTH=true
SEQ_API_KEY=
```

After the first deploy:

1. Connect through the SSH tunnel.
2. Open the Seq UI.
3. Create an administrator user in Seq.
4. Create an ingestion API key for the API service.
5. Update the deployment `.env`:

```env
SEQ_NOAUTH=false
SEQ_API_KEY=<seq-ingestion-api-key>
```

6. Restart the stack:

```bash
docker compose -f docker-compose.deploy.yml up -d
```

The API key is passed to Serilog by the deploy compose file:

```text
Serilog__WriteTo__1__Args__apiKey=${SEQ_API_KEY:-}
```

Keep `SEQ_API_KEY` secret. Treat it like any other production credential.

## Common Queries

Useful starting points in the Seq search box:

```text
@Level = 'Error'
```

```text
@Level in ['Warning', 'Error']
```

```text
RequestPath like '/api/%'
```

```text
SourceContext like '%Stripe%'
```

For request debugging, start from the failed request event and inspect structured properties such as path, status code, elapsed time, exception, correlation identifiers, user identifiers, and source context when available.

## Operational Checks

Check the container status on the VPS:

```bash
docker compose -f docker-compose.deploy.yml ps seq
```

Check recent Seq container logs:

```bash
docker compose -f docker-compose.deploy.yml logs --tail=100 seq
```

Check whether the API is configured to send logs to Seq:

```bash
docker compose -f docker-compose.deploy.yml exec api printenv | grep Serilog__WriteTo
```

## Troubleshooting

### Seq UI Does Not Open Through The Tunnel

Verify the stack is running and confirm the configured port:

```bash
docker compose -f docker-compose.deploy.yml ps seq
grep SEQ_PORT .env
```

Make sure the local tunnel port matches `SEQ_PORT`.

### API Logs Do Not Appear

Check these points:

- The `seq` container is running.
- The `api` container has `Serilog__WriteTo__1__Args__serverUrl=http://seq:5341`.
- If authentication is enabled, `SEQ_API_KEY` is set and matches an active ingestion API key in Seq.
- The API is emitting logs at a level accepted by the environment configuration.

### Authentication Was Left Disabled

If production still has `SEQ_NOAUTH=true`, create the administrator user and API key, then set:

```env
SEQ_NOAUTH=false
SEQ_API_KEY=<seq-ingestion-api-key>
```

Restart the stack after changing `.env`.

### Disk Usage Grows Too Much

Seq stores data in the Docker volume `seq_data`. Configure retention in the Seq UI according to the project's operational needs. Do not delete the `seq_data` volume unless you intentionally want to remove retained logs and Seq configuration.
