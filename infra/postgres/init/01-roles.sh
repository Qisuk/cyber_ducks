#!/usr/bin/env bash
# Service roles and databases (docs/data-model.md, ADR-0011).
# Runs once as the superuser ($POSTGRES_USER) on first start. Passwords come from .env (fake range values).
# No service ever connects as the superuser.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v store_owner_pw="$STORE_OWNER_PASSWORD" \
  -v store_app_pw="$STORE_APP_PASSWORD" \
  -v range_control_pw="$RANGE_CONTROL_PASSWORD" \
  -v flags_reader_pw="$FLAGS_READER_PASSWORD" <<'SQL'

-- Owns every object in duckstore. Used by store-api in *vulnerable* DB-role mode:
-- over-privileged within duckstore, but not a cluster superuser.
CREATE ROLE store_owner LOGIN PASSWORD :'store_owner_pw';

-- Least-privilege role for store-api in *secure* DB-role mode. Grants are in 02-schema.sql.
CREATE ROLE store_app LOGIN PASSWORD :'store_app_pw';

-- Owns the range_control database; also allowed to toggle database guards (03-guards.sql).
CREATE ROLE range_control_owner LOGIN PASSWORD :'range_control_pw';

-- Read-only access to defence flags, used by every range service.
CREATE ROLE flags_reader LOGIN PASSWORD :'flags_reader_pw';

-- duckstore: only the roles that need it may connect.
REVOKE CONNECT ON DATABASE duckstore FROM PUBLIC;
GRANT CONNECT ON DATABASE duckstore TO store_owner, store_app, range_control_owner;
ALTER SCHEMA public OWNER TO store_owner;

-- range_control: separate database the store roles cannot connect to.
CREATE DATABASE range_control OWNER range_control_owner;
REVOKE CONNECT ON DATABASE range_control FROM PUBLIC;
GRANT CONNECT ON DATABASE range_control TO range_control_owner, flags_reader;
SQL
