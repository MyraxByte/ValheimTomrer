# Настройка проекта на Windows

Инструкция по шагам. Делай по порядку. Команды выполняй в PowerShell.

## 1. Что поставить

| Что | Где взять | Проверка |
|---|---|---|
| Steam и Valheim 1.0 | Steam | игра запускается |
| .NET SDK 9 или новее | https://dotnet.microsoft.com/download | `dotnet --version` |
| Node.js LTS | https://nodejs.org | `node --version` |
| Git for Windows | https://git-scm.com | `git --version` |
| VS Code | https://code.visualstudio.com | расширения: C# Dev Kit, Visual Studio Tools for Unity |
| Claude Code | `irm https://claude.ai/install.ps1 \| iex` (проверь актуальную команду в документации Claude Code) | `claude --version` |

Закрой и открой PowerShell после установки, чтобы команды нашлись.

## 2. BepInEx в игре

1. Скачай **BepInExPack_Valheim 5.4.2350** на Thunderstore (страница пакета, кнопка Manual Download).
2. Распакуй архив. Внутри папка `BepInExPack_Valheim`. Скопируй **её содержимое** в папку игры
   (там, где лежит `valheim.exe`). В игре появятся `winhttp.dll`, `doorstop_config.ini` и папка `BepInEx`.
   Папка игры обычно такая: `C:\Program Files (x86)\Steam\steamapps\common\Valheim`.
   (Steam, ПКМ по игре, Управление, Просмотреть локальные файлы.)
3. Запусти игру один раз и выйди. Проверь, что есть файл `BepInEx\LogOutput.log`.
   Если файла нет, BepInEx не подхватился: проверь, что `winhttp.dll` лежит рядом с `valheim.exe`.

На Windows не нужны ни `ARCHPREFERENCE`, ни Rosetta, ни снятие карантина. Это только для Mac.

## 3. ScriptEngine (горячая перезагрузка)

1. Скачай `ScriptEngine.dll` со страницы релизов https://github.com/BepInEx/BepInEx.Debug/releases
2. Положи его в `...\Valheim\BepInEx\plugins\`.
3. Запусти игру один раз. Появится `BepInEx\config\com.bepis.bepinex.scriptengine.cfg`.
   Открой его и проверь `EnableFileSystemWatcher = true`. Если такого пункта нет, после каждой сборки
   жми **F6** в игре.
4. Папка `BepInEx\scripts` создаётся сама. Наш мод туда кладёт DLL при горячей сборке.

## 4. Проект

**Не клонируй проект в папку игры** (`Valheim\BepInEx\plugins\...`). BepInEx загружает все DLL из `plugins`,
и мод загрузится несколько раз (из `bin` и `obj`). Клонируй в обычную папку, например `D:\dev`.

```powershell
mkdir D:\dev -Force; cd D:\dev
git clone https://github.com/MyraxByte/ValheimTomrer.git
cd ValheimTomrer
```

Если Steam стоит не в `Program Files (x86)` (например, `D:\SteamLibrary`), скажи проекту, где игра. Один из двух способов:

```powershell
# способ 1: переменная среды на всегда (перезапусти терминал после)
setx VALHEIM_INSTALL "D:\SteamLibrary\steamapps\common\Valheim"
```

```powershell
# способ 2: файл Directory.Build.props.user в корне проекта (git его не берёт)
@'
<Project><PropertyGroup>
  <ValheimInstall>D:\SteamLibrary\steamapps\common\Valheim</ValheimInstall>
