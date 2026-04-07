# Recovery Notes

В репозитории нет миграций, `.sql`-дампов и файла с подключением к рабочей БД. Поэтому из проекта можно надёжно восстановить:

- структуру таблиц;
- индексы и один compatibility-view `public.log`;
- временного администратора для входа в приложение;
- только отдельные фрагменты данных из логов в `wwwroot/dumps`.

Нельзя гарантированно восстановить из кода:

- реальные анкеты и их JSON-вопросы;
- ответы организаций;
- исходных пользователей и пароли;
- полные справочники `omsu`.

## Что уже подготовлено

- `recovery/reconstruct_schema.sql`: схема PostgreSQL, собранная по SQL из контроллеров.
- `recovery/seed_temp_admin.sql`: временный админ `admin / TempAdmin12345!`.
- `recovery/recovered_hints.md`: подсказки, вытащенные из `wwwroot/dumps`.

## Как поднять пустую восстановленную БД локально

1. Создай новую БД:

```bash
createdb survey_recovered
```

2. Примени схему:

```bash
psql -d survey_recovered -f recovery/reconstruct_schema.sql
```

3. Добавь временного администратора:

```bash
psql -d survey_recovered -f recovery/seed_temp_admin.sql
```

4. Пропиши строку подключения в `appsettings.json` или user-secrets, например:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=survey_recovered;Username=dbusyrev"
  }
}
```

Если у локального Postgres нужен пароль, добавь `Password=...`.

## Что можно восстановить вручную дополнительно

- Из `wwwroot/dumps/*.txt` видны отдельные ID анкет и некоторые названия.
- В логах есть как минимум пользователь `Гордеев_СВ`.
- В `wwwroot/dumps/logs_dump_20250425_062004.txt` есть несколько названий организаций и их email.

Это лучше использовать как подсказки для ручного заполнения, а не как точный источник данных.

## Неочевидные моменты схемы

- `history_answer.id_survey` не связан внешним ключом, потому что код читает ответы и для `surveys`, и для `history_surveys`.
- `SurveyController.LogSurveyCreation()` пишет в таблицу `log`, а остальной код в `logs`. Поэтому в схеме создан updatable view `public.log -> public.logs`.
- `users.id_omsu` сделан nullable, потому что `SurveyExpirationService` выставляет `id_omsu = NULL` у просроченных организаций.
