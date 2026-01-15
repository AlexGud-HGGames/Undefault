# 🗺️ UndefaultIt - Development Roadmap

## Текущее состояние

✅ **Выполнено:**
- Solution scaffold (Core, GsiHost, UI проекты)
- **Модульная архитектура снапшотов:**
  - `ISnapshotModule` интерфейс
  - `GameSnapshot` с `IReadOnlyList<ISnapshotModule> Modules`
  - Модули: `VitalsModule`, `CombatModule`, `PositionModule`
  - `GetModule<TModule>()` для безопасного доступа
- **Маппинг через композицию:**
  - `ISnapshotModuleMapper` интерфейс
  - `GsiSnapshotMapper` с композицией мапперов
  - Реализации: `VitalsModuleMapper`, `CombatModuleMapper`, `PositionModuleMapper`
- Diff механизм (SnapshotDiffer работает с модулями через `GetModule<T>`)
- Event detection с cooldown/debounce (EventDetector)
- GSI Host pipeline (endpoint, маппинг, snapshot store)
- IRulesEngine интерфейс
- UI ViewModels (StatusViewModel, LogsViewModel)
- Базовые пакеты установлены

⏳ **В процессе / Осталось:**
- Spotify client (только placeholder)
- Rules engine реализация
- UI Views (есть ошибки компиляции)
- Интеграция Core → UI

---

## Архитектура: Модульная система снапшотов

### Концепция

Вместо жёстко заданной структуры `GameSnapshot` (health/armor/position и т.п.), используется **модульная архитектура**:

- `GameSnapshot` содержит `IReadOnlyList<ISnapshotModule> Modules`
- Каждый модуль (`VitalsModule`, `CombatModule`, `PositionModule`) реализует `ISnapshotModule`
- Доступ к модулям через `GetModule<TModule>()` (безопасно возвращает `null` если модуль отсутствует)

### Преимущества

1. **Гибкость:** Для каждой игры/режима можно собрать нужный набор модулей
2. **Расширяемость:** Добавление новой игры = новые модули + мапперы, без изменения Core
3. **Безопасность:** Diff и EventDetector безопасно обрабатывают отсутствующие модули
4. **Композиция:** Маппинг через `ISnapshotModuleMapper` (композиция вместо наследования)

### Как это работает

```
CS2 GSI Payload
    ↓
[VitalsModuleMapper, CombatModuleMapper, PositionModuleMapper]
    ↓
[VitalsModule, CombatModule, PositionModule]
    ↓
GameSnapshot(Modules: [...])
    ↓
SnapshotDiffer.GetModule<VitalsModule>() → безопасный доступ
    ↓
EventDetector работает с модулями
```

### Добавление новой игры (например, Dota 2)

1. Создать Dota 2 GSI DTOs
2. Создать новые модули (если нужны специфичные данные)
3. Создать мапперы `ISnapshotModuleMapper` для Dota 2 → модули
4. Зарегистрировать мапперы в DI
5. **Core остаётся без изменений** ✅

---

## Этапы разработки

### 📍 Этап 1: GSI Host Integration ✅
**Цель:** Принимать CS2 GSI данные и преобразовывать в Core snapshot pipeline

**Статус:** ✅ Выполнено

**Реализовано:**
- CS2 GSI DTOs (`GsiPayloadDto`, `PlayerDto`, `PlayerStateDto`, `MapDto`)
- Модульная архитектура маппинга (`ISnapshotModuleMapper`, композиция мапперов)
- Модули: `VitalsModule`, `CombatModule`, `PositionModule`
- `GsiSnapshotMapper` с композицией модулей
- Snapshot store (`ISnapshotStore`, `InMemorySnapshotStore`)
- GSI endpoint + pipeline (`GsiProcessingService`)
- Интеграция EventDetector в pipeline

**Архитектурное решение:**
Модульная архитектура позволяет подключать/отключать модули для разных игр/режимов без изменения Core. Diff и EventDetector безопасно работают с отсутствующими модулями через `GetModule<T>()`.

---

### 📍 Этап 2: Rules Engine Implementation
**Цель:** Реализовать правила маппинга событий → действия

**Задачи:**
- Agent A: Реализация IRulesEngine (базовые правила)
- Agent A: Конфигурация правил (event → action mapping)
- Agent A: Подготовка к Spotify actions (интерфейсы)

**Definition of Done:**
- RulesEngine может маппить события в действия
- Правила конфигурируемые (через Options/Config)
- Готовность к интеграции Spotify client

---

### 📍 Этап 3: Spotify Client Foundation
**Цель:** Базовый Spotify Web API клиент с OAuth

**Задачи:**
- Agent A: Spotify API client интерфейсы (ISpotifyClient)
- Agent A: OAuth flow (Authorization Code, token refresh)
- Agent A: Базовые методы (GetCurrentPlayback, Play, Pause, SetVolume)
- Agent A: Token storage (in-memory для MVP, позже persistent)