</PropertyGroup></Project>
'@ | Set-Content Directory.Build.props.user
```

Первая сборка:

```powershell
npm run build
```

Успех: в конце строка `-> deployed ValheimTomrer.dll to ...\BepInEx\plugins\ValheimTomrer`.
Ошибка "could not find assembly_valheim.dll": неверный путь игры, вернись к шагу выше.

## 5. Проверка, что всё работает

```powershell
npm run dev
```

Скрипт соберёт мод, запустит игру и покажет строки нашего мода. Жди 10-20 секунд. Должно быть:

```
[Info   :ValheimTomrer] ValheimTomrer 0.1.0 loaded (build 2026-..)
[Info   :ValheimTomrer] main menu reached | harmony=OK | publicizer=OK
```

Если строк нет, смотри `BepInEx\LogOutput.log`. Если скрипт ругается на `ExecutionPolicy`, запускай через `npm run`,
он сам ставит нужный режим.

В игре: Настройки, Геймплей, **Включить консоль** (один раз). Потом F5 в мире, команда `devcommands`.

## 6. Работа без перезапуска игры (Hot Reload)

1. Терминал 1: `npm run dev:hot`. Собирает в `BepInEx\scripts`, запускает игру, показывает лог.
2. Зайди в мир.
3. Терминал 2: `npm run hot`. Следит за `src\` и пересобирает при каждом сохранении.
4. Поменяй код, сохрани. Через несколько секунд в логе новая строка
   `ValheimTomrer 0.1.0 loaded (build ...)` с новым временем. Игра не закрывалась.
5. Если автоматической перезагрузки нет, нажми **F6** в игре.

Что требует перезапуска игры: новый патч Harmony на метод, который игра уже выполнила, смена
`[BepInPlugin]`, смена ссылок в `csproj`.

Важно: DLL лежит либо в `plugins`, либо в `scripts`, не в обоих местах. Проект сам удаляет лишнюю копию.
Обычный `npm run build` и автотест кладут DLL в `plugins`. После них для горячей работы запусти `npm run build:hot`
или `npm run dev:hot`.

## 7. Отладчик

1. `npm run dev -- --debug` (игра с отладчиком на `127.0.0.1:10000`).
2. VS Code, Запуск и отладка, **Attach to Valheim**.
3. После горячей перезагрузки подключись снова: ScriptEngine каждый раз грузит переименованную копию сборки.

Если отладчик не подключается, проверь, что порт слушает: `Get-NetTCPConnection -LocalPort 10000`.
Если флаг `--doorstop-mono-debug-enabled true` у твоей версии BepInEx не сработал, открой `doorstop_config.ini` в папке игры
и поставь там `debug_enabled=true` (в секции `[UnityMono]`).

## 8. Тесты и релиз

| Команда | Что делает |
|---|---|
| `npm run autotest -- editor_all` | полная проверка, около 11 минут. Игра должна быть закрыта. Успех: `fail=0` |
| `npm run autotest -- blueprints` | один сценарий |
| `npm run check` | правила проекта: ничего не пишем в мир, нет картинок и моделей |
| `npm run zip` | пакет для Thunderstore: `thunderstore\build\ValheimTomrer.zip` |

Во время автотеста не трогай мышь и клавиатуру: тест сам управляет игрой.

## 9. Claude Code на Windows

```powershell
cd ValheimTomrer
claude
```

Правила для Claude лежат в `CLAUDE.md` и `.claude\rules\`. Скиллы: `/autotest`, `/release`, `/game-update`.
После каждой правки файла в `src\` автоматически работает проверка правил (нужен Node.js).

## Если что-то не работает

| Проблема | Что делать |
|---|---|
| Нет `LogOutput.log` | BepInEx не загрузился: `winhttp.dll` и `doorstop_config.ini` должны лежать рядом с `valheim.exe` |
| `0 plugins to load` | DLL не в `plugins\ValheimTomrer`: запусти `npm run build` |
| Мод загрузился дважды | DLL лежит и в `plugins`, и в `scripts`. Запусти `npm run build` или `npm run build:hot`, лишняя копия удалится |
| Горячая перезагрузка не срабатывает | нет `ScriptEngine.dll` в `plugins`, или DLL лежит в `plugins` (нужен `npm run build:hot`). Нажми F6 |
| `npm` не найден | поставь Node.js и открой новый терминал |
| Не находит `assembly_valheim.dll` | неверный путь игры: `VALHEIM_INSTALL` или `Directory.Build.props.user` |
| Другие симптомы | `docs\troubleshooting.md` |
