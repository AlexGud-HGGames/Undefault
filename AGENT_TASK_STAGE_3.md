### 📍 Этап 3: Spotify Client Foundation

**Задача:** Реализовать базовый Spotify Web API клиент с OAuth авторизацией.

**Что нужно сделать:**

#### 1. Spotify API Client Интерфейсы

**Интерфейс `ISpotifyClient`** в Core (Core/Spotify/):
- Методы для управления воспроизведением:
  - `GetCurrentPlaybackAsync()` → текущее состояние (track, is_playing, volume)
  - `PlayAsync(uri?)` → начать воспроизведение (опционально URI трека/плейлиста)
  - `PauseAsync()` → пауза
  - `SetVolumeAsync(int volume)` → установка громкости (0-100)
  - `ResumeAsync()` → возобновление воспроизведения
- Методы для авторизации:
  - `IsAuthenticatedAsync()` → проверка наличия валидного токена
  - `GetAuthorizationUrlAsync()` → получение URL для OAuth авторизации
  - `AuthenticateAsync(string authorizationCode)` → обмен кода на токен

**Модели данных** (Core/Spotify/Models/):
- `PlaybackState` — текущее состояние воспроизведения
- `Track` — информация о треке
- `SpotifyAuthResult` — результат авторизации

**Требования:**
- Интерфейсы в Core (не зависит от UI/Host)
- Использовать async/await паттерн
- Обработка ошибок через исключения или Result pattern

**Предложи варианты:**
- Структуру `ISpotifyClient` интерфейса
- Модели данных (PlaybackState, Track и т.д.)
- Подход к ошибкам (exceptions или Result<T>)

---

#### 2. OAuth Flow (Authorization Code)

**Реализация OAuth 2.0 Authorization Code flow:**

1. **Генерация authorization URL:**
   - Spotify Client ID из конфигурации
   - Redirect URI (localhost для десктопного приложения)
   - Scopes: `user-modify-playback-state`, `user-read-playback-state`
   - State для защиты от CSRF

2. **Обмен authorization code на access token:**
   - POST запрос к Spotify Token endpoint
   - Client ID + Client Secret (из конфигурации)
   - Authorization code из redirect
   - Получение access_token и refresh_token

3. **Token refresh:**
   - Автоматический refresh при истечении access_token
   - Использование refresh_token для получения нового access_token

**Требования:**
- OAuth логика в Core (можно отдельный класс `SpotifyOAuthService`)
- Конфигурация через `SpotifyClientOptions` (ClientId, ClientSecret, RedirectUri)
- Безопасное хранение токенов (in-memory для MVP)

**Предложи варианты:**
- Структуру OAuth сервиса (отдельный класс или часть ISpotifyClient)
- Подход к хранению токенов (ITokenStorage интерфейс?)
- Конфигурацию через Options pattern

---

#### 3. HTTP Client Реализация

**Реализация `SpotifyClient`** (можно в отдельном проекте или в Core):
- Использование `HttpClient` для запросов к Spotify Web API
- Base URL: `https://api.spotify.com/v1`
- Автоматическое добавление `Authorization: Bearer {access_token}` заголовка
- Обработка HTTP ошибок (401 → refresh token, 403 → недостаточно прав, и т.д.)

**Требования:**
- HttpClient через DI (IHttpClientFactory)
- Сериализация/десериализация JSON (System.Text.Json)
- Retry логика для transient errors (опционально)

**Предложи варианты:**
- Где разместить реализацию (Core/Spotify/ или отдельный проект)
- Подход к HttpClient (IHttpClientFactory или прямой HttpClient)
- Обработка ошибок и retry логика

---

#### 4. Token Storage

**Интерфейс `ITokenStorage`** в Core:
- `GetAccessTokenAsync()` → получение access token
- `GetRefreshTokenAsync()` → получение refresh token
- `SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt)` → сохранение токенов
- `ClearTokensAsync()` → очистка токенов

**Реализация `InMemoryTokenStorage`** (для MVP):
- Хранение токенов в памяти
- Thread-safe (lock или ConcurrentDictionary)

**Требования:**
- Интерфейс в Core
- Реализация может быть в Core или отдельном проекте
- Подготовка к persistent storage (файл/БД) в будущем

**Предложи варианты:**
- Структуру ITokenStorage интерфейса
- Подход к thread-safety
- Где разместить реализацию

---

#### 5. Интеграция в DI

**Регистрация сервисов:**
- `ISpotifyClient` → `SpotifyClient`
- `ITokenStorage` → `InMemoryTokenStorage`
- `SpotifyOAuthService` (если отдельный класс)
- `HttpClient` для Spotify API
- `SpotifyClientOptions` из конфигурации

**Требования:**
- Регистрация в GsiHost (где будет использоваться) или в Core (если нужен везде)
- Конфигурация через appsettings.json или переменные окружения

**Предложи варианты:**
- Где регистрировать (GsiHost или Core)
- Подход к конфигурации (appsettings.json, environment variables)

---

**Definition of Done:**
- ✅ `ISpotifyClient` интерфейс реализован в Core
- ✅ OAuth Authorization Code flow работает
- ✅ Можно получить текущий playback state
- ✅ Можно управлять воспроизведением (play/pause/volume)
- ✅ Token refresh работает автоматически
- ✅ Токены хранятся в памяти (InMemoryTokenStorage)
- ✅ Сервисы зарегистрированы в DI
- ✅ Проект компилируется

---

### 📝 Формат ответа

После анализа и реализации, предоставь:

1. **Что уже было сделано** (если что-то нашёл)
2. **Что реализовал** (новый код)
3. **Предложенные варианты** (с обоснованием выбора):
   - Структура интерфейсов и классов
   - Подход к OAuth flow
   - Подход к token storage
   - Подход к HTTP клиенту
   - Подход к конфигурации
4. **Вопросы** (если критично)

**Важно:** 
- Предлагай варианты с trade-offs, используй best practices для .NET 8
- Используй Spotify Web API документацию: https://developer.spotify.com/documentation/web-api
- OAuth flow: https://developer.spotify.com/documentation/web-api/tutorials/code-flow
- Core должен оставаться независимым от UI/Host

---

### Справочная информация

**Spotify Web API Endpoints:**
- `GET /v1/me/player` — текущее состояние воспроизведения
- `PUT /v1/me/player/play` — начать воспроизведение
- `PUT /v1/me/player/pause` — пауза
- `PUT /v1/me/player/volume` — установка громкости

**OAuth Endpoints:**
- `GET https://accounts.spotify.com/authorize` — authorization URL
- `POST https://accounts.spotify.com/api/token` — получение/обновление токена

**Scopes:**
- `user-modify-playback-state` — управление воспроизведением
- `user-read-playback-state` — чтение состояния воспроизведения
