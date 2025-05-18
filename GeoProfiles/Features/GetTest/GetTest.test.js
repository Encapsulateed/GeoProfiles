const {httpClient} = require('../../Testing/fixtures');

const testData = {
    ...require('../../Testing/testData')
};

describe('api/v1/get-test', () => {
    it('should return 200 OK', async () => {
        // Arrange 
        const param = testData.random.alpha(20);
        // Act
        const response = await makeRequest(param);
        // Assert
        expect(response.status).toBe(200);
    })
    it('returns 400 VALIDATION_ERROR', async () => {
        // Arrange 
        const param = testData.random.alpha(5);

        // Act
        const response = await makeRequest(param);

        // Assert
        expect(response.status).toBe(400);
    })
})

async function makeRequest(param) {
    return await httpClient.get(`api/v1/get-test?param=${param}`);
}
