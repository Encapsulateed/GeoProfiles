const axios = require("axios");

const httpClient = axios.create({
    baseURL: process.env.MOCKSERVER_URL ?? "http://127.0.0.1:1080",
    headers: {
        'Content-Type': 'application/json; charset=utf-8'
    }
})

const mockJsonResponse = async (setup, timeout = 10000) => {
    let delay;

    if (setup.delay) {
        delay = {
            timeUnit: "MILLISECONDS",
            value: setup.delay
        };
    }

    const times = { }
    if (setup.times === 'unlimited') {
        times.unlimited = true;
    }
    else {
        times.remainingTimes = setup.times ?? 1
    }

    await httpClient.put("/mockserver/expectation", {
        httpRequest: {
            method: setup.method,
            path: setup.path,
            body: setup.requestBody,
            queryStringParameters: setup.queryStringParameters,
            headers: setup.requestHeaders
        },
        httpResponse: {
            delay: delay,
            statusCode: setup.statusCode,
            body: setup.body ?? '',
            connectionOptions: setup.connectionOptions
        },
        times: times,
    }, {
        timeout: timeout
    });
}

module.exports = {
    mockJsonResponse,
    httpClient
}
