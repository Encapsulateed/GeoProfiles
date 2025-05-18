const _ = require('lodash');

const delay = (ms) => new Promise((res, _) => {
    setTimeout(() => res(), ms);
});

const cutTrailingZerosOutOfISOString = (isoString) => isoString.replace(/0+Z$/, 'Z').replace(/\.Z$/, 'Z');

function removeUnwantedFields(obj, unwanted) {
    const result = {};

    for (const prop in obj) {
        if (!obj.hasOwnProperty(prop)) {
            continue;
        }
        if (unwanted.includes(prop)) {
            continue;
        }
        if (obj[prop] == null) {
            continue;
        }
        result[prop] = obj[prop];
    }
    return result;
}

const convertObjectPropertiesToSnakeCase = (object) => {
    const result = {};
    for (const key in object) {
        if (Object.hasOwnProperty.call(object, key)) {
            result[_.snakeCase(key)] = object[key];
        }
    }
    return result;
};

const convertObjectPropertiesToCamelCase = (object) => {
    const result = {};
    for (const key in object) {
        if (Object.hasOwnProperty.call(object, key)) {
            result[_.camelCase(key)] = object[key];
        }
    }
    return result;
};

module.exports = {
    delay,
    cutTrailingZerosOutOfISOString,
    removeUnwantedFields,
    convertObjectPropertiesToSnakeCase,
    convertObjectPropertiesToCamelCase
}