# Interactable Highlight / Outline (Підсвітка інтерактивних об'єктів)

This document describes the view-layer highlight stack for usable world objects:
ground items, lootables, containers, workbenches, and future interactables.
(Документ описує view-рівень підсвітки для об'єктів, з якими гравець може взаємодіяти:
предмети на землі, лут, контейнери, верстак та майбутні interactable-об'єкти.)

The goal is to make interactable objects readable without pushing gameplay rules
into MonoBehaviours.
(Мета: зробити interactable-об'єкти читабельними, не переносячи gameplay-правила в MonoBehaviour.)

## Architecture (Архітектура)

This feature is view-only.
(Ця фіча належить тільки до view/visual шару.)

- Gameplay interaction rules stay in `Systems/LootSystem`, `RaidSession`, and state.
  (Gameplay-правила взаємодії залишаються в `Systems/LootSystem`, `RaidSession` і state.)
- Highlight visuals live in `Assets/Scripts/View/`.
  (Візуальна підсвітка живе в `Assets/Scripts/View/`.)
- No `RaidState` stores Unity references.
  (`RaidState` не зберігає Unity-посилання.)
- No Unity `Layer` switching is used for outline selection.
  (Unity `Layer` не перемикається для вибору outline-об'єктів.)
- No material instances are created through `renderer.material`.
  (Не створюємо material instances через `renderer.material`.)

The stack is:

```text
InteractableOutlineTarget
  -> decides visual proximity state from player distance
  -> registers renderers + opacity in InteractableOutlineRegistry
  -> optionally triggers MaterialPropertyTweener components

InteractableOutlineRegistry
  -> stores active Renderer entries for the current frame

InteractableOutlineFeature
  -> URP RenderGraph feature
  -> renders active entries into a mask
  -> composites a screen-space outline around the mask

MaterialPropertyTweener
  -> optional reusable material-property animation helper
```

## Scripts (Скрипти)

### `InteractableOutlineTarget` (Ціль підсвітки)

File: `Assets/Scripts/View/InteractableOutlineTarget.cs`

Attach this to any usable object that should gain outline/highlight behaviour
when the player approaches it.
(Вішати на usable-об'єкт, який має отримувати outline/highlight, коли гравець підходить.)

It:
(Що робить:)

- finds child `Renderer` components;
  (знаходить дочірні `Renderer` компоненти;)
- checks distance to `App.Instance.RaidSession.RaidState.PlayerEntity`;
  (перевіряє дистанцію до гравця;)
- fades outline opacity using a curve;
  (плавно змінює прозорість outline через curve;)
- registers active renderers in `InteractableOutlineRegistry`;
  (реєструє активні renderer-и в `InteractableOutlineRegistry`;)
- triggers optional material tweeners on enter/exit.
  (запускає optional material tweeners при вході/виході з радіуса.)

It does not:
(Чого не робить:)

- change `gameObject.layer`;
  (не змінює `gameObject.layer`;)
- change `Renderer.renderingLayerMask`;
  (не змінює `Renderer.renderingLayerMask`;)
- modify gameplay state;
  (не змінює gameplay state;)
- decide whether the object is lootable/usable in gameplay terms.
  (не вирішує, чи об'єкт справді lootable/usable з точки зору gameplay.)

#### Inspector Fields (Поля інспектора)

`Highlighted`

Master visual gate. If false, this target will not outline or trigger tweeners.
Other scripts may set this at runtime.
(Головний visual-вимикач. Якщо false, об'єкт не дає outline і не запускає tweeners.
Інші скрипти можуть міняти це в runtime.)

`Include Inactive Children`

Whether child renderers on inactive GameObjects are collected when caching
renderers. Usually false.
(Чи збирати renderer-и з inactive child GameObject-ів. Зазвичай false.)

`Hide While Player In Menu`

If true, highlight turns off when player menus/inventory are open.
(Якщо true, підсвітка вимикається, коли у гравця відкрите меню/інвентар.)

`Activation Radius`

World-space radius around this GameObject where highlight starts.
(Радіус у world-space навколо GameObject, в якому вмикається підсвітка.)

`Fade Seconds`

Duration for outline opacity to move between off/on states.
(Час переходу прозорості outline між off/on станами.)

`Opacity From`

Outline opacity value at fade start.
(Прозорість outline на старті fade.)

`Opacity To`

Outline opacity value at fade end.
(Прозорість outline в кінці fade.)

`Opacity Curve`

Curve used to remap fade progress before lerping `Opacity From -> Opacity To`.
(Крива, яка ремапить fade progress перед lerp `Opacity From -> Opacity To`.)

`Proximity Tweeners`

Simple bidirectional material effects:
(Прості двонаправлені material-ефекти:)

- enter radius: `SetActive(true)` -> plays forward;
  (вхід у радіус: `SetActive(true)` -> програє вперед;)
- exit radius: `SetActive(false)` -> plays reverse.
  (вихід з радіуса: `SetActive(false)` -> програє назад.)

Use this when enter and exit should be the same animation reversed.
(Використовуй, коли вихід має бути тією ж анімацією у зворотному напрямку.)

`Activation Tweeners`

Custom enter effects. These restart and play forward when the player enters
radius.
(Кастомні enter-ефекти. Перезапускаються і програються вперед, коли гравець входить у радіус.)

Use this when the enter animation has its own curve/range, for example a quick
emission bloom.
(Використовуй, коли enter-анімація має власну curve/range, наприклад швидкий emission bloom.)

`Deactivation Tweeners`

Custom exit effects. These restart and play forward when the player exits
radius.
(Кастомні exit-ефекти. Перезапускаються і програються вперед, коли гравець виходить з радіуса.)

Use this when the exit animation should not simply be the reverse of enter.
(Використовуй, коли exit-анімація не має бути просто reverse від enter.)

### `InteractableOutlineRegistry` (Реєстр активної підсвітки)

File: `Assets/Scripts/View/InteractableOutlineRegistry.cs`

Static view-layer registry of currently highlighted renderers.
(Статичний view-layer реєстр renderer-ів, які зараз підсвічуються.)

Entries contain:

- `Renderer`
- `Opacity`

The render feature reads a snapshot each camera frame. Destroyed or disabled
renderers are pruned during snapshot creation.
(Render feature читає snapshot кожен camera frame. Destroyed/disabled renderer-и чистяться під час snapshot.)

This is intentionally not gameplay state. It is transient view glue.
(Це навмисно не gameplay state. Це тимчасовий view glue.)

### `InteractableOutlineFeature` (URP outline feature)

File: `Assets/Scripts/View/InteractableOutlineFeature.cs`

URP `ScriptableRendererFeature` that draws the screen-space outline.
(URP `ScriptableRendererFeature`, який малює screen-space outline.)

Pipeline:

1. Read active renderer entries from `InteractableOutlineRegistry`.
2. Render those renderers with `InteractableOutlineMask.shader` into a mask RT.
3. Composite `InteractableOutlineComposite.shader` over camera color.

The feature creates runtime materials from hidden shaders if overrides are not
assigned.
(Feature створює runtime materials з hidden shaders, якщо override-и не призначені.)

#### Inspector Fields (Поля інспектора)

`Mask Material Override`

Optional material override for the mask pass. Usually leave empty.
(Optional material override для mask pass. Зазвичай залишати empty.)

`Outline Material Override`

Optional material override for the composite pass. Usually leave empty.
(Optional material override для composite pass. Зазвичай залишати empty.)

`Outline Color`

Screen-space outline color.
(Колір screen-space outline.)

`Thickness Pixels`

Outline width in screen pixels.
(Товщина outline у screen pixels.)

`Opacity`

Global opacity multiplier. Target opacity from `InteractableOutlineTarget` is
multiplied by this value.
(Глобальний множник прозорості. Target opacity з `InteractableOutlineTarget`
множиться на це значення.)

### `MaterialPropertyTweener` (Твінер параметрів матеріалу)

File: `Assets/Scripts/View/MaterialPropertyTweener.cs`

Reusable component for animating one or more material properties on a renderer.
(Reusable компонент для анімації одного або кількох material property на renderer-і.)

It uses `MaterialPropertyBlock`, so it does not instantiate or mutate shared
materials.
(Використовує `MaterialPropertyBlock`, тому не створює material instances і не мутує shared materials.)

#### Inspector Fields (Поля інспектора)

`Target Renderer`

Renderer whose material properties will be animated. If empty, the component
uses the first child renderer found in `Awake`.
(Renderer, чиї material properties будуть анімуватись. Якщо empty, компонент бере перший child renderer в `Awake`.)

`Material Index`

Material slot index on the renderer. Most objects use `0`.
(Індекс material slot на renderer-і. Більшість об'єктів використовують `0`.)

`Duration`

Animation length in seconds.
(Довжина анімації в секундах.)

`Play On Enable`

If true, plays forward when the component is enabled.
(Якщо true, програє forward при enable компонента.)

`Use Unscaled Time`

If true, uses `Time.unscaledDeltaTime`.
(Якщо true, використовує `Time.unscaledDeltaTime`.)

`Tracks`

List of material properties to animate. Each track has:
(Список material properties для анімації. Кожен track має:)

- `Property Name`
- `Type`: `Float`, `Color`, or `Vector`
- `Curve`
- `Float From / To`
- `Color From / To`
- `Vector From / To`

Only the fields matching the selected `Type` are used.
(Використовуються тільки поля, що відповідають вибраному `Type`.)

#### Public Methods (Публічні методи)

`PlayForward()`

Continue playing forward from the current time.
(Продовжити програвання вперед з поточного часу.)

`PlayReverse()`

Continue playing backward from the current time.
(Продовжити програвання назад з поточного часу.)

`RestartForward()`

Reset to start and play forward.
(Скинути на старт і програти вперед.)

`RestartReverse()`

Reset to end and play backward.
(Скинути в кінець і програти назад.)

`SetActive(bool active)`

Forward when true, reverse when false.
(Forward при true, reverse при false.)

`SetNormalized(float normalized)`

Immediately set animation position from `0..1`.
(Одразу встановити позицію анімації від `0..1`.)

`Stop()`

Stop at the current value.
(Зупинити на поточному значенні.)

`SetFloatRange(int trackIndex, float from, float to)`

Change a float track range at runtime.
(Змінити діапазон float track в runtime.)

`SetColorRange(int trackIndex, Color from, Color to)`

Change a color track range at runtime.
(Змінити діапазон color track в runtime.)

`SetVectorRange(int trackIndex, Vector4 from, Vector4 to)`

Change a vector track range at runtime.
(Змінити діапазон vector track в runtime.)

## Typical Setup (Типові налаштування)

### Simple outline only (Тільки outline)

1. Add `InteractableOutlineTarget` to the object root.
2. Set `Activation Radius`.
3. Set `Fade Seconds`, `Opacity From`, `Opacity To`, and `Opacity Curve`.
4. Make sure `InteractableOutlineFeature` is added to the URP renderer asset.

### Outline + emission boost (Outline + підсилення emission)

1. Add `MaterialPropertyTweener` to the object.
2. Assign target renderer.
3. Add a float track, for example:
   - `Property Name = _EmissionStrength`
   - `Float From = 0`
   - `Float To = 2`
4. Assign the tweener into `InteractableOutlineTarget.Proximity Tweeners`.

### Separate enter and exit effects (Окремі enter/exit ефекти)

Use two tweeners:

- Enter tweener in `Activation Tweeners`
- Exit tweener in `Deactivation Tweeners`

Both play forward. Configure each tweener's own `From -> To` range and curve.

Example:

```text
Activation Tweener:
  _EmissionStrength: 0 -> 3
  Curve: soft ease out

Deactivation Tweener:
  _EmissionStrength: 3 -> 0
  Curve: fast ease in
```

## Known Tradeoffs (Відомі компроміси)

Target opacity is currently applied as a global maximum in the outline composite
pass. This keeps the RenderGraph path simple and avoids global-state writes
inside raster passes.
(Target opacity зараз застосовується як global maximum у composite pass.
Це тримає RenderGraph шлях простим і не потребує global-state writes у raster pass.)

If multiple highlighted objects overlap with different opacity values, they will
share the strongest active opacity for the final composite. If true per-object
opacity becomes important, the mask pass should be split into opacity buckets or
drawn with a shader path that reliably receives per-renderer values under
override materials.
(Якщо кілька highlighted objects мають різну opacity, фінальний composite візьме найсильнішу активну opacity.
Якщо знадобиться справжня per-object opacity, mask pass треба буде розділити на opacity buckets
або перейти на shader path, який надійно отримує per-renderer values під override material.)

## Related Files (Пов'язані файли)

- `Assets/Scripts/View/GroundItemPresenter.cs`
- `Assets/Scripts/View/LootablePresenter.cs`
- `Assets/Scripts/View/WorkbenchView.cs`
- `Assets/Shaders/InteractableOutlineMask.shader`
- `Assets/Shaders/InteractableOutlineComposite.shader`
- `docs/ai/architecture.md`
- `docs/ai/entity-lifecycle.md`
- `docs/ai/fog-of-war.md`
