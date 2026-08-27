# Развёртывание на IIS

## Где хранятся секреты

Рабочая конфигурация не должна находиться в репозитории, `appsettings.json`,
`web.config` или каталоге публикации. В окружении `Production` приложение требует
переменную `SURVEY_CONFIG_PATH` с абсолютным путём к внешнему JSON-файлу.

Рекомендуемая структура на сервере:

```text
C:\ProgramData\AIS-Anketirovanie\
  Config\server-config.json
  DataProtection-Keys\
  ProductionCalendar\
```

Пример `C:\ProgramData\AIS-Anketirovanie\Config\server-config.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=DB-SERVER;Port=5432;Database=surveys;Username=survey_app;Password=<DB_PASSWORD>;SSL Mode=Require"
  },
  "DataProtection": {
    "KeysPath": "C:\\ProgramData\\AIS-Anketirovanie\\DataProtection-Keys"
  },
  "ProductionCalendar": {
    "BaseUrl": "https://isdayoff.ru/",
    "DataPath": "C:\\ProgramData\\AIS-Anketirovanie\\ProductionCalendar",
    "RemoteDownloadEnabled": false
  }
}
```

При `RemoteDownloadEnabled=false` приложение работает без интернета и читает файлы
`{год}.txt` из `DataPath`. Подготовьте прошлый, текущий и следующий годы на компьютере
с интернетом:

```powershell
.\scripts\download-production-calendars.ps1 `
  -Years 2025,2026,2027 `
  -OutputPath C:\Temp\ProductionCalendar
```

Перенесите полученные файлы в
`C:\ProgramData\AIS-Anketirovanie\ProductionCalendar`. Если удалённая загрузка
включена, приложение сначала читает локальный файл, а при его отсутствии получает
год из внешнего сервиса и сохраняет результат в этот каталог.

Для PostgreSQL следует создать отдельного пользователя приложения с правами только
на рабочую БД. Учётную запись суперпользователя PostgreSQL использовать нельзя.

SMTP-профиль задаётся администратором внутри приложения. SMTP-пароль хранится в БД
в защищённом формате. Для его расшифровки и для постоянных сеансов необходимо
сохранять каталог `DataProtection-Keys` между публикациями и резервировать его вместе
с БД.

## Права на файлы

Пусть пул называется `AIS-Anketirovanie`. Выполнить PowerShell от администратора:

```powershell
$identity = 'IIS AppPool\AIS-Anketirovanie'
$root = 'C:\ProgramData\AIS-Anketirovanie'
$config = Join-Path $root 'Config\server-config.json'
$keys = Join-Path $root 'DataProtection-Keys'
$calendar = Join-Path $root 'ProductionCalendar'

New-Item -ItemType Directory -Force (Split-Path $config), $keys, $calendar | Out-Null

# После создания server-config.json ограничить доступ к нему и каталогам.
icacls $root /inheritance:r
icacls $root /grant:r 'BUILTIN\Administrators:(OI)(CI)(F)' 'NT AUTHORITY\SYSTEM:(OI)(CI)(F)'
icacls $root /grant:r "${identity}:(RX)"
icacls (Split-Path $config) /grant:r "${identity}:(RX)"
icacls $config /grant:r "${identity}:(R)"
icacls $keys /grant:r "${identity}:(OI)(CI)(M)"
icacls $calendar /grant:r "${identity}:(OI)(CI)(M)"
```

Файл конфигурации сначала создаётся администратором, затем ему выдаётся только право
чтения для пула. Каталогу ключей требуется право изменения. Пулу также нужны права
чтения и выполнения на каталог публикации, право изменения на каталог календарей и
право изменения на
`wwwroot\help_files`, потому что там сохраняются загруженные инструкции.

## Настройка пула IIS

1. Установить актуальный .NET 9 Hosting Bundle на IIS-сервер.
2. Создать отдельный пул `AIS-Anketirovanie` с `No Managed Code`.
3. Включить `Load User Profile = True`.
4. Добавить пулу две переменные окружения:

```text
ASPNETCORE_ENVIRONMENT=Production
SURVEY_CONFIG_PATH=C:\ProgramData\AIS-Anketirovanie\Config\server-config.json
```

Для IIS 10 их можно задать через Configuration Editor или `AppCmd.exe`:

```bat
%windir%\system32\inetsrv\AppCmd.exe set config -section:system.applicationHost/applicationPools /+"[name='AIS-Anketirovanie'].environmentVariables.[name='ASPNETCORE_ENVIRONMENT',value='Production']" /commit:apphost
%windir%\system32\inetsrv\AppCmd.exe set config -section:system.applicationHost/applicationPools /+"[name='AIS-Anketirovanie'].environmentVariables.[name='SURVEY_CONFIG_PATH',value='C:\ProgramData\AIS-Anketirovanie\Config\server-config.json']" /commit:apphost
```

Если переменная уже существует, изменить её в Configuration Editor вместо повторного
добавления. После изменения конфигурации перезапустить пул.

## Публикация

```powershell
dotnet publish .\main_project.csproj -c Release -r win-x64 --self-contained false -o C:\Deploy\AIS-Anketirovanie
```

До запуска новой публикации применить все отсутствующие миграции из корня исходного
проекта. Скрипт сам определяет уже установленные версии по таблице
`public.schema_migrations`:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\apply-migrations.ps1 `
  -Psql "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
  -ConnectionString "Host=DB-SERVER;Port=5432;Database=surveys;Username=survey_app;Password=<DB_PASSWORD>;SSL Mode=Require"
```

После выполнения проверить последние версии:

```sql
SELECT version, name, applied_at
FROM public.schema_migrations
ORDER BY version DESC
LIMIT 10;
```

Для редакции от 27.08.2026 последняя миграция —
`050_add_planned_survey_templates`. В последующих поставках ориентироваться нужно на
актуальный состав `db\migrations\000_apply_all.sql`, а не на зафиксированный здесь номер.

Содержимое каталога публикации переносится в физический каталог сайта. .NET Web SDK
сам создаёт корректный `web.config`; он должен оставаться в корне сайта. В него нельзя
добавлять строку подключения или другие секреты.

На IIS должен быть настроен HTTPS: в Production cookie авторизации передаётся только по
защищённому соединению. После публикации проверить вход, сохранение сессии после
перезапуска пула, загрузку инструкции и отправку тестового письма.

## Локальная разработка

Локальная строка подключения хранится в .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=surveys;Username=<DB_USER>;Password=<DB_PASSWORD>"
```

User Secrets предназначен только для разработки. Рабочие секреты IIS в него не
записываются.
