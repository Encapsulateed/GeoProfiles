const { httpClient }         = require('../../../Testing/fixtures');
const customExpect           = require('../../../Testing/customExpect');
const { generateAccessToken }= require('../../../Testing/auth');

const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/UserTestData'),
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/testData'),
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
    getProfileFromDb,
    getProfilePointsFromDb
} = testData.profile;

async function makeProfileRequest(projectId, body) {
    return await httpClient.post(`api/v1/${projectId}/profile`, body);
}

describe('POST /api/v1/:projectId/profile', () => {
    let user, token, project, projectRec;
    let start, end;

    beforeAll(async () => {
        // Arrange
        user = await testData.users.prepareUserInDb({
            username:     testData.random.alphaNumeric(8),
            email:        `${testData.random.alphaNumeric(5)}@example.com`,
        passwordHash: testData.random.alphaNumeric(60),
    });
        token = await generateAccessToken({ userId: user.id });

        const isolines = [
            { level: 0, geomWkt: randomBboxWkt() },
            { level: 1, geomWkt: randomBboxWkt() }
        ];
        project = await prepareProjectInDb({ userId: user.id }, isolines);

        projectRec = await getProjectFromDb(project.id);
        const coords = projectRec.bboxWkt
            .match(/\(\((.+)\)\)/)[1]
            .split(',')
            .map(pt => pt.trim().split(' ').map(Number));

        const [ [lonMin, latMin], , [lonMax, latMax] ] = coords;

        const deltaLon = (lonMax - lonMin) * 0.1;
        const deltaLat = (latMax - latMin) * 0.1;

        start = [lonMin + deltaLon, latMin + deltaLat];
        end   = [lonMax - deltaLon, latMax - deltaLat];
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    const validBody = () => ({ start, end });

    describe('happy path', () => {
        it('201 → profile persisted + all points returned', async () => {
            // Arrange
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const res = await makeProfileRequest(project.id, validBody());

            // Assert HTTP
            expect(res.status).toBe(201);
            expect(typeof res.data.profileId).toBe('string');
            expect(typeof res.data.length_m).toBe('number');
            expect(Array.isArray(res.data.points)).toBe(true);
            expect(res.data.points.length).toBeGreaterThan(0);

            // Assert DB: профиль
            const prof = await getProfileFromDb(res.data.profileId);
            expect(prof).not.toBeNull();
            expect(prof.lengthM).toBe(res.data.length_m);

            // Assert DB: точки
            const ptsDb = await getProfilePointsFromDb(res.data.profileId);
            expect(ptsDb.length).toBe(res.data.points.length);

            // каждая точка совпадает и в правильном порядке
            ptsDb.forEach((dbPt, idx) => {
                const apiPt = res.data.points[idx];
                expect(apiPt.distance).toBeCloseTo(dbPt.distM, 6);
                expect(apiPt.elevation).toBeCloseTo(dbPt.elevM, 6);
            });
        });
    });

    describe('validation errors', () => {
        beforeEach(() => {
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        });

        it('400 when start/end missing', async () => {
            // Act
            const res = await makeProfileRequest(project.id, {});

            // Assert
            customExpect.toBeValidationError(res);
        });

        it('400 when start == end', async () => {
            // Act
            const res = await makeProfileRequest(project.id, { start: [0,0], end: [0,0] });

            // Assert
            customExpect.toBeValidationError(res);
        });
    });

    describe('unauthorized', () => {
        it('401 if no token', async () => {
            // Act
            const res = await makeProfileRequest(project.id, validBody());

            // Assert
            expect(res.status).toBe(401);
        });

        it('401 for invalid token', async () => {
            // Arrange
            httpClient.defaults.headers['Authorization'] = 'Bearer invalid';

            // Act
            const res = await makeProfileRequest(project.id, validBody());

            // Assert
            expect(res.status).toBe(401);
        });
    });

    describe('not found', () => {
        beforeEach(() => {
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        });

        it('404 if project missing', async () => {
            // Act
            const fakeId = testData.random.uuid();
            const res = await makeProfileRequest(fakeId, validBody());

            // Assert
            expect(res.status).toBe(404);
        });

        it('404 if project belongs to another user', async () => {
            // Arrange
            const otherUser = await testData.users.prepareUserInDb({
                username: testData.random.alphaNumeric(8),
                email: `${testData.random.alphaNumeric(5)}@example.com`,
            passwordHash: testData.random.alphaNumeric(60),
        });
            const iso = [{ level: 0, geomWkt: randomBboxWkt() }];
            const otherProj = await prepareProjectInDb({ userId: otherUser.id }, iso);

            // Act
            const res = await makeProfileRequest(otherProj.id, validBody());

            // Assert
            expect(res.status).toBe(404);
        });
    });
}); 