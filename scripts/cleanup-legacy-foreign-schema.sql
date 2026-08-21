\set ON_ERROR_STOP on

\if :{?foreign_server}
    DROP SERVER IF EXISTS :"foreign_server" CASCADE;
\endif

\if :{?staging_schema}
    DROP SCHEMA IF EXISTS :"staging_schema" CASCADE;
\endif
