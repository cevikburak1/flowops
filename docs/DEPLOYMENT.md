# Deployment

The current Compose file is a local development stack. It binds PostgreSQL and Keycloak to loopback and keeps Redis and RabbitMQ internal to Compose. Development realm users and credentials come from the local environment.

Production must use a dedicated Keycloak realm and HTTPS. Configure the confidential OIDC client, its PKCE redirect and logout URIs, the API audience mapper, and the web origin. Configure a private database connection and persist the ASP.NET Core Data Protection keys before using multiple API replicas. Back up and restore both the FlowOps and Keycloak databases. Schedule schema changes and test restores before upgrading Elsa.

The API and web Dockerfiles build independently using `docker build -t flowops-api:local backend` and `docker build -t flowops-web:local apps/web`. Set the web image build argument `FLOWOPS_API_ORIGIN` to the internal API URL and use the same value at runtime. Configure the API with `ConnectionStrings__FlowOps`, `Auth__Authority`, `Auth__ClientSecret`, and `Auth__WebOrigin`. Route public HTTPS traffic to the web container and keep the API private.

The GitHub Actions workflow defines build, audit, and browser checks. Hosted CI execution, distributed session key storage, and production rollout validation are tracked in TASKS.md.
