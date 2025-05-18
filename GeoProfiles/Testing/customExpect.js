const {
    version: uuidVersion,
    validate: uuidValidate,
} = require("uuid");

const {convertObjectPropertiesToCamelCase} = require("../Testing/utils");
const { removeUnwantedFields } = require("./utils");

const unwanted = ['createdAt', 'updatedAt', 'isDeleted'];

module.exports = {
    toBeNotFoundError: (response, errorCode = 'NOT_FOUND') => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(400);
        expect(response.data).toHaveProperty('errorCode', errorCode);
    },
    toBeUnprocessableEntityError: (response, errorCode) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(422);
        expect(response.data).toHaveProperty('errorCode', errorCode);
    },
    toBeUnprocessableEntityErrorWithReason: (response, errorCode, reason) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(422);
        expect(response.data).toHaveProperty('errorCode', errorCode);
        expect(response.data.errorDetails.reasons).toBeArrayOfSize(1);
        expect(response.data.errorDetails.reasons[0]).toHaveProperty('code', reason);
    },
    toBeValidationError: (response, property = null) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(400);
        expect(response.data).toHaveProperty('errorCode', 'VALIDATION_ERROR');
        if (property) {
            expect(response.data).toHaveProperty('errorDetails');
            const errorFields = Object.keys(response.data.errorDetails);
            expect(errorFields).toEqual(expect.arrayContaining([expect.stringMatching(property)]));
        }
    },
    toBeValidListResponse: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(200);
        expect(response.data).toBeTruthy();
        expect(response.data).toHaveProperty('results');
        expect(response.data.results).not.toBeNull();
        expect(response.data.results).toBeArray();
        expect(response.data).toHaveProperty('offset');
        expect(response.data.offset).toBeGreaterThanOrEqual(0);
        expect(response.data).toHaveProperty('limit');
        expect(response.data.limit).toBeGreaterThanOrEqual(0);
        expect(response.data).toHaveProperty('size');
        expect(response.data.size).toBeGreaterThanOrEqual(0);
        expect(response.data).toHaveProperty('total');
        expect(response.data.total).toBeGreaterThanOrEqual(0);
    },
    toBeUnsupportedBodyFormatError: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(400);
        expect(response.data).toHaveProperty('errorCode', 'UNSUPPORTED_BODY_FORMAT');
    },
    toBeUnsupportedMediaTypeError: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(400);
        expect(response.data).toHaveProperty('errorCode', 'UNSUPPORTED_MEDIA_TYPE');
    },
    toBeOkResponse: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(200);
    },
    toBeCreatedResponse: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(201);
    },
    toBeUnauthorizedResponse: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(401);
    },
    toBeForbiddenResponse: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(403);
    },
    toBeInternalError: (response) => {
        expect(response).toBeTruthy();
        expect(response.status).toBe(500);
        expect(response.data).toHaveProperty('errorCode', 'INTERNAL_ERROR');
    },
    toBeValidUuid: (value) => {
        expect(uuidValidate(value)).toBeTrue();
        expect(uuidVersion(value)).toBe(4);
    },
    toBeValidOutboxEvent: (outboxEventDbEntity, expectedPayload) => {
        expect(outboxEventDbEntity).toBeTruthy();
        const outboxEventPayload = JSON.parse(outboxEventDbEntity.payload);
        const normalizedExpectedPayload = convertObjectPropertiesToCamelCase(removeUnwantedFields(expectedPayload, unwanted));
        expect(outboxEventPayload).toEqual(normalizedExpectedPayload);
        expect(outboxEventDbEntity.failedReason).toBeNull();
        expect(outboxEventDbEntity.retryCount).toBe(0);
        expect(outboxEventDbEntity.retryAfter).toBeNull();
        expect(outboxEventDbEntity.createdAt).toStrictEqual(outboxEventDbEntity.createdAt);
        expect(outboxEventDbEntity.updatedAt).toStrictEqual(outboxEventDbEntity.createdAt);
    }
}