# PostgreSQL integration tests

Integration tests are opt-in. They only run when `SURVEY_TEST_CONNECTION` points to a dedicated PostgreSQL database whose name contains `test`.

Each test drops and recreates only the `public` schema in that database, then applies `db/migrations/000_apply_all.sql` with `psql`.

```bash
export SURVEY_TEST_CONNECTION='Host=/tmp;Port=5432;Database=survey_contract_tests;Username=dbusyrev'
export SURVEY_TEST_PSQL=/opt/homebrew/opt/postgresql@18/bin/psql
dotnet test Tests/Unit/MainProject.Tests/MainProject.Tests.csproj --filter FullyQualifiedName~Integration
```
