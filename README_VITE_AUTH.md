Первый этап Vite подключён только для страницы авторизации.

Как запускать локально:
1. npm install
2. npm run dev
3. dotnet run

Важно: npm run dev и dotnet run запускаются в двух разных терминалах.

Что уже переведено:
- Views/Auth/auth.cshtml
- src/entries/auth.jsx

Что осталось по-старому:
- get_surveys.cshtml
- survey_list_user.cshtml
- прочие страницы

Для production:
1. npm install
2. npm run build
3. dotnet publish

Vite собирает auth.js в wwwroot/dist/auth.js без хеша, чтобы не нужен был manifest.
