# Эксплуатация

## Запуск

Приложение рассчитано на внутреннюю сеть и небольшое число пользователей. Для запуска нужны приложение, PostgreSQL и применённые миграции.

Data Protection по умолчанию хранит ключи в локальном каталоге `data-protection-keys` рядом с приложением. При необходимости путь можно переопределить:

```bash
export DataProtection__KeyRingPath='/var/lib/ais-anketirovanie/data-protection-keys'
```

Пароль SMTP защищается тем же Data Protection key ring в таблице `email_config`. Если удалить или заменить каталог ключей, сохранённый SMTP-пароль нужно будет ввести заново.

Логи приложения пишутся в stdout одной JSON-записью на событие. Запись завершённого HTTP-запроса содержит `TraceId`, метод, путь, IP клиента, статус и длительность.

## Резервное копирование PostgreSQL

`scripts/backup-postgres.sh` создаёт custom dump, шифрует его через GnuPG AES-256 и удаляет архивы старше заданного retention. В открытом виде dump на диск не записывается.

Для сервера нужны PostgreSQL client tools (`pg_dump`, `pg_restore`, `psql`, `createdb`, `dropdb`) и `gpg`. Подготовьте каталог и секреты с правами только для сервисного пользователя:

```bash
install -d -o ais-anketirovanie -g ais-anketirovanie -m 0700 /etc/ais-anketirovanie /var/lib/ais-anketirovanie/gnupg /var/backups/ais-anketirovanie
openssl rand -base64 48 > /etc/ais-anketirovanie/backup-passphrase
chown ais-anketirovanie:ais-anketirovanie /etc/ais-anketirovanie/backup-passphrase
chmod 600 /etc/ais-anketirovanie/backup-passphrase
cp deploy/backup.env.example /etc/ais-anketirovanie/backup.env
chown ais-anketirovanie:ais-anketirovanie /etc/ais-anketirovanie/backup.env
chmod 600 /etc/ais-anketirovanie/backup.env
cp deploy/postgresql_service.conf.example /etc/ais-anketirovanie/postgresql_service.conf
cp deploy/postgresql.pass.example /etc/ais-anketirovanie/postgresql.pass
chown ais-anketirovanie:ais-anketirovanie /etc/ais-anketirovanie/postgresql_service.conf /etc/ais-anketirovanie/postgresql.pass
chmod 600 /etc/ais-anketirovanie/postgresql_service.conf /etc/ais-anketirovanie/postgresql.pass
```

В `/etc/ais-anketirovanie/backup.env` задаются:

- `SURVEY_BACKUP_CONNECTION` — соединение с исходной БД;
- `SURVEY_BACKUP_DIRECTORY` — отдельный каталог резервных копий;
- `SURVEY_BACKUP_ENCRYPTION_PASSPHRASE_FILE` — файл с GPG passphrase;
- `SURVEY_BACKUP_RETENTION_DAYS` — срок хранения в днях, по умолчанию `30`;
- `SURVEY_RESTORE_VERIFY_*` — отдельная БД, используемая только для проверки восстановления. Её имя обязано содержать `restore_verify`.

Не храните пароль PostgreSQL в `backup.env`: пример использует `PGSERVICEFILE` и `PGPASSFILE`. Заполните значения в `postgresql_service.conf` и `postgresql.pass` по шаблонам в `deploy/`; оба файла должны принадлежать сервисному пользователю и иметь режим `0600`.

Каталог резервных копий должен быть вынесен за пределы сервера приложения или синхронизирован в независимое объектное хранилище. Проверочный скрипт `scripts/verify-postgres-backup.sh` расшифровывает последний архив, восстанавливает его в изолированную БД, проверяет наличие миграций и всегда удаляет эту БД после завершения.

Для systemd установите unit-файлы и включите расписание:

```bash
cp deploy/systemd/ais-anketirovanie-*.service /etc/systemd/system/
cp deploy/systemd/ais-anketirovanie-*.timer /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now ais-anketirovanie-backup.timer
systemctl enable --now ais-anketirovanie-restore-verify.timer
```

Backup запускается ежедневно в `02:15` с небольшим случайным сдвигом, проверка восстановления — по воскресеньям в `04:00`. Состояние проверяйте через `systemctl list-timers 'ais-anketirovanie-*'` и `journalctl -u ais-anketirovanie-backup.service`.

## Репетиция миграций

Скрипт копирует исходную базу в изолированную БД, применяет все миграции и выводит последнюю версию. Целевая база полностью пересоздаётся, а её имя обязано содержать `rehearsal`.

```bash
export SURVEY_SOURCE_CONNECTION='Host=db.example;Database=survey;Username=operator;Password=...'
export SURVEY_REHEARSAL_CONNECTION='Host=db.example;Database=survey_migration_rehearsal;Username=operator;Password=...'
export SURVEY_REHEARSAL_ADMIN_CONNECTION='Host=db.example;Database=postgres;Username=operator;Password=...'
export SURVEY_REHEARSAL_DATABASE='survey_migration_rehearsal'
bash scripts/rehearse-postgres-migrations.sh
```

Запускайте репетицию перед каждым production-развёртыванием миграций.

## Проверка планов чтения

После репетиции миграций измерьте read-сценарии на этой же изолированной копии. Скрипт выполняет `EXPLAIN ANALYZE` для первой страницы журнала событий, архивов администратора и клиента, а также обоих запросов отчётов. Все запросы запускаются внутри `BEGIN READ ONLY`; скрипт дополнительно проверяет, что имя целевой базы содержит `rehearsal`, `perf`, `benchmark` или `test` и совпадает с `current_database()`.

```bash
export SURVEY_EXPLAIN_CONNECTION="$SURVEY_REHEARSAL_CONNECTION"
export SURVEY_EXPLAIN_DATABASE="$SURVEY_REHEARSAL_DATABASE"
export SURVEY_EXPLAIN_LIMIT=10
bash scripts/explain-read-paths.sh | tee /var/log/ais-anketirovanie/read-plans-$(date +%F).txt
```

Сравнивайте план и буферы с предыдущей репетицией при том же объёме данных. Не фиксируйте CI на конкретный тип скана: PostgreSQL обоснованно выбирает `Seq Scan` на небольших таблицах. Регрессией являются рост `Execution Time`, чтений `shared read` или появление дорогой сортировки/полного прохода по большим таблицам там, где план на production-подобной копии раньше использовал индекс.
