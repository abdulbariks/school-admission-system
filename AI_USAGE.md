# AI usage disclosure

- **Tool:** ChatGPT (GPT-6).
- **Used for:** Interpreting the assignment, proposing the project structure, drafting the .NET minimal API and PostgreSQL data model, designing transactional seat allocation, preparing the React interface, Docker configuration, documentation, and automated test scaffolding.
- **Potential AI mistakes checked:** The assignment's seat limit is a concurrency invariant, not just a UI validation. The implementation uses a PostgreSQL row lock inside a transaction rather than relying on client-side seat counts. Confirmation is state-based and idempotent. The frontend reads the backend API rather than calculating authoritative seat availability itself.
- **Verification status:** This execution environment does not have the .NET SDK or Docker installed, so the .NET build, PostgreSQL integration tests, and Docker Compose startup could not be executed here. These must be run in a Docker/.NET-capable environment before submission. Frontend dependencies/build also require npm registry access.
