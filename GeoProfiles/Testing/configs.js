const dbConfig = {
    client: 'pg',
    connection: {
        host: process.env.PG_INSTANCE ?? '127.0.0.1',
        user: process.env.PG_USER ?? 'db_usr',
        password: process.env.PG_PASSWORD ?? 'db_pass',
        database: process.env.PG_DATABASE ?? 'db'
    }
};

module.exports = {
    dbConfig
}