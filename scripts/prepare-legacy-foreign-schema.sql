\set ON_ERROR_STOP on

\if :{?source_host}
\else
    DO $$ BEGIN RAISE EXCEPTION 'source_host is required'; END $$;
\endif

\if :{?source_port}
\else
    \set source_port 5432
\endif

\if :{?source_database}
\else
    DO $$ BEGIN RAISE EXCEPTION 'source_database is required'; END $$;
\endif

\if :{?source_user}
\else
    DO $$ BEGIN RAISE EXCEPTION 'source_user is required'; END $$;
\endif

\if :{?source_schema}
\else
    \set source_schema public
\endif

\if :{?source_sslmode}
\else
    \set source_sslmode prefer
\endif

\if :{?staging_schema}
\else
    DO $$ BEGIN RAISE EXCEPTION 'staging_schema is required'; END $$;
\endif

\if :{?foreign_server}
\else
    DO $$ BEGIN RAISE EXCEPTION 'foreign_server is required'; END $$;
\endif

\getenv source_password SURVEY_LEGACY_SOURCE_PASSWORD
\if :{?source_password}
\else
    DO $$ BEGIN RAISE EXCEPTION 'source database password is required'; END $$;
\endif

CREATE EXTENSION IF NOT EXISTS postgres_fdw;

CREATE SERVER :"foreign_server"
    FOREIGN DATA WRAPPER postgres_fdw
    OPTIONS (
        host :'source_host',
        port :'source_port',
        dbname :'source_database',
        sslmode :'source_sslmode'
    );

CREATE USER MAPPING FOR CURRENT_USER
    SERVER :"foreign_server"
    OPTIONS (
        user :'source_user',
        password :'source_password'
    );

CREATE SCHEMA :"staging_schema";

IMPORT FOREIGN SCHEMA :"source_schema"
    LIMIT TO (
        organisation,
        users,
        roles,
        io,
        surveys,
        questions,
        surveys_questions,
        access_to_survey,
        survey_response,
        user_answers,
        csp
    )
    FROM SERVER :"foreign_server"
    INTO :"staging_schema";
