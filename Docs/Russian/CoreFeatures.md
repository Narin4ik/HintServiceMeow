Нажмите [здесь](/Docs/Russian/README.md), чтобы вернуться к README

# Основные функции

Данная документация охватывает публичный API HintServiceMeow, организованный по функциональным модулям.

## Адаптация к экрану игрока

Встроенный парсер автоматически учитывает соотношение сторон, которое клиент игры передаёт через `AspectRatioSync`. Получать точное разрешение в пикселях или устанавливать клиентские моды не требуется. Пока данные клиента не получены, используется соотношение сторон **16:9**. После синхронизации или изменения размера игрового окна уже показанные подсказки пересчитываются.

Рабочая область имеет высоту 1080 единиц и ширину `1080 × соотношение сторон`. Сверху и снизу сохраняется защитный отступ 16 единиц; по горизонтали принудительного отступа нет, поэтому текст можно подвести к левому или правому краю экрана. Подсказка сначала сдвигается внутрь экрана. Если она больше рабочей области, её текст, размеры в поддерживаемых тегах `<size>` и межстрочный интервал пропорционально уменьшаются. Автоматическое увеличение шрифта и дополнительный перенос строк не применяются.

Для `Hint` выравнивание `Left` и `Right` привязывается к соответствующему краю рабочей области; `XCoordinate` задаёт смещение относительно выбранного края. Для `Center` координата X отсчитывается от центра экрана. `YCoordinate` и `YCoordinateAlign` сохраняют прежний смысл.

`DynamicHint` ищет позицию с учётом видимой области и собственных границ. Свойства `LeftMargin`, `RightMargin`, `TopMargin` и `BottomMargin` задают расстояние до других подсказок и применяются только при проверке пересечений. Они не увеличивают отступ от края экрана и не уменьшают доступную для текста область. При ограничении экраном учитываются фактические размеры текста, без дополнительных боковых отступов и с защитным отступом 16 единиц сверху и снизу; зона коллизии с отступами может выходить за экран. Размеры и позиции коллизий учитывают адаптацию. Стратегия `Hide` скрывает подсказку, если свободное место не найдено; `StayInPosition` допускает пересечение с другими подсказками, но оставляет текст внутри экрана. `TargetX` задаёт целевую координату горизонтального центра блока; в CustomHint ей соответствует `position_x`.

Адаптация выполняется отдельно при рендеринге для каждого игрока и не изменяет исходный объект подсказки. Один объект можно использовать для игроков с разными экранами. Публичный интерфейс `IHintParser` сохранён; пользовательские реализации парсера самостоятельно отвечают за свою раскладку.

---

## Содержание

