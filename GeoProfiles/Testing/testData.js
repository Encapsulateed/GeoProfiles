const uuid = require('uuid');
const faker = require('faker');

const letterAlphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ';
const randomString = (length = 10, alphabet = '1234567890' + letterAlphabet) => {
    const result = new Array(length);
    const numberOfUUIDs = Math.ceil(length / 16);
    for (let i = 0; i < numberOfUUIDs; i++) {
        const bytes = uuid.parse(uuid.v4());
        for (let j = 0; j < bytes.length; j++) {
            const pos = 16 * i + j;
            if (pos >= length) break;
            result[pos] = bytes[j];
        }
    }

    for (let i = 0; i < result.length; i++) {
        const x = result[i] % alphabet.length;
        result[i] = alphabet[x];
    }

    return result.join('');
};

/**
 * Получает случайное число.
 * @param {number} min - нижняя граница, включительно.
 * @param {number} max - верхняя граница, исключительно.
 */
function getRandomInt(min, max) {
    return Math.floor(Math.random() * (max - min) + min);
}

const randomNumericString = (length = 10) => {
    const result = randomString(length, '1234567890');

    return result[0] === '0'
        ? getRandomInt(1, 10) + result.slice(1)
        : result;
};

const chooseRandomElement = (arr) => {
    return arr[getRandomInt(0, arr.length)];
}

const getRandomDateString = (locale = 'ru-RU') => faker.date.recent().toLocaleDateString(locale);

function getRandomValidInn(length) {
    let inn;
    const coefficients = [3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8];

    switch (length) {
        case 10:
            inn = randomNumericString(9);
            inn = inn + getDigit(inn, coefficients.slice(2));
            return inn;
        case 12:
            inn = randomNumericString(10);
            inn = inn + getDigit(inn, coefficients.slice(1));
            inn = inn + getDigit(inn, coefficients);
            return inn;
    }
}

function getDigit (inn, coefficients) {
    let sum = 0;
    for (let i in coefficients) {
        sum += coefficients[i] * inn[i];
    }
    return sum % 11 % 10;
}

module.exports = {
    number: {
        maxFourByteInteger: () => Math.pow(2, 32) - 1,
        maxSignedFourByteInteger: () => Math.pow(2, 31) - 1,
    },
    decimal: {
        maxValueAsString: () => "79228162514264337593543950335",
    },
    uuid: {
        empty: () => uuid.NIL,
    },
    random: {
        uuid: () => uuid.v4(),
        alphaNumeric: (length = 10) => randomString(length),
        alpha: (length = 10) => randomString(length, letterAlphabet),
        numeric: (length = 10) => randomNumericString(length),
        number: (length = 10) => parseInt(randomNumericString(length)),
        /**
         * Получает случайное число.
         * @param {number} min - нижняя граница, включительно.
         * @param {number} max - верхняя граница, исключительно.
         */
        numberInRange: (min, max) => getRandomInt(min, max),
    },
    date: {
        dateOnlyISOString: (addYears = 0) => faker.date.future(addYears).toISOString().slice(0, 10),
        dateOnlyString: (format = 'ru-RU') => getRandomDateString(format),
        asUTC: (date) => new Date(Date.UTC(
            date.getFullYear(), date.getMonth(), date.getDate(),
            date.getHours(), date.getMinutes(), date.getSeconds(), date.getMilliseconds())),
    },
    phone: {
        phoneNumberRu: () => `+79${randomNumericString(9)}`,
    },
}