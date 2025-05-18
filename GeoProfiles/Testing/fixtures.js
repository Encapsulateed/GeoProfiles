const axios = require("axios");
const knex = require('knex').knex;
const pg = require('pg');
const {dbConfig} = require("./configs");

const db = knex(dbConfig);

afterAll(async () => {
    await db.destroy();
});

const httpClient = axios.create({
    baseURL: process.env.API_BASE_URL ?? 'http://localhost:8080'
});

// Для тестов мы не хотим чтобы 400-ки и 500-ки были ошибками в коде
httpClient.interceptors.response.use((response) => {
    return response;
}, (error) => {
    if (error.response) return error.response;
    throw error;
});

// Большие постгресовские числовые типы (например, decimal) возвращаются как строки, т.к. они слишком большие, чтобы влзеть в JS-овский number
// В тестах форсим парсинг, т.к. можем контролировать значения тестовых данных (чтобы они были маленькими и влезали в number)
pg.types.setTypeParser(pg.types.builtins.NUMERIC, parseFloat);

// Данные из колонок Date-типа будут возвращаться как строки "yyyy-MM-dd"
pg.types.setTypeParser(pg.types.builtins.DATE, (value) => value);

module.exports = {
    db,
    httpClient
}