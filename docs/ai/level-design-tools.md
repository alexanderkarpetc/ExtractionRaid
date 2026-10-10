# Level Design Editor Tools

Повний користувацький посібник із параметрами, прикладами та troubleshooting:
[Environment Scatter та Map Analysis](../guides/level-design-tools.md).

Інструменти відкриваються через `Tools → Level Design → Environment Scatter / Map Analysis`.
Реалізація та тести в [Editor-only assembly](../../Assets/Tools/LevelDesign/Editor).
Runtime-збірки не змінені; prefab assets і NavMesh інструменти не перезаписують.

## Splat Map Painter

Малювання RGBA-карти шарів та окремої маски видимості по UV статичного меша для `ExtractShaders/SplatRGBA`:
[інструкція та обмеження](../guides/splat-map-painter.md).
Інструмент має окрему Editor-only збірку `SplatMap.Editor`; робочий матеріал і
raycast-колайдер тимчасові, PNG та призначення матеріалу зберігаються явно кнопкою.
Undo/Redo працює з робочим буфером, не повертає попередній вміст PNG на диску.
EditMode slice: `SplatMap.Editor.Tests`.
Road Marking Unlit підтримує Visibility Mask: канал RGBA читається в grayscale,
збереження призначає R без зміни blend/depth налаштувань. Road Builder генерує UV2;
матеріал обирає їх через Use UV2 for Painted Mask для локального малювання.
Painter підтримує незалежні Width/Height для Splat і Mask; відкриття й PNG-збереження
зберігають прямокутні розміри. Растеризація враховує окремий texel-крок кожної осі.
Кисть вимірюється у світових метрах і растеризує UV-трикутники за відстанню на меші;
масштаб об’єкта та розтягування UV не змінюють її світовий радіус. Геометрія кешується
при відкритті карти й зміні transform; shared-edge texel-и не отримують подвійну силу.
Маска читається з R; її збереження вмикає прозорий режим матеріалу. При зміні карти
поточні зміни потрібно зберегти або відкинути. Прозорий режим вимикає solid depth/normal
та shadow passes; Depth Offset змінює raster depth так само, як у Road Marking Unlit.

## Road Builder

Прокладання відкритої дороги точками у Scene View, генерація меша та окремих карт для
Splat Painter: [інструкція](../guides/road-builder.md).
`SplatRoad` містить лише authoring-параметри й Unity-посилання; Editor-only збірка
генерує assets явною кнопкою. Runtime-генерації та singleton-ів немає.
Lateral Offset зміщує поперечні ряди меша у світових метрах перед grounding,
без зміни контрольних точок; UV-дистанції залишаються за початковою трасою.
Create Overlay from Current Path створює незалежну копію точок без спільних generated assets.
Звичайний матеріал (Use Splat Maps вимкнено) використовує UV0 поперек 0–1 і повторення
вздовж дороги в метрах; paint maps не створюються. Режим обирається до першої побудови.
Для Splat UV0 залишаються 0–1 для фарбування; UV3 відділяють повторення текстур від карт малювання.
Перебудова не перезаписує намальовані PNG та блокується, поки карта дороги відкрита в Painter.
EditMode slice: `SplatRoads.Editor.Tests`. Grounding потребує Collider-ів і ручної
перевірки на робочій геометрії; довільні самоперетини не зшиваються автоматично.
T/X-вузли створюються явно з 3/4 кінців через Create Intersection from Selected Roads.
RoadIntersection зберігає підходи та власні assets. Обрізання зберігає UV рядків доріг;
центральний меш повторює повні поперечні стики та має окремі paint maps.
Edge Fade початкової маски обчислюється за відстанню XZ до відкритого зовнішнього
контуру; стики підходів виключені. Явна команда Rebuild and Create Edge Fade Mask
створює новий PNG без перезапису попереднього; звичайний Rebuild залишає маску.
Підключені дороги перебудовуються через вузол; Detach відновлює повну геометрію.
Поки що один вузол на об'єкт дороги; PNG підходів і незалежні overlay не змінюються.

## Environment Scatter

1. Відкрити й зберегти звичайну сцену. Задати Center, Size X/Z, Rotation Y. Edit Area Handles
   дозволяє змінювати область у Scene View. Center Y — центр вертикального діапазону raycast.
2. Додати активні prefab asset roots у Prefabs; вони повинні мати Renderer із bounds.
   Enabled/Weight визначають участь, від'ємний Min Distance Override означає глобальну дистанцію.
3. Вказати count, seed, ground/collision masks. Початковий prefab scale множиться на глобальний
   і per-entry випадковий scale; обертання prefab зберігається як базове.
4. Generate Preview / Randomize Seed замінює поточний `EnvironmentScatter_PREVIEW`.
   Bake перейменовує parent на `EnvironmentScatter_BAKED`; діти залишаються звичайними prefab
   instances. Clear Preview видаляє поточний Preview із дітьми, не зачіпаючи baked-групи.

