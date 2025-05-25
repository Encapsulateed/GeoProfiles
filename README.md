# GeoProfiles Backend

Стек: .NET Core / C#

## Разработка

Для локальной разработки потребуются:

* .NET SDK 8.0 https://dotnet.microsoft.com/download
* Node.js https://nodejs.org/
* Docker https://www.docker.com/

### <a id="run-locally" name="run-locally"></a> Запуск проекта локально
1. Поднять локальную инфраструктуру (`Postgres`, `MockServer`). Для этого выполнить команду:

    ```bash
   chmod +x scripts/run-infra.sh
    ./scripts/run-infra.sh
    ```
   Эта команда поднимет `Postgres` на `localhost:5430`, `MockServer` на `localhost:1080`. Выполнит все миграции в бд.
2. После этого можно запустить локальный Debug в IDE

## Тесты
Все тесты являются интеграционными тестами.

### Запуск тестов (Jest)

Для тестов потребуется Node.js и NPM (входит в поставку Node.js по умолчанию). Для запуска:

* Установить зависимости - `npm install`
* Запустить само приложение (см. [Запуск проекта локально](#run-locally))
* Запустить тесты - `npm test`


* В Rider добавить новую конфигурацию с типом `Jest`

> Rider имеет полную поддержку Jest из коробки. Тесты можно запускать по одному и дебажить прямо в IDE, вместе с основным кодом (для этого и само приложение нужно запустить под дебагом).

Документация по Jest:

* Старт: https://jestjs.io/docs/getting-started
* Ассерты: https://jestjs.io/docs/using-matchers
* Больше ассертов: https://github.com/jest-community/jest-extended
