# Подпись релизов (SignPath Foundation)

Публичные релизы подписываются бесплатно через [SignPath Foundation](https://signpath.org/).

## Подготовка

1. В корне репозитория есть `LICENSE` (MIT).
2. Подать заявку: https://signpath.org/
3. После одобрения создать проект и signing policy в SignPath.io.
4. Установить SignPath GitHub App на репозиторий.
5. Добавить в GitHub:

| Тип | Имя | Описание |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | API token с правами submitter |
| Variable | `SIGNPATH_ORGANIZATION_ID` | UUID организации |
| Variable | `SIGNPATH_PROJECT_SLUG` | slug проекта |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | обычно `release-signing` |

## Как работает

При manual release (`workflow_dispatch` на `main`/`master`) CI:

1. собирает single-file EXE;
2. отправляет его в SignPath;
3. кладёт подписанный файл в GitHub Release.

Локальный MSI (`build-installer.ps1`) не подписывается — подпись только в CI.

## В заявке

Опишите, что клиент показывает статистику боя и по согласию пользователя может заменить экран загрузки игры (модификация ресурсов, не чит/античит).

## Проверка

```powershell
signtool verify /pa /v .\XVMBlitz-1.1.3-win-x64.exe
Get-AuthenticodeSignature .\XVMBlitz-1.1.3-win-x64.exe
```