Мінімальна дистанція — XZ між точками поточного розсіювання, для пари використовується більший
override. Наявна геометрія блокує розміщення через Collider і collision mask; об'єкти без Collider
не блокують фізичний запит. Нові props перевіряються між собою консервативними bounds, проти сцени —
oriented box. Collision Radius Multiplier змінює X/Z, не висоту. Контакт має малий допуск.
Grounding використовує bounds і локальну площину hit: нерівний terrain, навіси, порожнисті mesh
та складні pivots потребують візуальної перевірки. Renderer bounds можуть відрізнятися від colliders.

Exclusion Colliders приймає Collider або GameObject із Collider; використовуйте активні primitive
або convex colliders. Exclusion Layer Mask враховує також triggers. Перевіряється точка посадки.
Avoid NavMesh використовує просторовий радіус SamplePosition, включно з близькими поверхами.

Робота поступова, з часовим бюджетом Editor update. Cancel/Undo/закриття вікна/перехід у Play
зупиняють генерацію; частковий Preview залишається доступним для Clear/Bake.
Кожне розміщення має власну Undo-групу, щоб не захоплювати сторонні дії між пакетами; Clear/Bake —
окремі дії Undo. Для видалення всього Preview використовуйте Clear Preview.
Seed відтворюється за незмінної сцени, порядку prefab list і налаштувань.

Preview — звичайні сценові об'єкти: збереження сцени зберігає і Preview. Перед підготовкою рівня
до білда слід свідомо Bake або Clear. Сам Editor-код у білд не потрапляє.

## Map Analysis

Задати Box Area, Sample Spacing, висотний допуск та NavMesh agent type ID. NavMesh має бути вже
побудований. Указати masks для interesting geometry, cover і density. POI задаються у списку
налаштувань: scene object, ім'я, радіус і тип. Сценового компонента немає, список локальний
для дизайнера. Об'єкти з посилань мають належати збереженим і завантаженим сценам.

Analyze Map збирає геометрію завантажених сцен і кешує вимірювання, не змінюючи карту.
Зміни сцени або measurement settings потребують нового Analyze Map; до нього overlay використовує
thresholds останнього аналізу. Display, кольори й режим overlay змінюються без перерахунку.

- Interesting/cover distance — XZ-відстань до bounds активних Renderer та enabled non-trigger
  Collider на заданих layers. Це приблизна геометрична діагностика, не оцінка combat cover.
- Density рахує унікальні найближчі prefab instance roots; для інших об'єктів — GameObject.
  Renderer + Collider одного об'єкта не подвоюють лічильник. Відстань вимірюється до bounds.
- POI distance — XZ-відстань до краю радіуса. Відсутні цілі дають infinity; вимкнений модуль — grey.
- Connectivity перевіряє чотири світові напрямки через SamplePosition і NavMesh.Raycast.
  Це локальна прохідність; off-mesh links і NPC simulation не враховуються.
- Порожні кластери об'єднують сусідні grid samples тільки за прохідного NavMesh-зв'язку.
  Усі ввімкнені distance/density умови мають виконатися. Низька connectivity сама по собі
  не означає «поганий дизайн».
- Біле кільце позначає potential dead end за порогом; connectivity overlay показує також corridor.
  Клік по видимій sample відкриває вимірювання й flags у вікні.

Show Visualization вимикає результати, Show Area — окремо рамку. Clear Analysis очищає кеш;
Analyze/Clear підтримують Undo. Cancel залишає попередній завершений результат.
Ліміт samples перевіряється перед grid allocation; display budget обмежує кількість точок і
маркерів. Збір геометрії, sampling і clustering виконуються поступово; побудова bounds index —
окремий скінченний етап. Великі багатоповерхові bounds спотворюють XZ-метрики: аналізуйте
релевантні layers/поверхи окремо. Results — тимчасові Editor-дані, не build assets.

## Налаштування та перевірка

Налаштування зберігаються в EditorPrefs окремо для проєкту й користувача. Посилання мають
GlobalObjectId; після рестарту відповідні сцени повинні бути завантажені. Це локальні
налаштування, не спільний version-controlled preset. Нових singleton чи runtime state немає.
Об'єкти налаштувань мають бути прихованими й тимчасовими, але без `NotEditable`:
`HideAndDontSave` блокує їхні SerializedProperty-поля. Прапорці нормалізуються також після reload.

EditMode suite: `LevelDesign.Editor.Tests`. Інтеграційні fixtures створюють тимчасові сцени та
prefab через Editor API; запускайте в чистому тестовому проєкті або після збереження сцен.
Автотести не замінюють перевірку handles, читабельності overlay та продуктивності на робочій карті.
Стан приймання ведеться тільки в [tasks.md](tasks.md).
