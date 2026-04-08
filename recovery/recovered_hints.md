# Recovered Hints From Logs

Ниже не точный дамп, а только то, что удалось извлечь из логов в `wwwroot/dumps`.

## Пользователи

- В логах многократно встречается администратор `Гордеев_СВ`.

## Анкеты

- `id_survey = 62`: 11.12.2024 анкета была переименована с `Survey A` в `Survey B`.
- `id_survey = 789`: 17.05.2023 анкета была переименована с `Опрос 1` в `Опрос 2023`.
- `id_survey = 790`: 18.05.2023 анкета была выдана на прохождение.
- `id_survey = 791`: 20.05.2023 анкета ушла в архив из-за истечения срока.
- Из логов архивации видны также ID анкет: `50`, `60`, `96-112`, `113-114`, `124-127`.

## Организации

Из `wwwroot/dumps/logs_dump_20250425_062004.txt` удалось вытащить такие соответствия:

- `organization_id = 34`: `sdgffhfg`, email `hlebopashev.anton@yandex.ru`
- `organization_id = 35`: `fdghfghf`, email `hlebopashev.anton@yandex.ru`
- `organization_id = 37`: `КУКУ`, email `hlebopashev.anton@yandex.ru`
- `organization_id = 38`: `sdgfddfghfhf`, email `hlebopashev.anton@yandex.ru`
- `organization_id = 39`: `уарпарапр`, email `hlebopashev.anton@yandex.ru`

## Ограничения

- В большинстве логов названия анкет уже потеряны и отображаются как `Нет данных`.
- JSON с вопросами анкет и JSON с ответами из логов не восстанавливаются.
- Пароли пользователей из логов не извлекаются.
