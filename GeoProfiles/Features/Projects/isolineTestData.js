const {db} = require('../../Testing/fixtures');
const {
    convertObjectPropertiesToSnakeCase,
    convertObjectPropertiesToCamelCase,
} = require('../../Testing/utils');
const testData = require('../../Testing/testData');

/**
 * Вставляет изолинию в БД.
 * Если WKT-геометрия – LineString/MultiLineString, она автоматически
 * превращается в Polygon через ST_Envelope (bounding-box).
 *
 * @param {string}  projectId
 * @param {object}  [init]
 * @param {string}  [init.geomWkt]  – в любом случае SRID     = 4326
 * @param {number}  [init.level=0]
 */
async function prepareIsolineInDb(projectId, init = {}) {
    const isoline = {
        id: init.id ?? testData.random.uuid(),
        projectId,
        level: init.level ?? 0,
        geomWkt: init.geomWkt
            ?? 'POLYGON((0 0,0.002 0,0.002 0.002,0 0.002,0 0))',
    };

    const snake = convertObjectPropertiesToSnakeCase(isoline);
    const geomWkt = snake.geom_wkt;
    delete snake.geom_wkt;

    const wktUpper = geomWkt.trim().toUpperCase();
    const geomExpression = wktUpper.startsWith('POLYGON')
        ? db.raw('ST_GeomFromText(?, 4326)', [geomWkt])
        : db.raw('ST_Envelope(ST_GeomFromText(?, 4326))', [geomWkt]);
    
    await db('isolines').insert({
        ...snake,
        geom: geomExpression,
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
