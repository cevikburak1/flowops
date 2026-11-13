# Architecture

FlowOps starts as a modular monolith. The web server renders the workspace and forwards same-origin authentication and API traffic to ASP.NET Core. Keycloak owns identity. PostgreSQL owns organization membership and application records. Elsa owns workflow execution.

```mermaid
flowchart LR
    Browser --> Next
    Next --> API[ASP.NET Core API and BFF]
    Browser --> Keycloak
    API --> Keycloak
    API --> PostgreSQL
    API --> Elsa
    Elsa --> PostgreSQL
    API -. integration events .-> RabbitMQ
    RabbitMQ -. operator integration .-> n8n
```

The web boundary uses a server-managed session and same-origin write protection. The API also supports Keycloak bearer tokens with a FlowOps audience. Keycloak subjects identify users. Organization membership in the application database grants tenant access.

Purchase requests, approval tasks, audit entries, and notifications use organization-scoped keys. Commands update domain state with optimistic concurrency. The purchase workflow uses a stable instance ID and durable bookmarks. A PostgreSQL command queue bridges the transaction boundary between business state and Elsa. Workflow callbacks must be idempotent across replay.

## Data model

```mermaid
erDiagram
    ORGANIZATION ||--o{ MEMBERSHIP : grants
    ORGANIZATION ||--o{ PURCHASE : owns
    PURCHASE ||--o{ APPROVAL : requires
    PURCHASE ||--o{ AUDIT_EVENT : records
    PURCHASE ||--o{ NOTIFICATION : generates
    PURCHASE ||--o{ WORKFLOW_COMMAND : schedules
```

See [TASKS.md](TASKS.md) for implementation and verification status.
