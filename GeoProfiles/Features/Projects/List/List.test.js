const { httpClient } = require('../../../Testing/fixtures');

const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/UserTestData'),
    ...require('../ProjectTestData'),
};

const { prepareUserInDb } = testData.users;
const { prepareProjectInDb, getProjectListFromDb } = testData.projects;
const { generateAccessToken } = require('../../../Testing/auth');

async function makeRequest() {
    return await httpClient.get('api/v1/project/list');
}

describe('GET /api/v1/project/list', () => {
    let owner;
    let stranger;
    let ownerProjects;

    beforeAll(async () => {
        // Arrange 
        [owner, stranger] = await Promise.all([
            prepareUserInDb({
                username:     `own_${testData.random.alphaNumeric(6)}`,
                email:        `${testData.random.alphaNumeric(5)}@example.com`,
                passwordHash: testData.random.alphaNumeric(60),
            }),
            prepareUserInDb({
                username:     `str_${testData.random.alphaNumeric(6)}`,
                email:        `${testData.random.alphaNumeric(5)}@example.com`,
                passwordHash: testData.random.alphaNumeric(60),
            }),
        ]);

        ownerProjects = [];
        for (let i = 0; i < 3; i++) {
            const proj = await prepareProjectInDb({
                userId: owner.id,
                name:   `list_proj_${i}`,
            });
            ownerProjects.push(proj);
        }

        await prepareProjectInDb({
            userId: stranger.id,
            name:   'other_user_proj',
        });
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    describe('happy path', () => {
        it('returns only owner projects with correct shape', async () => {
            // Arrange
            const token = await generateAccessToken({ userId: owner.id });
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest();

            // Assert: HTTP response
            expect(response.status).toBe(200);
            expect(Array.isArray(response.data.projects)).toBe(true);
            expect(response.data.projects.length).toBe(ownerProjects.length);

            // Assert: correct IDs
            const returnedIds = response.data.projects.map(p => p.id).sort();
            const expectedIds = ownerProjects.map(p => p.id).sort();
            expect(returnedIds).toEqual(expectedIds);

            // Assert: each item has id, name, bboxWkt, createdAt
            for (const item of response.data.projects) {
                expect(typeof item.id).toBe('string');
                expect(typeof item.name).toBe('string');
                expect(typeof item.bboxWkt).toBe('string');
                expect(typeof item.createdAt).toBe('string');
                expect(!isNaN(Date.parse(item.createdAt))).toBe(true);
            }

            // Assert: DB consistency
            const dbProjects = await getProjectListFromDb(ownerProjects.map(p => p.id));
            for (const dbProj of dbProjects) {
                const respProj = response.data.projects.find(p => p.id === dbProj.id);
                expect(respProj).toBeDefined();
                expect(respProj.name).toBe(dbProj.name);
                expect(respProj.bboxWkt).toBe(dbProj.bboxWkt);
                expect(Date.parse(respProj.createdAt)).toBe(dbProj.createdAt.getTime());
            }
        });
    });

    describe('unauthorized', () => {
        it('401 if no token provided', async () => {
            // Arrange: no auth header

            // Act
            const response = await makeRequest();

            // Assert
            expect(response.status).toBe(401);
        });

        it('401 for invalid token', async () => {
            // Arrange
            httpClient.defaults.headers['Authorization'] = 'Bearer invalid.token';

            // Act
            const response = await makeRequest();

            // Assert
            expect(response.status).toBe(401);
        });
    });

    describe('not found', () => {
        it('404 if token subject does not exist', async () => {
            // Arrange
            const fakeUserId = testData.random.uuid();
            const token = await generateAccessToken({ userId: fakeUserId });
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest();

            // Assert
            expect(response.status).toBe(404);
        });
    });

    describe('edge cases', () => {
        it('returns empty list for user without projects', async () => {
            // Arrange
            const newUser = await prepareUserInDb({
                username:     `new_${testData.random.alphaNumeric(6)}`,
                email:        `${testData.random.alphaNumeric(5)}@example.com`,
                passwordHash: testData.random.alphaNumeric(60),
            });
            const token = await generateAccessToken({ userId: newUser.id });
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest();

            // Assert
            expect(response.status).toBe(200);
            expect(Array.isArray(response.data.projects)).toBe(true);
            expect(response.data.projects.length).toBe(0);
        });
    });
});