- [Основные функции](#основные-функции)
  - [Адаптация к экрану игрока](#адаптация-к-экрану-игрока)
  - [Содержание](#содержание)
  - [Модели подсказок](#модели-подсказок)
    - [AbstractHint](#abstracthint)
    - [Hint](#hint)
    - [DynamicHint](#dynamichint)
  - [PlayerDisplay](#playerdisplay)
  - [Слой UI](#слой-ui)
    - [PlayerUI](#playerui)
    - [CommonHint](#commonhint)
  - [Методы расширения](#методы-расширения)
    - [Расширения AbstractHint](#расширения-abstracthint)
    - [Расширения PlayerDisplay](#расширения-playerdisplay)
    - [Расширения NW Player](#расширения-nw-player)
    - [Расширения EXILED Player](#расширения-exiled-player)
  - [Содержимое подсказки](#содержимое-подсказки)
    - [AbstractHintContent](#abstracthintcontent)
    - [StringContent](#stringcontent)
    - [AutoContent](#autocontent)

---

## Модели подсказок

### AbstractHint

> Пространство имён: `HintServiceMeow.Core.Models.Hints`

Базовый класс для всех типов подсказок. **Все подсказки (`Hint`, `DynamicHint` и др.) наследуются от `AbstractHint`**, который предоставляет общие свойства, такие как текстовое содержимое, размер шрифта, скорость синхронизации и видимость. Реализует `INotifyPropertyChanged`, чтобы изменения свойств автоматически вызывали обновление отображения.

При создании любого типа подсказки свойства, перечисленные ниже, всегда доступны независимо от конкретного типа подсказки.

**Свойства:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| Guid | `Guid` (только чтение) | Автоматически генерируемый уникальный идентификатор |
| Id | `string` | Пользовательский строковый идентификатор для поиска. По умолчанию: `""` |
| SyncSpeed | `HintSyncSpeed` | Приоритет обновления. По умолчанию: `Normal`. Значения: `Fastest` (192) — обновляется как можно скорее, может задерживать другие подсказки; `Fast` (160) — планирует обновление немедленно при изменении; `Normal` (128) — стандартная скорость; `Slow` (96) — ждёт других подсказок; `Slowest` (64) — ждёт дольше; `UnSync` (32) — без автосинхронизации, но всё равно обновляется, когда другие подсказки инициируют синхронизацию |
| FontSize | `int` | Размер шрифта текста. По умолчанию: `20` |
| LineHeight | `float` | Дополнительный вертикальный интервал между строками |
| Content | `AbstractHintContent` | Провайдер содержимого для данной подсказки. По умолчанию: `StringContent("")` |
| Text | `string?` | Ярлык для получения/задания статического текста. Задание этого свойства заменяет `Content` новым `StringContent` |
| AutoText | `AutoContent.TextUpdateHandler?` | Ярлык для получения/задания делегата динамического текста. Задание этого свойства заменяет `Content` новым `AutoContent` |
| Hide | `bool` | Скрыта ли подсказка. По умолчанию: `false` |

**Пример использования:**

```csharp
// Свойства автоматически синхронизируются с экраном игрока
hint.Text = "Обновлённый текст";
hint.FontSize = 30;
// Дополнительных вызовов методов не требуется
```

---

### Hint

> Пространство имён: `HintServiceMeow.Core.Models.Hints`

Подсказка с фиксированной позицией, отображаемая в конкретных координатах экрана. Наследуется от [AbstractHint](#abstracthint).

**Свойства (в дополнение к AbstractHint):**

| Свойство | Тип | Описание |
|----------|-----|----------|
| YCoordinate | `float` | Вертикальная позиция. Большие значения перемещают текст ниже на экране. По умолчанию: `700` |
| XCoordinate | `float` | Горизонтальное смещение. Большие значения перемещают текст вправо. По умолчанию: `0` |
| Alignment | `HintAlignment` | Выравнивание текста. Значения: `Left`, `Right`, `Center`. По умолчанию: `Center` |
| YCoordinateAlign | `HintVerticalAlign` | Как координата Y выравнивается относительно текста. Значения: `Top` — Y является верхним краем; `Middle` — Y является вертикальным центром; `Bottom` — Y является нижним краем. По умолчанию: `Middle` |

![Пример координаты Y](Images/YCoordinateExample.jpg)

**Пример использования:**

```csharp
Hint hint = new Hint
{
    Text = "Hello World",
    FontSize = 40,
    YCoordinate = 700,
    Alignment = HintAlignment.Left
};

PlayerDisplay playerDisplay = PlayerDisplay.Get(player);
playerDisplay.AddHint(hint);
```

Поскольку в HSM есть функция автообновления, любые изменения свойств автоматически отражаются на экране игрока без дополнительных вызовов методов.

```csharp
hint.Text = "Какой-то новый текст";
// Дополнительных вызовов методов не требуется
```

---

### DynamicHint

> Пространство имён: `HintServiceMeow.Core.Models.Hints`

Подсказка, которая автоматически позиционируется, чтобы избежать перекрытия с другими подсказками. Наследуется от [AbstractHint](#abstracthint).

**Свойства (в дополнение к AbstractHint):**

| Свойство | Тип | Описание |
|----------|-----|----------|
| TopBoundary | `float` | Верхняя граница для размещения. По умолчанию: `0` |
| BottomBoundary | `float` | Нижняя граница для размещения. По умолчанию: `1000` |
| LeftBoundary | `float` | Левая граница для размещения. По умолчанию: `-1200` |
| RightBoundary | `float` | Правая граница для размещения. По умолчанию: `1200` |
| TargetX | `float` | Предпочтительная горизонтальная позиция. По умолчанию: `0` |
| TargetY | `float` | Предпочтительная вертикальная позиция. По умолчанию: `700` |
| TopMargin | `float` | Дополнительное пространство выше подсказки при расстановке. По умолчанию: `5` |
| BottomMargin | `float` | Дополнительное пространство ниже подсказки при расстановке. По умолчанию: `5` |
| LeftMargin | `float` | Дополнительное пространство слева при расстановке. По умолчанию: `100` |
| RightMargin | `float` | Дополнительное пространство справа при расстановке. По умолчанию: `100` |
| Priority | `HintPriority` | Приоритет расстановки. Подсказки с более высоким приоритетом расставляются первыми. Значения: `Highest` (192), `High` (160), `Medium` (128), `Low` (96), `Lowest` (64). По умолчанию: `Medium` |
| Strategy | `DynamicHintStrategy` | Поведение при отсутствии доступного места. Значения: `Hide` — скрыть подсказку; `StayInPosition` — оставить на целевой позиции. По умолчанию: `Hide` |

**Пример использования:**

```csharp
var dynamicHint = new DynamicHint
{
    Text = "Привет, динамическая подсказка"
};

PlayerDisplay playerDisplay = PlayerDisplay.Get(player);
playerDisplay.AddHint(dynamicHint);
```

---

## PlayerDisplay

> Пространство имён: `HintServiceMeow.Core.Utilities`

Центральный класс для управления отображением подсказок игрока. У каждого игрока есть один экземпляр `PlayerDisplay`.

**События:**

| Событие | Тип | Описание |
|---------|-----|----------|
| UpdateAvailable | `UpdateAvailableEventHandler` | Вызывается каждый тик, когда дисплей готов к обновлению |

**Делегат:** `delegate void UpdateAvailableEventHandler(UpdateAvailableEventArg ev)`

**Свойства:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| ReferenceHub | `ReferenceHub?` (только чтение) | Игрок, которому принадлежит данный дисплей |
| HintParser | `IHintParser` | Парсер, который преобразует подсказки в rich text. Заменяемый |
| CompatibilityAdaptor | `ICompatibilityAdaptor` | Адаптер для совместимости с другими плагинами. Заменяемый |

**Статические методы:**

| Метод | Параметры | Возвращает | Описание |
|-------|-----------|------------|----------|
| Get | `ReferenceHub referenceHub` | `PlayerDisplay` | Получает или создаёт PlayerDisplay для игрока |
| Get | `LabApi.Features.Wrappers.Player player` | `PlayerDisplay` | Получает или создаёт PlayerDisplay (NW/LabApi) |
| Get | `Exiled.API.Features.Player player` | `PlayerDisplay` | Получает или создаёт PlayerDisplay (только EXILED) |

**Методы экземпляра:**

| Метод | Параметры | Возвращает | Описание |
|-------|-----------|------------|----------|
| AddHint | `AbstractHint? hint` | `void` | Добавляет подсказку в дисплей |
| AddHint | `IEnumerable<AbstractHint>? hints` | `void` | Добавляет несколько подсказок |
| AddHint | `params AbstractHint[]? hints` | `void` | Добавляет несколько подсказок (params) |
| AddHint | `AbstractHint? hint, string groupName` | `void` | Добавляет подсказку в определённую группу |
| ShowHint | `AbstractHint hint, float duration = 7f, AfterShowAction afterShow = AfterShowAction.Remove` | `void` | Добавляет подсказку и автоматически удаляет/скрывает её через `duration` секунд. Значения `AfterShowAction`: `Remove` — удалить подсказку; `Hide` — установить `Hide = true` |
| ShowHint | `IEnumerable<AbstractHint> hints, float duration = 7f, AfterShowAction afterShow = AfterShowAction.Remove` | `void` | Отображает несколько подсказок с автоудалением |
| RemoveHint | `AbstractHint? hint` | `void` | Удаляет подсказку |
| RemoveHint | `IEnumerable<AbstractHint>? hints` | `void` | Удаляет несколько подсказок |
| RemoveHint | `params AbstractHint[]? hints` | `void` | Удаляет несколько подсказок (params) |
| RemoveHint | `AbstractHint? hint, string groupName` | `void` | Удаляет подсказку из определённой группы |
| RemoveHint | `string id` | `void` | Удаляет все подсказки с указанным Id |
| RemoveHint | `Guid id` | `void` | Удаляет подсказку с указанным Guid |
| ClearHint | — | `void` | Удаляет все подсказки, принадлежащие вызывающей сборке |
| GetHint | `string? id` | `AbstractHint?` | Возвращает первую подсказку с соответствующим Id |
| GetHint | `Guid guid` | `AbstractHint?` | Возвращает первую подсказку с соответствующим Guid |
| GetHints | `string id` | `IEnumerable<AbstractHint>` | Возвращает все подсказки с соответствующим Id |
| GetHints | — | `IEnumerable<AbstractHint>` | Возвращает все подсказки, принадлежащие вызывающей сборке |
| HasHint | `string id` | `bool` | Проверяет, существует ли подсказка с указанным Id |
| HasHint | `Guid guid` | `bool` | Проверяет, существует ли подсказка с указанным Guid |
| TryGetHint | `string id, out AbstractHint hint` | `bool` | Пытается получить первую подсказку с соответствующим Id |
| TryGetHint | `Guid guid, out AbstractHint hint` | `bool` | Пытается получить первую подсказку с соответствующим Guid |
| TryGetHints | `string? id, out IEnumerable<AbstractHint> hints` | `bool` | Пытается получить все подсказки с соответствующим Id |
| ForceUpdate | `bool useFastUpdate = false` | `void` | Принудительно обновляет дисплей. Используйте при работе с `HintSyncSpeed.UnSync` |
| SetMinUpdateInterval | `TimeSpan interval` | `void` | Устанавливает минимальный интервал между обновлениями |
| AddDisplayOutput | `IDisplayOutput output` | `void` | Добавляет пользовательский вывод дисплея |
| RemoveDisplayOutput | `IDisplayOutput output` | `void` | Удаляет вывод дисплея |
| RemoveDisplayOutput\<T\> | — | `void` | Удаляет все выводы дисплея типа `T` (где `T : IDisplayOutput`) |

**Пример использования:**

```csharp
PlayerDisplay pd = PlayerDisplay.Get(player);

// Добавить подсказку
var hint = new Hint { Text = "Привет", YCoordinate = 500 };
pd.AddHint(hint);

// Показать временную подсказку на 5 секунд
pd.ShowHint(new Hint { Text = "Временная!" }, duration: 5f);

// Найти и изменить подсказки
if (pd.TryGetHint("my-hint-id", out var found))
{
    found.Text = "Обновлено";
}

// Принудительное обновление для UnSync подсказок
pd.ForceUpdate();
```

---

## Слой UI

### PlayerUI

> Пространство имён: `HintServiceMeow.UI.Utilities`

Фасад пользовательского интерфейса для каждого игрока, предоставляющий доступ к [CommonHint](#commonhint).

**Свойства:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| ReferenceHub | `ReferenceHub` (только чтение) | Ссылка на базового игрока |
| PlayerDisplay | `PlayerDisplay` (только чтение) | Экземпляр PlayerDisplay игрока |
| CommonHint | `CommonHint` (только чтение) | Компонент общих подсказок |

**Статические методы:**

| Метод | Параметры | Возвращает | Описание |
|-------|-----------|------------|----------|
| Get | `ReferenceHub referenceHub` | `PlayerUI` | Получает или создаёт PlayerUI для игрока |
| Get | `LabApi.Features.Wrappers.Player player` | `PlayerUI` | Получает или создаёт PlayerUI (NW/LabApi) |
| Get | `Exiled.API.Features.Player player` | `PlayerUI` | Получает или создаёт PlayerUI (только EXILED) |

---

### CommonHint

> Пространство имён: `HintServiceMeow.UI.Utilities`

Предоставляет предварительно настроенные макеты подсказок для распространённых случаев: описания предметов, информация о карте, информация о роли и общие сообщения.

Все длительности отображения настраиваются через конфигурацию плагина. Параметр `time` в каждой перегрузке указывается в секундах.

**Методы — подсказки предмета:**

| Метод | Параметры | Описание |
|-------|-----------|----------|
| ShowItemHint | `string itemName` | Показывает только название предмета (короткая длительность) |
| ShowItemHint | `string itemName, float time` | Показывает только название предмета с пользовательской длительностью |
| ShowItemHint | `string itemName, string description` | Показывает название предмета и одну строку описания |
| ShowItemHint | `string itemName, string description, float time` | Показывает название предмета и одну строку описания с пользовательской длительностью |
| ShowItemHint | `string itemName, string[] description` | Показывает название предмета и несколько строк описания |
| ShowItemHint | `string itemName, string[] description, float time` | Показывает название предмета и несколько строк описания с пользовательской длительностью |

**Методы — подсказки карты:**

| Метод | Параметры | Описание |
|-------|-----------|----------|
| ShowMapHint | `string roomName` | Показывает только название комнаты (короткая длительность) |
| ShowMapHint | `string roomName, float time` | Показывает только название комнаты с пользовательской длительностью |
| ShowMapHint | `string roomName, string description` | Показывает название комнаты и одну строку описания |
| ShowMapHint | `string roomName, string description, float time` | Показывает название комнаты и одну строку описания с пользовательской длительностью |
| ShowMapHint | `string roomName, string[] description` | Показывает название комнаты и несколько строк описания |
| ShowMapHint | `string roomName, string[] description, float time` | Показывает название комнаты и несколько строк описания с пользовательской длительностью |

**Методы — подсказки роли:**

| Метод | Параметры | Описание |
|-------|-----------|----------|
| ShowRoleHint | `string roleName` | Показывает только название роли (короткая длительность) |
| ShowRoleHint | `string roleName, float time` | Показывает только название роли с пользовательской длительностью |
| ShowRoleHint | `string roleName, string description` | Показывает название роли и одну строку описания |
| ShowRoleHint | `string roleName, string description, float time` | Показывает название роли и одну строку описания с пользовательской длительностью |
| ShowRoleHint | `string roleName, string[] description` | Показывает название роли и несколько строк описания |
| ShowRoleHint | `string roleName, string[] description, float time` | Показывает название роли и несколько строк описания с пользовательской длительностью |

**Методы — прочие подсказки:**

| Метод | Параметры | Описание |
|-------|-----------|----------|
| ShowOtherHint | `string messages` | Показывает одно сообщение как DynamicHint |
| ShowOtherHint | `string messages, float time` | Показывает одно сообщение с пользовательской длительностью |
| ShowOtherHint | `string[] messages` | Показывает несколько сообщений (длительность масштабируется с количеством) |
| ShowOtherHint | `string[] messages, float time` | Показывает несколько сообщений с пользовательской общей длительностью |

**Пример использования:**

```csharp
var ui = PlayerUI.Get(player);
ui.CommonHint.ShowRoleHint("SCP-173", new[] { "Убить всех людей", "Использовать навыки" });
ui.CommonHint.ShowMapHint("Зона Тяжёлого Содержания", "Место, где появляется большинство SCP");
ui.CommonHint.ShowItemHint("Карта доступа", "Используется для открытия дверей");
ui.CommonHint.ShowOtherHint("Сервер запускается!");
```

---

## Методы расширения

Все методы расширения собраны здесь для удобного использования.

### Расширения AbstractHint

> Пространство имён: `HintServiceMeow.Core.Extension`

| Метод | Расширяет | Параметры | Описание |
|-------|-----------|-----------|----------|
| HideAfter | `AbstractHint` | `float delay` | Устанавливает `Hide = true` через `delay` секунд. Сбрасывает любой существующий таймер скрытия |

```csharp
hint.HideAfter(5f); // Скрывает подсказку через 5 секунд
```

### Расширения PlayerDisplay

> Пространство имён: `HintServiceMeow.Core.Extension`

| Метод | Расширяет | Параметры | Описание |
|-------|-----------|-----------|----------|
| RemoveAfter | `PlayerDisplay` | `AbstractHint hint, float delay` | Удаляет подсказку из дисплея через `delay` секунд. Сбрасывает любой существующий таймер удаления |

```csharp
playerDisplay.RemoveAfter(hint, 10f); // Удаляет подсказку через 10 секунд
```

### Расширения NW Player

> Пространство имён: `HintServiceMeow.Core.Extension` / `HintServiceMeow.UI.Extension`

Методы расширения для `LabApi.Features.Wrappers.Player`.

| Метод | Пространство имён | Возвращает | Описание |
|-------|-------------------|------------|----------|
| GetPlayerDisplay | Core | `PlayerDisplay` | Получает PlayerDisplay игрока |
| AddHint | Core | `void` | Добавляет подсказку в дисплей игрока |
| RemoveHint | Core | `void` | Удаляет подсказку из дисплея игрока |
| GetPlayerUi | UI | `PlayerUI` | Получает экземпляр PlayerUI игрока |

```csharp
// Использование расширений NW player (LabApi)
LabApi.Features.Wrappers.Player player = ...;

// Получить PlayerDisplay и добавить подсказку прямо на объект игрока
var hint = new Hint { Text = "Привет из расширения NW!", YCoordinate = 500 };
player.AddHint(hint);

// Позже удалить
player.RemoveHint(hint);

// Доступ к PlayerDisplay для более сложных операций
PlayerDisplay pd = player.GetPlayerDisplay();
pd.ShowHint(new Hint { Text = "Временная!" }, duration: 3f);

// Доступ к PlayerUI и CommonHint
PlayerUI ui = player.GetPlayerUi();
ui.CommonHint.ShowRoleHint("SCP-096", new[] { "Сидеть и плакать", "Преследовать цели" });
```

### Расширения EXILED Player

> Пространство имён: `HintServiceMeow.Core.Extension` / `HintServiceMeow.UI.Extension`

Методы расширения для `Exiled.API.Features.Player`. Доступны только в сборках EXILED.

| Метод | Пространство имён | Возвращает | Описание |
|-------|-------------------|------------|----------|
| GetPlayerDisplay | Core | `PlayerDisplay` | Получает PlayerDisplay игрока |
| AddHint | Core | `void` | Добавляет подсказку в дисплей игрока |
| RemoveHint | Core | `void` | Удаляет подсказку из дисплея игрока |
| GetPlayerUi | UI | `PlayerUI` | Получает экземпляр PlayerUI игрока |

```csharp
// Использование расширений EXILED player
Exiled.API.Features.Player player = ...;

// Получить PlayerDisplay и добавить подсказку прямо на объект игрока
var hint = new Hint { Text = "Привет из расширения EXILED!", YCoordinate = 500 };
player.AddHint(hint);

// Позже удалить
player.RemoveHint(hint);

// Доступ к PlayerDisplay для более сложных операций
PlayerDisplay pd = player.GetPlayerDisplay();
pd.ShowHint(new Hint { Text = "Временная!" }, duration: 3f);

// Доступ к PlayerUI и CommonHint
PlayerUI ui = player.GetPlayerUi();
ui.CommonHint.ShowItemHint("Карта O5", "Открывает доступ ко всем зонам");
```

---

## Содержимое подсказки

Эти классы используются внутри подсказок для управления их текстовым содержимым. В большинстве случаев вам не нужно взаимодействовать с ними напрямую — используйте свойства `Text` или `AutoText` в [AbstractHint](#abstracthint).

### AbstractHintContent

> Пространство имён: `HintServiceMeow.Core.Models.HintContent`

Базовый класс для провайдеров содержимого подсказок.

**События:**

| Событие | Тип | Описание |
|---------|-----|----------|
| ContentUpdated | `UpdateHandler` | Вызывается при изменении содержимого |

**Методы:**

| Метод | Параметры | Возвращает | Описание |
|-------|-----------|------------|----------|
| GetText | — | `string?` | Возвращает текущее текстовое содержимое |

---

### StringContent

> Пространство имён: `HintServiceMeow.Core.Models.HintContent`

Провайдер содержимого, хранящий статический текст. Наследуется от [AbstractHintContent](#abstracthintcontent).

**Конструктор:** `StringContent(string? content)`

**Свойства:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| Text | `string?` | Статический текст. При изменении вызывает `ContentUpdated` |

---

### AutoContent

> Пространство имён: `HintServiceMeow.Core.Models.HintContent`

Провайдер содержимого, периодически вызывающий делегат для генерации динамического текста. Наследуется от [AbstractHintContent](#abstracthintcontent).

**Делегат:** `delegate string TextUpdateHandler(AutoContentUpdateArg ev)`

**Конструктор:** `AutoContent(TextUpdateHandler? autoText, float defaultUpdateInterval = -1)`

Если `defaultUpdateInterval` отрицательный, по умолчанию используется `0.1` секунды.

**Свойства:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| AutoText | `TextUpdateHandler?` | Делегат, вызываемый для генерации текста. Сброс этого свойства также сбрасывает время следующего обновления |

**Пример использования:**

```csharp
hint.AutoText = (ev) =>
{
    ev.NextUpdateDelay = TimeSpan.FromSeconds(1); // Обновлять каждую 1 секунду
    return $"Время: {DateTime.Now:HH:mm:ss}";
};
```

Нажмите [здесь](/Docs/Russian/README.md), чтобы вернуться к README
