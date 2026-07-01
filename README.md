## Event Manager API

Базовый REST API для управления мероприятиями, реализованный на ASP.NET Core Web API.

## Требования
- .NET 10 SDK или выше: [Скачать](https://dotnet.microsoft.com/download)
- Git

## Установка и запуск

```bash
# 1. Клонируйте репозиторий
git clone https://github.com/ddamage2389/AspNetApi
cd EventManager

# 2. Соберите проект
dotnet build

# 3. Запустите сервер
dotnet run

# 4. Откройте Swagger UI
https://localhost:xxxx/swagger

---

## Краткая документация API

| Метод | Эндпоинт | Описание | Статусы |
|-------|----------|----------|---------|
| `GET` | `/api/events` | Получить все события | `200` |
| `GET` | `/api/events/{id}` | Получить событие по ID | `200`, `404` |
| `POST` | `/api/events` | Создать событие | `201`, `400` |
| `PUT` | `/api/events/{id}` | Обновить событие | `200`, `400`, `404` |
| `DELETE` | `/api/events/{id}` | Удалить событие | `204`, `404` |

## Правила валидации
- `title`, `startAt`, `endAt` — обязательные поля
- `endAt` должен быть позже `startAt`

Полная интерактивная документация: [Swagger UI](https://localhost:xxxx/swagger)