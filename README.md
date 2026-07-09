# DinoAI

DinoAI — локальный ИИ-агент для разработки на C#/.NET. Проект вдохновлён OpenCode, но строится как приложение, нативное для .NET, с Blazor-интерфейсом, локальными инструментами рабочей папки, сессиями, разрешениями и подключаемыми API, совместимыми с OpenAI.

## Что уже готово

- Blazor-интерфейс на русском языке.
- Список сессий и сохранение истории сообщений.
- Панель рабочей папки `D:\DinoAI`.
- Локальные команды агента: `/workspace`, `/files`, `/read`, `/status`, `/diff`, `/build`.
- Ручной запуск инструментов из интерфейса.
- Настройки ИИ-модели через интерфейс: базовый адрес, модель и ключ API.
- Поддержка API, совместимых с OpenAI: Gemini, Groq, OpenRouter, DeepSeek и другие совместимые провайдеры.
- Git-репозиторий: https://github.com/NikitaDobrodski/DinoAI

## Структура проекта

- `src/DinoAI.Core` — доменная логика агента, сессии, инструменты, разрешения, настройки и провайдеры модели.
- `src/DinoAI.Server` — локальный ASP.NET Core API для клиентов агента.
- `src/DinoAI.Web` — Blazor-интерфейс для чата, сессий, настроек модели, рабочей папки и ручного запуска инструментов.
- `src/DinoAI.Cli` — консольный вход для проверки команд, инструментов и сценариев агента.

## Запуск

Из папки проекта:

```powershell
cd D:\DinoAI
dotnet run --project src/DinoAI.Web --urls http://127.0.0.1:5088
```

После запуска интерфейс доступен по адресу:

```text
http://127.0.0.1:5088/
```

## Настройка модели

В интерфейсе есть блок `Модель`.

Для совместимого API нужно заполнить:

- `Базовый адрес` — адрес API, совместимого с OpenAI.
- `Модель` — имя модели у выбранного провайдера.
- `Ключ API` — ключ доступа.

После заполнения:

1. Нажать `Сохранить настройки`.
2. Нажать `Проверить подключение`.
3. Если проверка успешна, обычные сообщения в чат будут отправляться в модель.

Пример для Google Gemini:

```text
Базовый адрес: https://generativelanguage.googleapis.com/v1beta/openai/
Модель: gemini-3.5-flash
```

Пример для Groq:

```text
Базовый адрес: https://api.groq.com/openai/v1
Модель: qwen/qwen3-32b
```

Быстрая настройка Groq через CLI:

```powershell
dotnet run --project src/DinoAI.Cli -- model groq apiKey=<твой_groq_api_key>
```

Можно не передавать ключ в команду, если он уже есть в окружении:

```powershell
$env:GROQ_API_KEY="<твой_groq_api_key>"
dotnet run --project src/DinoAI.Cli -- model groq
```

Пример для OpenRouter:

```text
Базовый адрес: https://openrouter.ai/api/v1
Модель: выбрать совместимую модель на стороне OpenRouter
```

Пример для DeepSeek:

```text
Базовый адрес: https://api.deepseek.com/v1
Модель: deepseek-chat
```

Важно: бесплатный чат на сайте провайдера не всегда означает бесплатный API. Если провайдер возвращает `402 Payment Required`, значит запрос дошёл до API, но на аккаунте нет доступного баланса или бесплатного лимита.

## Команды в чате

Команды ниже выполняются локально и не требуют ИИ-модели:

```text
/workspace
/files *.csproj
/files *.razor
/files *.cs
/read README.md
/status
/diff
/build
```

Примеры:

```text
/files *.csproj
/read src/DinoAI.Web/Components/Pages/Home.razor
/build
```

Если сообщение не похоже на локальную команду, DinoAI отправляет его в настроенную модель.
Если модель поддерживает function calling, DinoAI передаёт ей доступные workspace/shell tools, выполняет запрошенные tool calls локально и возвращает результат модели для финального ответа.

## Инструменты агента

Сейчас доступны инструменты:

- `workspace.describe` — показать структуру рабочей папки.
- `workspace.find_files` — найти файлы по шаблону.
- `workspace.read_file` — прочитать файл внутри рабочей папки.
- `workspace.write_file` — записать файл внутри рабочей папки, только с явным разрешением.
- `shell.run` — выполнить разрешённую команду оболочки.

Для инструмента `shell.run` без подтверждения разрешены только безопасные команды:

- `dotnet build`
- `dotnet test`
- `dotnet --info`
- `git status`
- `git diff`
- `git log`

Остальные команды требуют явного подтверждения.

## CLI-примеры

Интерактивный терминальный вход:

```powershell
.\dino.ps1
```

В CMD можно запускать так:

```cmd
dino
dino /workspace
dino "посмотри проект"
```

Если CMD уже был открыт до настройки PATH, закрой его и открой заново.

Чтобы запускать DinoAI как `dino` из любого нового PowerShell, добавь функцию в профиль:

```powershell
if (!(Test-Path $PROFILE)) { New-Item -ItemType File -Force -Path $PROFILE | Out-Null }
Add-Content $PROFILE 'function dino { & "D:\DinoAI\dino.ps1" @args }'
```

После этого открой новый PowerShell:

```powershell
dino
dino /workspace
dino "посмотри проект и скажи, какие файлы открыть первыми"
```

Внутри интерактивного режима доступны локальные команды без расхода токенов Groq:

```text
/help
/models
/workspace
/files *.cs
/read README.md
/clear
/exit
```

Показать рабочую папку:

```powershell
dotnet run --project src/DinoAI.Cli -- workspace D:\DinoAI
```

Найти проекты:

```powershell
dotnet run --project src/DinoAI.Cli -- files D:\DinoAI *.csproj
```

Прочитать файл:

```powershell
dotnet run --project src/DinoAI.Cli -- read D:\DinoAI README.md
```

Запустить команду агента:

```powershell
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI /workspace
```

Показать список инструментов:

```powershell
dotnet run --project src/DinoAI.Cli -- tools
```

Проверить текущую модель:

```powershell
dotnet run --project src/DinoAI.Cli -- model status
```

Сохранить настройки Groq:

```powershell
dotnet run --project src/DinoAI.Cli -- model groq apiKey=<твой_groq_api_key>
```

Запустить инструмент вручную:

```powershell
dotnet run --project src/DinoAI.Cli -- tool workspace.find_files D:\DinoAI pattern=*.csproj maxResults=20
```

## Локальные данные

DinoAI хранит локальные данные времени работы в папке:

```text
D:\DinoAI\.dinoai
```

Файлы:

- `sessions.json` — история сессий и сообщений.
- `model-settings.json` — настройки провайдера модели.

Папка `.dinoai/` является локальным состоянием и игнорируется git.

## Текущая цель MVP

Ближайшая цель — сделать полезный цикл локального агента разработки:

1. Пользователь пишет задачу.
2. Агент читает и ищет файлы проекта.
3. Агент предлагает действия или изменения.
4. Пользователь подтверждает опасные операции.
5. Агент выполняет команды и проверки.
6. Пользователь видит результат, различия и историю действий.

## Следующие шаги

- Улучшить статус модели: `ключ задан`, `подключение проверено`, `ошибка ключа`, `нет баланса`, `модель не найдена`.
- Добавить более безопасное хранение ключа API.
- Научить модель запрашивать инструменты через понятный план действий.
- Сделать подтверждение вызовов инструментов прямо в чате.
- Добавить просмотр различий и применение патчей из интерфейса.