**Definition of Done:**
- Можно авторизоваться через Spotify OAuth
- Можно получить текущий playback state
- Можно управлять воспроизведением (play/pause/volume)
- Token refresh работает

---

### 📍 Этап 4: Rules → Spotify Integration
**Цель:** Связать Rules Engine с Spotify actions

**Задачи (вариант A — config-only):**
- Agent A: Перенести `RulesEngineOptions.ActionMap` в `GsiHost/appsettings.json`
- Agent A: Удалить хардкод маппинга из `GsiHost/Program.cs`
- Agent A: Биндинг `RulesEngineOptions` только через конфиг
- Agent A: Обработка ошибок Spotify API (логирование, без падений)

**Definition of Done:**
- ActionMap читается из конфигурации
- `Program.cs` не содержит доменной логики правил
- События из игры триггерят Spotify actions
- Правила работают end-to-end: GSI → Event → Rule → Spotify
- Ошибки Spotify API обрабатываются gracefully

---

### 📍 Этап 5: UI Integration & Status Display
**Цель:** UI отображает статус и логи, интегрирован с Core

**Задачи:**
- Agent B: Исправить ошибки компиляции UI (GroupBox и т.д.)
- Agent B: Реализовать StatusView с реальными данными из Core
- Agent B: Реализовать LogsView с событиями
- Agent B: Интеграция Core events в UI (через сервисы/интерфейсы)
- Agent B: DI wiring для Core сервисов в UI

**Definition of Done:**
- UI компилируется и запускается
- StatusView показывает: последнее событие, GSI статус, Spotify статус
- LogsView показывает поток событий
- UI получает данные из Core через интерфейсы

---

### 📍 Этап 6: Settings & Configuration
**Цель:** UI для настройки правил и конфигурации

**Задачи:**
- Agent B: SettingsView с настройками правил
- Agent B: Конфигурация GSI endpoint
- Agent B: Spotify auth flow в UI
- Agent A: Сохранение/загрузка конфигурации (JSON файл для MVP)

**Definition of Done:**
- Можно настроить правила через UI
- Можно указать GSI endpoint
- Можно авторизоваться в Spotify через UI
- Конфигурация сохраняется и загружается

---

### 📍 Этап 7: Testing & Polish
**Цель:** Тесты, обработка edge cases, финальная полировка

**Задачи:**
- Agent A: Unit тесты для Core (EventDetector, SnapshotDiffer)
- Agent A: Integration тесты для GsiHost endpoints
- Agent A: Обработка edge cases (GSI недоступен, Spotify API errors)
- Agent B: UI polish (UX улучшения, error states)

**Definition of Done:**
- Core покрыт unit тестами
- GsiHost покрыт integration тестами
- Edge cases обработаны
- UI показывает error states

---

### 📍 Этап 8: Dota 2 Support (Future)
**Цель:** Добавить поддержку Dota 2 GSI используя модульную архитектуру

**Задачи:**
- Agent A: Dota 2 GSI DTOs
- Agent A: Dota 2 модули (если нужны специфичные: `HeroModule`, `TeamModule` и т.д.)
- Agent A: Dota 2 мапперы (`ISnapshotModuleMapper` для Dota 2 → модули)
- Agent A: Dota 2 специфичные события (fight_start, roshan_kill) → NormalizedEvent
- Agent B: UI для выбора игры (CS2 / Dota 2)

**Преимущества модульной архитектуры:**
- Core остаётся без изменений
- Можно переиспользовать существующие модули (`VitalsModule`, `CombatModule`)
- Добавляются только новые модули и мапперы
- Diff и EventDetector работают автоматически

**Definition of Done:**
- Dota 2 GSI работает через модульную систему
- События нормализуются в общий формат
- UI поддерживает переключение игр

---

## Порядок выполнения

**MVP (Минимальный рабочий продукт):**
1. Этап 1: GSI Host Integration
2. Этап 2: Rules Engine Implementation  
3. Этап 3: Spotify Client Foundation
4. Этап 4: Rules → Spotify Integration
5. Этап 5: UI Integration & Status Display

**После MVP:**
6. Этап 6: Settings & Configuration
7. Этап 7: Testing & Polish
8. Этап 8: Dota 2 Support (опционально)

---

## Зависимости между этапами

```
Этап 1 (GSI Host) 
    ↓
Этап 2 (Rules Engine)
    ↓
Этап 3 (Spotify Client) ──┐
    ↓                      │
Этап 4 (Rules→Spotify) ←──┘
    ↓
Этап 5 (UI Integration)
    ↓
Этап 6 (Settings)
    ↓
Этап 7 (Testing)
```

---

## Вопросы для обсуждения

1. **Приоритет MVP:** Все 5 этапов MVP или можно сократить?
2. **Spotify OAuth:** Делать полноценный OAuth flow или stub для MVP?
3. **Конфигурация:** JSON файл достаточно или нужна БД?
4. **Тестирование:** На каком этапе начинать писать тесты?
