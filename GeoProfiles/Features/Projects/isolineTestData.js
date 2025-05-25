const {db} = require('../../Testing/fixtures');
const {
    convertObjectPropertiesToSnakeCase,
    convertObjectPropertiesToCamelCase,
} = require('../../Testing/utils');
const testData = require('../../Testing/testData');

async function prepareIsolineInDb(projectId, init = {}) {
    const isoline = {
        id: init.id ?? testData.random.uuid(),
        projectId,
        level: init.level ?? 0,
        geomWkt: init.geomWkt
            ?? 'POLYGON((-0.001 -0.001,0.001 -0.001,0.001 0.001,-0.001 0.001,-0.001 -0.001))',
    };

    const snake = convertObjectPropertiesToSnakeCase(isoline);
    const {geomWkt, ...rest} = snake;

    await db('isolines').insert({
        ...rest,
        geom: db.raw('ST_GeomFromText(?, 4326)', [geomWkt]),
    });

    return isoline;
}

async function getIsolinesForProject(projectId) {
    const rows = await db
        .select(
            'id',
            'project_id',
            'level',
            db.raw('ST_AsText(geom) as geom_wkt'),
        )
        .from('isolines')
        .where({project_id: projectId})
        .orderBy('level');

    return rows.map(convertObjectPropertiesToCamelCase);
}

module.exports = {
    isolines: {
        prepareIsolineInDb,
        getIsolinesForProject,
    },
};
