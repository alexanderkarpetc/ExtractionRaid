# Level Design Editor Tools

Повний користувацький посібник із параметрами, прикладами та troubleshooting:
[Environment Scatter та Map Analysis](../guides/level-design-tools.md).

Інструменти відкриваються через `Tools → Level Design → Environment Scatter / Map Analysis`.
Реалізація та тести в [Editor-only assembly](../../Assets/Tools/LevelDesign/Editor).
Runtime-збірки не змінені; prefab assets і NavMesh інструменти не перезаписують.

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
