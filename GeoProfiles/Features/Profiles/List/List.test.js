
const { httpClient }          = require('../../../Testing/fixtures');
const customExpect            = require('../../../Testing/customExpect');
const { generateAccessToken } = require('../../../Testing/auth');

const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/UserTestData'),
    ...require('../../Projects/ProjectTestData'),
    ...require('../../Projects/isolineTestData'),
    ...require('./../ProfileTestData'),
};

const {
    randomBboxWkt,
    prepareProjectInDb,
    getProjectFromDb
} = testData.projects;

const {
    getProfileFromDb
} = testData.profile;

async function makeProfile(projectId, body) {
    return await httpClient.post(`api/v1/${projectId}/profile`, body);
}

async function makeList(projectId) {
    return await httpClient.get(`api/v1/${projectId}/list`);
}

describe('GET /api/v1/:projectId/list', () => {
    let user, token, project, projectRec;
    let start, end;
    let body1, body2;

    beforeAll(async () => {
        // Arrange: create user + token
        user  = await testData.users.prepareUserInDb({
            username:     testData.random.alphaNumeric(8),
            email:        `${testData.random.alphaNumeric(5)}@example.com`,
            passwordHash: testData.random.alphaNumeric(60),
        });
        token = await generateAccessToken({ userId: user.id });

        // Arrange: project with isolines
        const isolines = [
            { level: 0, geomWkt: randomBboxWkt() },
            { level: 1, geomWkt: randomBboxWkt() }
        ];
        project = await prepareProjectInDb({ userId: user.id }, isolines);

        // Read bbox to pick start/end inside it
        projectRec = await getProjectFromDb(project.id);
        const coords = projectRec.bboxWkt
            .match(/\(\((.+)\)\)/)[1]
            .split(',')
            .map(pt => pt.trim().split(' ').map(Number));
        const [[lonMin, latMin], , [lonMax, latMax]] = coords;
        const dx = (lonMax - lonMin) * 0.1;
        const dy = (latMax - latMin) * 0.1;
        start = [lonMin + dx, latMin + dy];
        end   = [lonMax - dx, latMax - dy];

        // Prepare two different profile requests
        body1 = { start, end };
        body2 = {
            start: [start[0] - dx, start[1]],
            end:   [end[0]   - dx, end[1]]
        };

        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        await makeProfile(project.id, body1);
        
        await new Promise(r => setTimeout(r, 50));
        await makeProfile(project.id, body2);
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    it('200 returns all created profiles in order', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

        // Act
        const res = await makeList(project.id);

        // Assert HTTP
        expect(res.status).toBe(200);
        expect(Array.isArray(res.data.items)).toBe(true);
        expect(res.data.items).toHaveLength(2);

        // Check first item matches body1
        const item1 = res.data.items[0];
        expect(item1.start).toEqual(body1.start);
        expect(item1.end).toEqual(body1.end);
        expect(typeof item1.length_m).toBe('number');
        expect(typeof item1.created_at).toBe('string');

        // DB consistency
        const db1 = await getProfileFromDb(item1.id);
        expect(db1).not.toBeNull();
        expect(item1.length_m).toBe(db1.lengthM);

        const item2 = res.data.items[1];
        expect(item2.start).toEqual(body2.start);
        expect(item2.end).toEqual(body2.end);

        const db2 = await getProfileFromDb(item2.id);
        expect(item2.length_m).toBe(db2.lengthM);
    });

    it('401 if not authenticated', async () => {
        // Act
        const res = await makeList(project.id);

        // Assert
        expect(res.status).toBe(401);
    });

    it('404 for invalid projectId format', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

        // Act
        const res = await httpClient.get('api/v1/not-a-guid/list');

        // Assert
        expect(res.status).toBe(404);
    });

    it('404 if project does not exist or not owned', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        const fakeId = testData.random.uuid();

        // Act
        const res = await makeList(fakeId);

        // Assert
        expect(res.status).toBe(404);
    });
});
