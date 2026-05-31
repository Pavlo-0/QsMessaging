# IntegrationTest_V2

RabbitMQ integration suite for `QsMessaging`. It runs the ordinary request-response flow and horizontal scale-out scenarios with:

- one runner;
- two sender service instances;
- two receiver service instances;
- QsMessaging events for service heartbeats and sender commands;
- QsMessaging messages for sender results;
- QsMessaging request-response calls for the workload itself.

The runner console shows `WAIT`, `PROGRESS`, `PASS`, `FAIL`, and `SKIP` states. Scenario failures are written to `logs/integration-test-v2-*.log`. When `run.ps1` is used, stdout and stderr from background agents are also written to `logs`.

## Run from PowerShell

RabbitMQ must be available on `localhost:5672` with the default `guest` / `guest` credentials.

```powershell
.\IntegrationTest_V2\run.ps1
```

Use `-NoBuild` after a successful build:

```powershell
.\IntegrationTest_V2\run.ps1 -NoBuild
```

## Run from Visual Studio

Open `IntegrationTest_V2\IntegrationTest_V2.sln`, install the SwitchStartupProject extension if needed, and select `IntegrationTest_V2.RabbitMq`. This mirrors the multi-project startup style used by the original `IntegrationTest`.

RabbitMQ connection settings and runner timeouts can be adjusted in each project's `appsettings.json`.
