# RAYLIB_API.md — проверенные имена API

Среда: Raylib-cs 4.2.0.2, TargetFramework net8.0.
Источник истины: зонд reflection в сборочной песочнице, не документация и не память.
Ссылки только по семантике функций (не по именам членов):
- https://www.raylib.com/cheatsheet/cheatsheet.html (C-API raylib)
- https://github.com/Raylib-cs/Raylib-cs (примеры обёртки)
- https://learn.microsoft.com/en-us/dotnet/api/system.mathf (BCL, сверять с net8.0)

Как дополнять файл: консольный проект с ссылкой на Raylib-cs нужной версии
печатает через reflection члены нужных типов (Enum.GetValues, GetFields,
GetMethods); результат вставляется сюда с пометкой версии. Старые разделы
не удалять, помечать версией проверки.

## KeyboardKey (enum)
Члены с префиксом KEY_: KEY_NULL (=0), KEY_W, KEY_A, KEY_S, KEY_D, KEY_E,
KEY_SPACE, KEY_TAB, KEY_ESCAPE, KEY_LEFT_CONTROL, KEY_LEFT_SHIFT и др.
None и Null НЕ СУЩЕСТВУЮТ. В config.json хранятся точные имена членов
(KEY_W и т.д.), парсинг через Enum.TryParse.

## CameraProjection (enum)
CAMERA_PERSPECTIVE = 0, CAMERA_ORTHOGRAPHIC = 1. Perspective НЕ СУЩЕСТВУЕТ.

## Raylib_cs.Camera3D (struct)
Поля строчными: position, target, up, fovy, projection.
Изменение полей возвращённой struct запрещено (CS1612): переприсваивать
структуру целиком (Data = new Raylib_cs.Camera3D { ... }).

## Texture2D (struct)
Поля строчными: id (uint), width, height, mipmaps, format. Id НЕ СУЩЕСТВУЕТ.

## Font (struct)
Поля: baseSize, glyphCount, glyphPadding, texture, recs, glyphs.
Id НЕ СУЩЕСТВУЕТ; сравнение с дефолтным шрифтом — через texture.id.

## Color (struct)
Статические поля ЗАГЛАВНЫМИ: LIGHTGRAY, GRAY, DARKGRAY, YELLOW, GOLD,
ORANGE, PINK, RED, MAROON, GREEN, LIME, DARKGREEN, SKYBLUE, BLUE,
DARKBLUE, PURPLE, VIOLET, DARKPURPLE, BEIGE, BROWN, DARKBROWN, WHITE,
BLACK, BLANK, MAGENTA, RAYWHITE. Black и White НЕ СУЩЕСТВУЮТ.

## System.MathF в net8.0
MathF.Clamp и MathF.Deg2Rad ОТСУТСТВУЮТ: использовать собственный Clamp
и const Deg2Rad = MathF.PI / 180f. MathF.Max, MathF.Sin, MathF.Cos — есть.
