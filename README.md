# ProjectManagement

Тестовое задание для вакансии **C# (ASP.NET) Developer** в Sibers — приложение
для ведения проектов, сотрудников, задач и документов с двумя альтернативными
презентациями (Vue 3 SPA + Web API и Razor MVC), Identity-авторизацией по
трём ролям, EF Core Code-First с миграциями и поддержкой SQLite/MSSQL.

---

## Содержание

- [Стек и версии](#стек-и-версии)
- [Что реализовано](#что-реализовано)
- [Архитектура](#архитектура)
- [Структура репозитория](#структура-репозитория)
- [Быстрый старт через Docker](#быстрый-старт-через-docker)
- [Локальный запуск без Docker](#локальный-запуск-без-docker)
- [Демо-аккаунты](#демо-аккаунты)
- [Тесты](#тесты)
- [Миграции](#миграции)
- [Конфигурация](#конфигурация)
- [Замечания и известные ограничения](#замечания-и-известные-ограничения)

---

## Стек и версии

| Компонент | Версия |
|---|---|
| .NET SDK | **10.0** (`net10.0`, требование ТЗ было «минимум .NET 7») |
| ASP.NET Core | 10.0 (`Microsoft.AspNetCore.App`) |
| Entity Framework Core | 10.0.8 (Code-First + миграции) |
| EF Core providers | `Sqlite` 10.0.8, `SqlServer` 10.0.8 |
| ASP.NET Core Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.8 |
| MediatR | 14.1.0 |
| JWT bearer | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.8 |
| Swashbuckle (Swagger) | 10.1.7 |
| Vue | 3.5 |
| Vue Router | 4.4 |
| Pinia | 2.2 |
| Axios | 1.7 |
| Vite | 5.4 |
| TypeScript | 5.5 |
| xUnit v3 | 3.2.2 |
| NSubstitute | 5.3.0 |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.6 |
| EF Core CLI tool | `dotnet-ef` 10.0.8 (закреплён в `dotnet-tools.json`) |

---

## Что реализовано

### Обязательная часть (Backend + Frontend)

- ✅ Трёхслойная архитектура: **DataAccess → BusinessLogic → Presentation**.
- ✅ Хранилище: **SQLite** (по умолчанию) или **MSSQL** — переключается одной
  переменной `Database__Provider`.
- ✅ CRUD проектов: `POST/GET/PUT/DELETE` + назначение/снятие сотрудника (M:N).
- ✅ CRUD сотрудников.
- ✅ Фильтрация проектов: по диапазону `StartDate`/`EndDate`, диапазону
  приоритета, по PM, по участнику, по подстроке имени.
- ✅ Сортировка по `Name | StartDate | EndDate | Priority` + tie-break по `Id`,
  чтобы строки не «прыгали» между страницами.
- ✅ Пагинация (`PagedResult<T>`).
- ✅ **Две презентации** (ТЗ требовало одну на выбор):
  - **Vue 3 SPA + Web API** (`vue-client/` + `src/ProjectManagement.Api`)
  - **Razor MVC** (`src/ProjectManagement.Web`)
- ✅ Визард создания проекта на **5 шагов**:
  1. Название/даты/приоритет
  2. Заказчик/исполнитель
  3. Руководитель проекта
  4. Команда проекта
  5. Документы проекта
- ✅ AJAX-typeahead на шагах 3 и 4 с дебаунсом 220 мс (`AutocompleteInput.vue`
  и `wwwroot/js/employee-autocomplete.js`).
- ✅ HTML5 drag-&-drop аплоадер документов (`FileDropZone.vue` /
  `wizard.js`), лимит **50 MB / файл**.

### Доп. задание 1 — Сущность «Задача»

- ✅ Сущность `ProjectTask` через EF Code-First Migrations
  (`20260522183629_AddProjectTasks`).
- ✅ CRUD задач, привязка к проекту (1:N), `Author`/`Assignee` (FK на
  `Employee`), enum `ProjectTaskStatus { ToDo, InProgress, Done }`.
- ✅ Доменный инвариант: исполнитель задачи должен быть участником проекта
  (PM или член команды), иначе `DomainValidationException`.
- ✅ Фильтрация по статусу/приоритету/имени/автору/исполнителю; сортировка по
  `Name | Priority | Status`.

### Доп. задание 2 — Identity и роли

- ✅ ASP.NET Core Identity (`IdentityDbContext<ApplicationUser>`).
- ✅ Три роли: `Director` (Руководитель), `ProjectManager`,
  `Employee` (`BusinessLogic/Identity/Roles.cs`).
- ✅ **Cookie-аутентификация** для Razor MVC, **JWT bearer** для Vue/API.
- ✅ Идемпотентный сидинг ролей и демо-аккаунтов на старте
  (`DataAccess/Identity/IdentitySeeder.cs`).
- ✅ Авторизационные правила для **Проектов** реализованы и в Web, и в Api:
  - **Director** — полный доступ.
  - **ProjectManager** — видит и редактирует только проекты, где он PM;
    может назначать/снимать участников, грузить документы.
  - **Employee** — read-only, только проекты, где он участник.
- ✅ Фильтрация на уровне query, а не view — запрещённые проекты в SPA даже
  не «мигают».
- ⚠️ **Известное ограничение по доп. 2**: авторизация по сущности
  **Task** реализована не полностью — см.
  [замечания](#замечания-и-известные-ограничения).

---

## Архитектура

Чистая трёхслойка + Vertical Slices внутри `BusinessLogic` (одна папка на
фичу: `Projects/`, `Tasks/`, `Employees/`, `Documents/`, `Identity/`).
Контроллеры/Views ничего не знают про EF — общаются с предметкой через
**MediatR** (Command/Query handlers).

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation                                               │
│  ┌──────────────────────┐    ┌──────────────────────────┐   │
│  │ ProjectManagement.Api│    │ ProjectManagement.Web    │   │
│  │  (Web API + JWT)     │    │  (Razor MVC + Cookies)   │   │
│  └──────────┬───────────┘    └──────────┬───────────────┘   │
│             │                           │                   │
│             └───────────┬───────────────┘                   │
│                         │ MediatR                           │
└─────────────────────────┼───────────────────────────────────┘
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  BusinessLogic                                              │
│  Domain entities (Project, Employee, ProjectTask,           │
│  ProjectDocument) + DomainGuard + Commands/Queries +        │
│  Repository interfaces + Identity contracts.                │
└─────────────────────────┬───────────────────────────────────┘
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  DataAccess                                                 │
│  AppDbContext (IdentityDbContext<ApplicationUser>),         │
│  EF Core Fluent configurations, Repositories,               │
│  Migrations, LocalFileStorage, IdentitySeeder.              │
└─────────────────────────────────────────────────────────────┘
```

**Frontend (Vue SPA):** Vue 3 + `<script setup lang="ts">`, Pinia для auth,
Vue Router, Axios; Vite-прокси на `/api` ходит на ASP.NET по HTTPS, чтобы
не терять заголовок `Authorization` при HTTP→HTTPS-редиректе.

**Ключевые доменные инварианты** (живут на сущностях, а не «размазаны»
по хендлерам):
- Проект и его PM не могут быть пустыми; PM не может одновременно быть
  обычным участником (`Project.AddEmployee` проверяет это).
- Исполнитель задачи обязан быть членом проекта (`ProjectTask.AssignWorker`).
- Все строки валидируются через единый `DomainGuard` (`NotBlank`,
  `OptionalText`, `DateRange`, `NonNegative`).
- Доменные ошибки бросаются `DomainValidationException` и
  `EntityNotFoundException`, конвертируются в HTTP-ответы единым
  `DomainExceptionHandler` (Api → ProblemDetails) и
  `ModelState.AddModelError` (Web).

---

## Структура репозитория

```
ProjectManagement/
├── src/
│   ├── BusinessLogic/           # доменные сущности, MediatR-хендлеры, интерфейсы репозиториев
│   ├── DataAccess/              # AppDbContext, EF-конфигурации, миграции, репозитории, Identity-сидер, LocalFileStorage
│   ├── ProjectManagement.Api/   # Web API (JWT) — серверный бэк для Vue SPA
│   └── ProjectManagement.Web/   # Razor MVC + Cookies — самостоятельный фронт
├── vue-client/                  # Vue 3 + Vite SPA
├── Tests/
│   ├── Tests.BusinessLogic/     # юнит-тесты домена и хендлеров (NSubstitute + xUnit)
│   ├── Tests.DataAccess/        # тесты репозиториев на in-memory SQLite
│   ├── Tests.Presentation/      # E2E API через WebApplicationFactory + JWT
│   └── Tests.Presentation.Web/  # E2E Razor через WebApplicationFactory + Cookies
├── docker/                      # docker/README.md
├── docker-compose.yml           # профили vue | razor × sqlite | mssql
├── .env.example                 # шаблон env для docker-compose
└── ProjectManagement.slnx       # SLNX-формат, открывается в VS 2022+/Rider
```

---

## Быстрый старт через Docker

Требуется **Docker Desktop ≥ 4.x** (Compose v2).

```bash
cp .env.example .env             # подправь порты/строку подключения, если нужно
docker compose --profile vue up --build
```

Откроется:
- Vue SPA: <http://localhost:5173>
- API (для Swagger в Development): <http://localhost:5266/swagger>

Альтернативные комбинации:

| Команда | Фронт | БД |
|---|---|---|
| `docker compose --profile vue up --build` | Vue 3 SPA | SQLite |
| `docker compose --profile vue --profile mssql up --build` | Vue 3 SPA | MSSQL 2022 |
| `docker compose --profile razor up --build` | Razor MVC | SQLite |
| `docker compose --profile razor --profile mssql up --build` | Razor MVC | MSSQL 2022 |

Полная матрица и пояснения — в [`docker/README.md`](docker/README.md).

Для MSSQL нужно раскомментировать соответствующий блок в `.env`.

---

## Локальный запуск без Docker

### Предварительные требования

- **.NET SDK 10.0** (`dotnet --version` должен показать `10.0.x`)
- **Node.js ≥ 20** + npm (только для Vue-клиента)
- (опционально) MSSQL — по умолчанию используется SQLite-файл

### 1. Восстановить инструменты и зависимости

```bash
dotnet tool restore           # ставит dotnet-ef локально из dotnet-tools.json
dotnet restore
```

### 2. Вариант A — Vue SPA + Web API

В двух терминалах:

```bash
# Терминал 1 — API
dotnet run --project src/ProjectManagement.Api --launch-profile https
# → http://localhost:5266  и  https://localhost:7283
# → Swagger: https://localhost:7283/swagger

# Терминал 2 — Vue dev-server
cd vue-client
npm install
npm run dev
# → http://localhost:5173  (Vite-прокси /api → https://localhost:7283)
```

Если ASP.NET ругается на dev-сертификат:

```bash
dotnet dev-certs https --trust
```

### 3. Вариант B — Razor MVC (всё в одном процессе)

```bash
dotnet run --project src/ProjectManagement.Web --launch-profile https
# → https://localhost:7100
```

При первом запуске любой презентации:

1. Создаётся файл `projectmanagement.db` (SQLite) в каталоге запуска.
2. Применяются миграции (`Database.Migrate()` в `Program.cs`).
3. Запускается `IdentitySeeder` — создаёт роли и три демо-аккаунта.

---

## Демо-аккаунты

Создаются сидером при первом старте. Логины/пароли заданы в
`appsettings.json` (`Identity:Seed:Accounts`):

| Email | Пароль | Роль |
|---|---|---|
| `admin@local` | `Admin#12345` | Director |
| `pm@local` | `Pm#12345` | ProjectManager |
| `employee@local` | `Emp#12345` | Employee |

> ⚠️ Это demo-секреты для локального запуска. Перед публичным деплоем
> поменяйте `Identity:Seed:Accounts` и `Jwt:Key` (≥ 32 символов) через
> user-secrets / env-переменные.

---

## Тесты

```bash
dotnet test
```

В репозитории 4 тестовых проекта, **370 тестов** в сумме:

| Проект | Что тестирует | Кол-во |
|---|---|---:|
| `Tests.BusinessLogic` | Доменные сущности (`Project`, `Employee`, `ProjectTask`, `ProjectDocument`), все Command/Query-хендлеры через NSubstitute | 222 |
| `Tests.DataAccess` | Репозитории на in-memory SQLite (фильтры, сортировка, пагинация, удаление с каскадом) | 61 |
| `Tests.Presentation` | E2E для Web API через `WebApplicationFactory<Program>` с тестовой JWT-схемой | 50 |
| `Tests.Presentation.Web` | E2E для Razor MVC через `WebApplicationFactory<Program>` с тестовой cookie-схемой | 37 |

E2E-тесты гоняют живой пайплайн (контроллер → MediatR → EF → SQLite) на
изолированной БД per-test через подмену провайдера в
`ConfigureTestServices`.

---

## Миграции

```bash
# Добавить новую миграцию
dotnet ef migrations add MyChange --project src/DataAccess --startup-project src/ProjectManagement.Api

# Применить вручную (обычно не нужно — на старте вызывается Database.Migrate())
dotnet ef database update --project src/DataAccess --startup-project src/ProjectManagement.Api
```

В репозитории уже есть 6 миграций — от `InitialCreate` до
`FinalMergeSnapshot`.

> Миграции сгенерированы под SQLite. Для MSSQL они проходят, но при
> экзотических сценариях возможно понадобится перегенерировать их с
> указанием `Database__Provider=SqlServer` и валидной строкой подключения.

---

## Конфигурация

Всё конфигурируется через `appsettings.json` + переменные окружения
(стандартный ASP.NET Core конвейер). Ключевые секции:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=projectmanagement.db"
  },
  "Database": { "Provider": "Sqlite" },          // "Sqlite" | "SqlServer"
  "FileStorage": { "BasePath": "uploads" },      // абсолютный или относительный путь
  "Jwt": {                                       // только для Api
    "Issuer": "ProjectManagement.Api",
    "Audience": "ProjectManagement.Vue",
    "Key": "dev-only-secret-please-replace-in-production-32+chars",
    "LifetimeHours": 8
  },
  "Identity": {
    "Seed": {
      "Accounts": [ /* см. демо-аккаунты выше */ ]
    }
  }
}
```

В docker-compose эти значения подаются через переменные окружения с
двойным подчёркиванием:

```
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=Server=mssql,1433;Database=ProjectManagement;User Id=sa;Password=...;TrustServerCertificate=True
FileStorage__BasePath=/data/uploads
```

---

## Замечания и известные ограничения

- 🟡 **Email сотрудника живёт на `ApplicationUser`, а не на доменной
  сущности `Employee`.** Так Identity-слой целиком владеет учётными
  данными, а доменный `Employee` остаётся чистым. Снаружи поле
  по-прежнему отдаётся в `EmployeeDto.Email` через join, так что
  функционально требование ТЗ закрыто.
- 🟠 **Авторизация по задачам реализована не полностью.** Для проектов
  правила трёх ролей разведены (см. `ProjectsController.CanView/CanManage`
  в обоих презентациях), а `TasksController` (Api/Web) сейчас защищён
  только глобальной политикой «требуется аутентификация». Это не закрывает
  букву ТЗ для роли `Employee`/`ProjectManager`. Если нужно — добавляется
  декларативно через `[Authorize(Roles = …)]` + ресурсная проверка
  «AssigneeId == currentUser.EmployeeId» / «Project.ProjectManagerId ==
  currentUser.EmployeeId».
- ⚪ Папка `src/Presentation/` — пустой остаток от ранней итерации
  (есть только `.Backup.tmp` и SQLite-журнал, всё игнорируется git).
  Удаляется без последствий.
