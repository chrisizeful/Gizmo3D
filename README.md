
<div align="right">
  <details>
    <summary >🌐 Language</summary>
    <div>
      <div align="center">
        <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=en">English</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=zh-CN">简体中文</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=zh-TW">繁體中文</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=ja">日本語</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=ko">한국어</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=hi">हिन्दी</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=th">ไทย</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=fr">Français</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=de">Deutsch</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=es">Español</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=it">Italiano</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=ru">Русский</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=pt">Português</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=nl">Nederlands</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=pl">Polski</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=ar">العربية</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=fa">فارسی</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=tr">Türkçe</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=vi">Tiếng Việt</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=id">Bahasa Indonesia</a>
        | <a href="https://openaitx.github.io/view.html?user=chrisizeful&project=Gizmo3D&lang=as">অসমীয়া</
      </div>
    </div>
  </details>
</div>

![Banner](https://i.imgur.com/qyWHmxW.png)

Gizmo3D encapsulates the Godot Engines 3D move/scale/rotation gizmos into a customizable node for use at runtime. The major differences are that you can edit all transformations at the same time, and customization options have been added. The selection box and axes can be toggled, colors changed, snapping intervals changed, and more. Transformation methods can be easily overriden to customize the default behavior. It is available in both C# and GDScript.

### Installation
Copy either Gizmo3DScript or Gizmo3DSharp from the addons folder into the addons folder of your project. Read more about installing and enabling addons [here](https://docs.godotengine.org/en/stable/tutorials/plugins/editor/installing_plugins.html). 

Once installed, you can add a Gizmo3D node to your project. For usage, it's recommended to take a look at the demo project - note that to use the demo you will have to **git clone** the repo, since the artifact is setup for use with the Godot Asset Library.

### Signals

C#                  | GDScript            |
--------------------|---------------------|
`SelectionChanged`  | `selection_changed` |
`TransformBegin`    | `transform_begin`   |
`TransformChanged`  | `transform_changed` |
`TransformEnd`      | `transform_end`     |

### Overridable Transformations

| C#                | GDScript             |
|-------------------|----------------------|
| `EditTranslate()` | `_edit_translate()`  |
| `EditScale()`     | `_edit_scale()`      |
| `EditRotate()`    | `_edit_rotate()`     |

### Licensing
Gizmo3D is largely a port of C++ code from the Godot Engine source. Gizmo3D is licensed under MIT, while the license for the Godot Engine can be found [here](https://godotengine.org/license/). The demo project uses assets from Kenney's CC0 licensed [Mini Dungeon](https://kenney.nl/assets/mini-dungeon) asset pack. The banner logo uses the [Dimbo](https://www.dafont.com/dimbo.font) font.
