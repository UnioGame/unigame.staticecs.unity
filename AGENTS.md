# AGENTS — unigame.staticecs.unity

## Слой и зависимости

- Unity-интеграция Static ECS: `MonoBehaviour`-обёртки, конвертеры, дефолтный мир.
- Зависит от `unigame.staticecs` (базы). Используется `unigame.staticecs.features` и game-кодом.
- Здесь живёт дефолтный мир проекта [`Main`](Runtime/Main.cs) — единственная точка определения.

## World-default aliases

Применяется правило [world-default-aliases](../../../../docs/knowledge/static-ecs/conventions/world-default-aliases.md). Для нового generic-on-TWorld публичного API в этом пакете заводите Main-default алиас рядом с generic-версией (отдельный `<TypeName>.Main.cs` для классов; перегрузка в том же файле для статических операций).

`Main` определён здесь — внутри пакета можно ссылаться без дополнительного `using` (тот же namespace). Из `unigame.staticecs.features` Main подключается через `using unigame.staticecs.unity;`.
